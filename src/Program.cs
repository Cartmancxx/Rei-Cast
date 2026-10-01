using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ReiCast {
static class Program {
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static int Main(string[] args) {
        try {
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length>1&&args[0]=="--render-avatar") {using(var art=AvatarArt.Create())art.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);return 0;}
            if(args.Length>1&&args[0]=="--list-displays") {Json.Atomic(args[1],ScreenCatalog.List());return 0;}
            if(args.Length>0 && args[0]=="--recover") {using(var signal=EventWaitHandle.OpenExisting("Local\\ReiCast.Recover.1")) signal.Set();return 0;}
            if(args.Length>1 && args[0]=="--self-test") return Tests.Run(args[1]);
            if(args.Length>1 && args[0]=="--inspect") { Paths.Init();var config=Config.Read(Paths.ConfigFile);using(var cp=new CodexProvider(Paths.Codex(config))) Json.Atomic(args[1],cp.Poll(DateTime.UtcNow));return 0; }
            if(args.Length>1 && args[0]=="--render-preview") {
                Size previewSize=new Size(854,480);if(args.Length>3) {string[] dimensions=args[3].Split('x');int width,height;if(dimensions.Length==2&&Int32.TryParse(dimensions[0],out width)&&Int32.TryParse(dimensions[1],out height)&&width>=240&&height>=240&&width<=4096&&height<=4096)previewSize=new Size(width,height);}
                using(var d=new Display(new Config(),true,true)) using(var bmp=new Bitmap(previewSize.Width,previewSize.Height)) {
                    d.Health="界面预览 · 示例数据";d.Metrics=new Metrics {Cpu=8,Ram=32,Gpu=4,UsedGb=20.4,TotalGb=64,DownKbps=24,UpKbps=3};d.Weather=new Weather {City="成都",Temperature=25,FeelsLike=26,Summary="多云",Updated=DateTime.UtcNow};
                    if(args.Length>2&&args[2]=="busy") { d.Active=new TaskInfo {Title="构建小屏助手，验证任务同步与运行负载",State="running",Phase="正在执行操作",Started=DateTime.UtcNow.AddMinutes(-3),Tools=12};d.ActiveCount=1; }
                    if(args.Length>2&&args[2]=="dot") d.Dialogue=new DotDialogue {Id="preview",Demo=true,Updated=DateTime.UtcNow,UserMessage="能不能在水冷小屏上显示 Dot 的对话？",Reply="可以。这里会显示你最近的消息和 Dot 的回复。\n\n较长的回复会自动翻页；一段时间没有新消息，就恢复日期、天气和电脑性能。\n\n这是界面示例，真实对话需要由 Dot 发布到本地。"};
                    using(var g=Graphics.FromImage(bmp)) d.PaintScene(g,bmp.Size);bmp.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
                } return 0;
            }
            Paths.Init();if(args.Length>0&&args[0]=="--enable-startup") {Startup.Set(true);return 0;}if(args.Length>0&&args[0]=="--disable-startup") {Startup.Set(false);return 0;}
            bool first;using(var single=new Mutex(true,"Local\\ReiCast.Desktop.1",out first)) {if(!first) return 0; Config config; try {config=Config.Read(Paths.ConfigFile);} catch {config=new Config();Paths.Log("config: defaults used due to invalid file");} Application.Run(new Display(config,Array.IndexOf(args,"--preview")>=0));}
            return 0;
        } catch(Exception e) {Paths.Log("fatal: "+e);return 1;}
    }
}
static class Tests {
    static List<string> passed=new List<string>();
    static void Check(bool yes,string title) {if(!yes) throw new Exception(title);passed.Add(title);}
    static Dictionary<string,object> Event(string kind,string stamp,string turn="a") {return Json.Parse("{\"timestamp\":\""+stamp+"\",\"type\":\"event_msg\",\"payload\":{\"type\":\""+kind+"\",\"turn_id\":\""+turn+"\"}}");}
    public static int Run(string report) {
        string temp=Path.Combine(Path.GetTempPath(),"ReiCastTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try {
            var t=new TaskInfo();TaskReducer.Apply(t,Event("task_started","2026-09-25T01:00:00Z"));Check(t.State=="running","task start");
            TaskReducer.Apply(t,Event("task_complete","2026-09-25T01:01:00Z","old"));Check(t.State=="running","old turn completion ignored");
            TaskReducer.Apply(t,Event("task_complete","2026-09-25T01:02:00Z"));Check(t.State=="complete","completion");
            TaskReducer.Apply(t,Event("task_started","2026-09-25T00:00:00Z"));Check(t.State=="complete","out of order event ignored");
            TaskReducer.Apply(t,Event("task_started","2026-09-25T02:00:00Z","b"));TaskReducer.Apply(t,Event("turn_aborted","2026-09-25T02:01:00Z","b"));Check(t.State=="interrupted","interruption");
            string file=Path.Combine(temp,"tail.jsonl");File.WriteAllText(file,"{\"字\":\"成都\"}\n",new System.Text.UTF8Encoding(false));var tail=new TailFile(file);Check(new List<string>(tail.Read(100)).Count==1,"UTF8 complete record");Check(new List<string>(tail.Read(100)).Count==0,"empty append does not throw");
            File.AppendAllText(file,"{\"x\":");Check(new List<string>(tail.Read(100)).Count==0,"incomplete record held");File.AppendAllText(file,"1}\n");Check(new List<string>(tail.Read(100))[0]=="{\"x\":1}","split record reassembled");
            File.WriteAllText(file,"{}\n");Check(new List<string>(tail.Read(100)).Count==1,"truncated file recovers");
            File.AppendAllText(file,new string('x',1000)+"\n{\"ok\":true}\n");var lines=new List<string>(tail.Read(100));Check(lines.Count==1&&lines[0].Contains("ok"),"oversized output skipped within budget");
            string cfg=Path.Combine(temp,"config.json");File.WriteAllText(cfg,"{\"metricsSeconds\":0,\"weatherMinutes\":1,\"brightness\":1}");var c=Config.Read(cfg);Check(c.metricsSeconds==2&&c.weatherMinutes==15&&c.brightness==20,"configuration resource limits");
            Check(WeatherProvider.Code(0)=="晴"&&WeatherProvider.Code(95)=="雷雨","weather code mapping");
            var recovery=new RecoveryPlan();DateTime wake=DateTime.UtcNow;int stage;
            Check(recovery.Schedule(wake,"wake") && recovery.Take(wake,out stage) && stage==0,"wake schedules immediate recovery");
            Check(!recovery.Schedule(wake.AddSeconds(1),"duplicate") && !recovery.Take(wake.AddSeconds(1),out stage),"duplicate wake events coalesced");
            Check(recovery.Take(wake.AddSeconds(2),out stage)&&stage==1,"delayed surface recreation");
            Check(recovery.Take(wake.AddSeconds(40),out stage)&&stage==4&&!recovery.Pending,"retry sequence is bounded and skips missed stages");
            recovery.Schedule(wake.AddSeconds(50),"wake");recovery.Cancel();Check(!recovery.Take(wake.AddMinutes(1),out stage),"screen off cancels recovery");
            string logdir=Path.Combine(temp,"sessions",DateTime.Now.ToString("yyyy/MM/dd"));Directory.CreateDirectory(logdir);string log=Path.Combine(logdir,"rollout-00000000-0000-0000-0000-000000000001.jsonl");File.WriteAllText(log,Json.Encode(Event("task_started",DateTime.UtcNow.ToString("o")))+"\n");
            using(var cp=new CodexProvider(temp)) {Check(cp.Poll(DateTime.UtcNow).Count==1,"initial task discovery");File.AppendAllText(log,Json.Encode(Event("task_complete",DateTime.UtcNow.AddSeconds(1).ToString("o")))+"\n");Thread.Sleep(100);var tasks=cp.Poll(DateTime.UtcNow.AddSeconds(2));Check(tasks.Count==1&&tasks[0].State=="complete","watcher normalized paths and live completion");}
            string prior=Paths.Root;Paths.Root=temp;try {Json.Atomic(Path.Combine(temp,"external-task.json"),new {state="running",title="test",updatedUtc=DateTime.UtcNow.AddHours(-1).ToString("o"),ttlSeconds=30});Check(ExtensionProvider.Read(DateTime.UtcNow)==null,"expired extension ignored");Json.Atomic(Path.Combine(temp,"external-task.json"),new {state="waiting",title="test",updatedUtc=DateTime.UtcNow.ToString("o"),ttlSeconds=30});Check(ExtensionProvider.Read(DateTime.UtcNow).State=="waiting","valid extension accepted");} finally {Paths.Root=prior;}
            prior=Paths.Root;Paths.Root=temp;try {
                string snapshot=Path.Combine(temp,"dot-dialogue.json");var dialogue=new DotDialogueProvider();DateTime now=DateTime.UtcNow;
                Json.Atomic(snapshot,new {id="1",reply="真实回复",userMessage="你好",updatedUtc=now.ToString("o"),ttlSeconds=60});
                var first=dialogue.Read(now);Check(first!=null&&first.Reply=="真实回复"&&first.UserMessage=="你好","Dot visible message snapshot");
                Check(Object.ReferenceEquals(first,dialogue.Read(now.AddSeconds(1))),"unchanged Dot snapshot reused");
                Check(dialogue.Read(now.AddSeconds(61))==null,"expired Dot reply returns to standby");
                Json.Atomic(snapshot,new {id="2",reply="未来",updatedUtc=now.AddMinutes(2).ToString("o")});Check(dialogue.Read(now)==null,"future Dot reply ignored");
                File.WriteAllText(snapshot,"{broken");Check(dialogue.Read(now)==null,"malformed Dot snapshot ignored");
                File.WriteAllText(snapshot,new string('x',17000));Check(dialogue.Read(now)==null,"oversized Dot snapshot ignored");
                using(var bmp=new Bitmap(854,480)) using(var g=Graphics.FromImage(bmp)) using(var font=new Font("Microsoft YaHei UI",20,FontStyle.Regular,GraphicsUnit.Pixel)) {
                    string longReply=new string('测',500);var pages=DotDialogueProvider.Pages(g,longReply,font,new SizeF(492,132));Check(pages.Count>1&&String.Concat(pages)==longReply,"Dot pagination retains reply text");
                }
            } finally {Paths.Root=prior;}
            var primary=new ScreenTarget {DeviceName="main",Primary=true,Bounds=new Rectangle(0,0,1920,1080)};
            var generic=new ScreenTarget {DeviceName="lcd",Identity="stable-lcd",Bounds=new Rectangle(-1024,0,1024,600)};
            var branded=new ScreenTarget {DeviceName="dcast",FriendlyName="D-Cast Monitor",Bounds=new Rectangle(1920,0,1280,720)};
            var choose=new Config {displayDevice="lcd"};Check(ScreenCatalog.Select(choose,new[]{primary,generic})==generic,"manual selection supports arbitrary LCD sizes");
            choose.displayIdentity="stable-lcd";choose.displayDevice="old-number";Check(ScreenCatalog.Select(choose,new[]{primary,generic})==generic,"monitor identity survives display renumbering");
            choose.displayDevice="lcd";choose.displayIdentity="disconnected-lcd";Check(ScreenCatalog.Select(choose,new[]{primary,generic})==null,"missing saved monitor cannot take over a reused display number");
            choose.displayIdentity="";choose.displayDevice="main";Check(ScreenCatalog.Select(choose,new[]{primary,generic})==null,"primary monitor cannot be targeted");
            choose.displayDevice="auto";Check(ScreenCatalog.Select(choose,new[]{primary,generic,branded})==branded,"named D-Cast monitor auto detection");
            generic.AdapterName="JZFSDisplayDriver Device";Check(ScreenCatalog.Select(choose,new[]{primary,generic})==generic,"JZFS D-Cast adapter detected without resolution filter");generic.AdapterName="";
            Check(ScreenCatalog.Select(choose,new[]{branded,new ScreenTarget {DeviceName="another",FriendlyName="DeepCool LCD",Bounds=new Rectangle(0,0,480,854)}})==null,"multiple D-Cast monitors require explicit choice");
            Check(ScreenCatalog.Select(choose,new[]{primary,generic})==null,"unknown secondary display is not automatically taken over");
            Check(SceneLayout.Fit(new Size(480,854)).Mode=="portrait"&&SceneLayout.Fit(new Size(480,480)).Mode=="compact","portrait and square layouts selected");
            var fit=SceneLayout.Fit(new Size(1024,600));Check(fit.Mode=="landscape"&&fit.OffsetY>=0&&Math.Abs(fit.Canvas.Width*fit.Scale-1024)<0.1&&fit.Canvas.Height*fit.Scale<=600,"uniform scaling fits without stretching");
            Json.Atomic(report,new {success=true,tests=passed});return 0;
        } catch(Exception e) {Json.Atomic(report,new {success=false,tests=passed,error=e.ToString()});return 1;}
        finally {Directory.Delete(temp,true);}
    }
}
}
