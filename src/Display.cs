using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ReiCast {
public sealed class Display : Form {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
    [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)] static extern IntPtr RegisterPowerSettingNotification(IntPtr window,ref Guid setting,int flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnregisterPowerSettingNotification(IntPtr registration);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
    static readonly Guid DisplayPowerGuid=new Guid("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
    IntPtr powerRegistration;
    readonly RecoveryPlan recovery=new RecoveryPlan();
    System.Threading.EventWaitHandle repairSignal;
    int displayPower=-1,recoveryCount,paintCount;
    bool sessionLocked,rebuilding,live;
    DateTime lastUiTick,lastPaint,lastRecovery;
    string lastRecoveryReason="";
    public Config Settings; public Metrics Metrics=new Metrics(); public Weather Weather=new Weather(); public TaskInfo Active;
    public int ActiveCount; public string Health="本地事件"; public bool Preview;
    public DotDialogue Dialogue;
    readonly DotDialogueProvider dialogueProvider=new DotDialogueProvider();
    List<string> dialoguePages=new List<string>();string dialogueText="",dialogueLayout="";DateTime dialogueStart=DateTime.MinValue;
    readonly Timer timer=new Timer { Interval=1000 }; NotifyIcon tray; Bitmap avatar; readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
    CodexProvider codex; MetricsProvider metrics; WeatherProvider weather; DateTime configTime, nextMetrics, nextTasks, nextScreen, nextDiagnostic;
    bool polling,closing,manualHidden; string monitor="";
    readonly Color bg=Color.FromArgb(11,17,27),muted=Color.FromArgb(132,152,174),white=Color.FromArgb(230,239,249),line=Color.FromArgb(34,49,66);
    public Display(Config settings,bool preview,bool renderOnly=false) {
        Settings=settings; Preview=preview; Text="Rei Cast · 零"; BackColor=bg; ClientSize=new Size(854,480);
        FormBorderStyle=preview?FormBorderStyle.FixedSingle:FormBorderStyle.None; MaximizeBox=false; ShowInTaskbar=preview;
        DoubleBuffered=true; SetStyle(ControlStyles.ResizeRedraw,true); StartPosition=FormStartPosition.Manual;
        LoadAvatar();
        if(renderOnly) return;
        live=true;
        repairSignal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\ReiCast.Recover.1");
        Paths.Init(); codex=new CodexProvider(Paths.Codex(settings)); metrics=new MetricsProvider(settings.enableGpu); weather=new WeatherProvider();
        configTime=File.GetLastWriteTimeUtc(Paths.ConfigFile);
        var menu=new ContextMenuStrip(); menu.Items.Add("显示在小屏",null,(s,e)=>{manualHidden=false;Preview=false;Place();});
        menu.Items.Add("主屏预览",null,(s,e)=>{manualHidden=false;Preview=true;Place();});
        menu.Items.Add("隐藏画面",null,(s,e)=>{manualHidden=true;Hide();}); menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("修复小屏画面",null,(s,e)=>{manualHidden=false;RequestRecovery("manual");});
        menu.Items.Add("设置…",null,(s,e)=>OpenSettings());
        var startup=new ToolStripMenuItem("登录 Windows 后启动"); startup.Checked=Startup.Enabled(); startup.Click+=(s,e)=>{try {Startup.Set(!Startup.Enabled());startup.Checked=Startup.Enabled();}catch(Exception error){Paths.Log("startup setting: "+error);MessageBox.Show("登录自启动设置失败，请检查诊断日志。","Rei Cast");}}; menu.Items.Add(startup);
        menu.Items.Add("打开配置文件",null,(s,e)=>System.Diagnostics.Process.Start("notepad.exe",Paths.ConfigFile));
        menu.Items.Add(new ToolStripSeparator()); menu.Items.Add("退出 Rei Cast",null,(s,e)=>Close()); ContextMenuStrip=menu;
        tray=new NotifyIcon { Icon=SystemIcons.Information, Text="Rei Cast · 零",ContextMenuStrip=menu,Visible=true }; tray.DoubleClick+=(s,e)=>OpenSettings();
        KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape) {manualHidden=true;Hide();}};
        SystemEvents.DisplaySettingsChanged+=ScreensChanged;
        SystemEvents.PowerModeChanged+=PowerChanged;
        SystemEvents.SessionSwitch+=SessionChanged;
        Shown+=(s,e)=>{Place();timer.Start();Tick(null,EventArgs.Empty);}; timer.Tick+=Tick;
    }
    protected override bool ShowWithoutActivation { get { return !Preview; } }
    protected override CreateParams CreateParams { get { var p=base.CreateParams; if(!Preview) p.ExStyle|=0x08000000|0x80; return p; } }
    Color Accent { get { try { return ColorTranslator.FromHtml(Settings.accent); } catch { return Color.LightBlue; } } }
    Font F(float size,bool bold=false,string family="Microsoft YaHei UI") { string k=family+size+bold; Font f; if(!fonts.TryGetValue(k,out f)) { f=new Font(family,size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel); fonts[k]=f; } return f; }
    void LoadAvatar() {
        string p=Settings.avatarPath; if(String.IsNullOrWhiteSpace(p)) p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets","companion.png");
        try { using(var input=Image.FromFile(p)) { var next=new Bitmap(280,280); using(var g=Graphics.FromImage(next)) { g.InterpolationMode=InterpolationMode.NearestNeighbor; g.PixelOffsetMode=PixelOffsetMode.Half; g.DrawImage(input,0,0,280,280); } if(avatar!=null) avatar.Dispose(); avatar=next; } } catch { if(avatar!=null)avatar.Dispose();avatar=AvatarArt.Create();Paths.Log("avatar: using built-in pixel mascot"); }
    }
    void OnUi(Action action) { if(closing || !IsHandleCreated) return; try {BeginInvoke((Action)(()=>{if(!closing) action();}));} catch(InvalidOperationException) {} }
    void ScreensChanged(object s,EventArgs e) { OnUi(()=>{if(!rebuilding) RequestRecovery("display-change");}); }
    void PowerChanged(object s,PowerModeChangedEventArgs e) { OnUi(()=>{if(e.Mode==PowerModes.Resume) RequestRecovery("system-resume");else if(e.Mode==PowerModes.Suspend) recovery.Cancel();}); }
    void SessionChanged(object s,SessionSwitchEventArgs e) { OnUi(()=>{
        if(e.Reason==SessionSwitchReason.SessionLock) {sessionLocked=true;recovery.Cancel();}
        else if(e.Reason==SessionSwitchReason.SessionUnlock || e.Reason==SessionSwitchReason.ConsoleConnect || e.Reason==SessionSwitchReason.RemoteConnect) {sessionLocked=false;RequestRecovery("session-unlock");}
    }); }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        if(!live) return;
        Guid setting=DisplayPowerGuid;
        powerRegistration=RegisterPowerSettingNotification(Handle,ref setting,0);
        if(powerRegistration==IntPtr.Zero) Paths.Log("display-power subscription failed: "+System.Runtime.InteropServices.Marshal.GetLastWin32Error());
    }
    protected override void OnHandleDestroyed(EventArgs e) { if(powerRegistration!=IntPtr.Zero) {UnregisterPowerSettingNotification(powerRegistration);powerRegistration=IntPtr.Zero;} base.OnHandleDestroyed(e); }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x218 && m.WParam.ToInt32()==0x8013 && m.LParam!=IntPtr.Zero) {
            var guid=(Guid)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam,typeof(Guid));
            if(guid==DisplayPowerGuid && System.Runtime.InteropServices.Marshal.ReadInt32(m.LParam,16)==4) {
                int state=System.Runtime.InteropServices.Marshal.ReadInt32(m.LParam,20),previous=displayPower;
                if(state>=0 && state<=2) {
                    displayPower=state;
                    if(state==0) {recovery.Cancel();Paths.Log("display-power: off");}
                    // Registration also reports initial state. Do not rebuild recursively on that report.
                    else if(previous==0) {Paths.Log("display-power: on");RequestRecovery("display-on");}
                }
            }
        }
        base.WndProc(ref m);
    }
    void RequestRecovery(string reason) {
        if(closing || manualHidden || Preview) return;
        if(recovery.Schedule(DateTime.UtcNow,reason)) {lastRecoveryReason=reason;nextScreen=nextDiagnostic=DateTime.MinValue;Paths.Log("recovery requested: "+reason);}
    }
    void RecoveryTick(DateTime now) {
        if(manualHidden || Preview || sessionLocked || displayPower==0) return;
        int stage;if(!recovery.Take(now,out stage)) return;
        // Recreate our own HWND once per recovery cycle to replace a stale composition surface.
        // Never restart the graphics driver or prevent Windows from sleeping.
        if(stage==1) {try {rebuilding=true;BufferedGraphicsManager.Current.Invalidate();RecreateHandle();} finally {rebuilding=false;}}
        WindowState=FormWindowState.Normal;Place();
        if(Visible && monitor!="waiting") {
            SetWindowPos(Handle,new IntPtr(-1),Bounds.X,Bounds.Y,Bounds.Width,Bounds.Height,0x10|0x20|0x40);
            Invalidate(true);Update();
            recoveryCount++;lastRecovery=now;nextDiagnostic=DateTime.MinValue;
            Paths.Log("recovery repaint: stage="+stage+" display="+monitor);
        }
    }
    void Place() {
        if(manualHidden) return;
        if(Preview) { FormBorderStyle=FormBorderStyle.FixedSingle;TopMost=false;ShowInTaskbar=true;ClientSize=new Size(854,480); var b=Screen.PrimaryScreen.WorkingArea; Location=new Point(b.X+(b.Width-Width)/2,b.Y+(b.Height-Height)/2); if(!Visible) Show(); monitor="preview"; return; }
        ScreenTarget target=ScreenCatalog.Select(Settings,ScreenCatalog.List());
        if(target==null) { Hide();monitor="waiting";return; }
        FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;Bounds=target.Bounds;monitor=target.DeviceName;if(!Visible) Show();
    }
    async void Tick(object sender,EventArgs args) {
        if(closing) return;
        DateTime uiNow=DateTime.UtcNow;
        try {
            if(lastUiTick!=DateTime.MinValue && uiNow-lastUiTick>TimeSpan.FromSeconds(20)) RequestRecovery("timer-resumed");
            lastUiTick=uiNow;
            if(repairSignal!=null && repairSignal.WaitOne(0)) RequestRecovery("repair-command");
            RecoveryTick(uiNow);
            // Painting must remain responsive even if a performance counter or event read is slow.
            if(displayPower!=0 && !sessionLocked && Visible && WindowState!=FormWindowState.Minimized && (Settings.showSeconds || Active!=null || Dialogue!=null || uiNow>=nextMetrics || DateTime.Now.Second==0)) Invalidate();
        } catch(Exception e) {Paths.Log("display recovery: "+e.GetType().Name+" "+e.Message);}
        if(polling) return; polling=true;
        try {
            DateTime now=DateTime.UtcNow;
            if(now>=nextScreen) { nextScreen=now.AddSeconds(10); if(!Preview) Place();
                DateTime stamp=File.GetLastWriteTimeUtc(Paths.ConfigFile); if(stamp!=configTime) { configTime=stamp; try { var c=Config.Read(Paths.ConfigFile); bool gpu=c.enableGpu!=Settings.enableGpu; bool source=Paths.Codex(c)!=Paths.Codex(Settings); bool art=c.avatarPath!=Settings.avatarPath; Settings=c; if(gpu) {metrics.Dispose();metrics=new MetricsProvider(c.enableGpu);} if(source) {codex.Dispose();codex=new CodexProvider(Paths.Codex(c));} if(art) LoadAvatar(); nextMetrics=nextTasks=DateTime.MinValue; Place(); } catch { Paths.Log("config: invalid JSON; retaining previous settings"); } }
            }
            bool sample=now>=nextMetrics, tasks=now>=nextTasks;
            if(sample || tasks) {
                Metrics newMetrics=null; List<TaskInfo> list=null;DotDialogue newDialogue=null;
                await Task.Run(()=>{if(sample) newMetrics=metrics.Sample(); if(tasks) {list=codex.Poll(now);if(Settings.enableDotDialogue)newDialogue=dialogueProvider.Read(now);}});
                if(closing) return;
                if(newMetrics!=null) {Metrics=newMetrics;nextMetrics=now.AddSeconds(Settings.metricsSeconds);}
                if(list!=null) {
                    if(!Object.ReferenceEquals(Dialogue,newDialogue)) {Dialogue=newDialogue;dialoguePages.Clear();dialogueText="";dialogueStart=now;}
                    var extension=ExtensionProvider.Read(now); if(extension!=null) list.Add(extension);
                    var active=list.Where(t=>t.State=="running"||t.State=="waiting").OrderByDescending(t=>t.Updated).ToList();ActiveCount=active.Count;
                    Active=active.FirstOrDefault()??list.Where(t=>t.State=="complete"&&now-t.Updated<TimeSpan.FromSeconds(12)).OrderByDescending(t=>t.Updated).FirstOrDefault();
                    Health=codex.Health; if(Active==null && list.Any(t=>t.State=="stale")) Health="有任务等待状态更新";
                    nextTasks=now.AddSeconds(Settings.taskSeconds);
                }
            }
            var update=weather.Refresh(Settings); if(update.IsCompleted) await update; Weather=weather.Current;
            if(now>=nextDiagnostic) { nextDiagnostic=now.AddSeconds(10); try { Json.Atomic(Path.Combine(Paths.Root,"runtime.json"),new { timestamp=now.ToString("o"), version="0.2.0", processId=System.Diagnostics.Process.GetCurrentProcess().Id, display=monitor, visible=Visible, nativeVisible=IsWindowVisible(Handle), windowHandle=Handle.ToInt64(), displayPower=displayPower, powerNotifications=powerRegistration!=IntPtr.Zero, sessionLocked=sessionLocked, recoveryPending=recovery.Pending, recoveryCount=recoveryCount, recoveryReason=lastRecoveryReason, lastRecovery=lastRecovery.ToString("o"), paintCount=paintCount,lastPaint=lastPaint.ToString("o"), bounds=new {x=Bounds.X,y=Bounds.Y,width=Bounds.Width,height=Bounds.Height}, mode=Dialogue!=null?"dot":Active!=null?"task":"standby", dialogueUpdatedUtc=Dialogue==null?null:Dialogue.Updated.ToString("o"), dialogueSource=Dialogue==null?null:Dialogue.Demo?"demo":"dot", activeCount=ActiveCount, task=Active==null?"idle":Active.State, phase=Active==null?"待命":Active.Phase, cpu=Metrics.Cpu, ram=Metrics.Ram, gpu=Metrics.Gpu, weather=Weather.Summary, weatherUpdated=Weather.Updated.ToString("o"), health=Health }); } catch {} }
        } catch(Exception e) {Paths.Log("poll: "+e.GetType().Name+" "+e.Message);} finally {polling=false;}
    }
    void OpenSettings() { using(var form=new SettingsDialog(Settings)) if(form.ShowDialog()==DialogResult.OK) {Json.Atomic(Paths.ConfigFile,form.Result);nextScreen=DateTime.MinValue;} }
    void TextAt(Graphics g,string text,float x,float y,float width,float height,float size,Color color,bool bold=false,string family="Microsoft YaHei UI") {
        using(var b=new SolidBrush(color)) using(var format=new StringFormat { Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.LineLimit }) g.DrawString(text,F(size,bold,family),b,new RectangleF(x,y,width,height),format);
    }
    void Fill(Graphics g,Color c,float x,float y,float w,float h) {using(var b=new SolidBrush(c)) g.FillRectangle(b,x,y,w,h);}
    void Line(Graphics g,Color c,float x,float y,float x2,float y2) {using(var p=new Pen(c)) g.DrawLine(p,x,y,x2,y2);}
    void Metric(Graphics g,int x,string label,double value,string detail) {
        Fill(g,Color.FromArgb(17,27,41),x,354,150,77); TextAt(g,label,x+12,362,120,19,12,muted,true);
        TextAt(g,value<0?"--":Math.Round(value)+"%",x+10,379,125,37,28,white,true,"Consolas");
        Fill(g,line,x+12,421,126,3); if(value>=0) Fill(g,Accent,x+12,421,(float)(126*Math.Min(value,100)/100),3);
    }
    protected override void OnPaint(PaintEventArgs e) {base.OnPaint(e); PaintScene(e.Graphics,ClientSize);paintCount++;lastPaint=DateTime.UtcNow;}
    public void PaintScene(Graphics g,Size size) {
        var saved=g.Save();g.Clear(bg);var scene=SceneLayout.Fit(size,Settings.layout);g.TranslateTransform(scene.OffsetX,scene.OffsetY);g.ScaleTransform(scene.Scale,scene.Scale);g.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;g.SmoothingMode=SmoothingMode.None;
        if(scene.Mode=="landscape") PaintLandscape(g);else PaintVertical(g,scene.Mode=="portrait");
        if(Settings.brightness<100) Fill(g,Color.FromArgb((int)(255*(1-Settings.brightness/100.0)),0,0,0),0,0,scene.Canvas.Width,scene.Canvas.Height);
        g.Restore(saved);
    }
    void PaintLandscape(Graphics g) {
        Color accent=Accent; DateTime now=DateTime.Now; bool busy=Active!=null && Active.State!="complete";
        Fill(g,accent,24,27,5,17);TextAt(g,"REI / CAST",40,22,210,32,22,white,true);TextAt(g,"D-CAST COMPANION",331,28,275,22,12,muted,true,"Consolas");
        string badge=Dialogue!=null?"●  DOT":Active==null?"●  STANDBY":Active.State=="complete"?"●  COMPLETE":Active.State=="waiting"?"●  WAITING":"●  CONNECTED";
        TextAt(g,badge,682,28,157,24,12,Active!=null&&Active.State=="waiting"?Color.FromArgb(240,196,127):accent,true,"Consolas");
        Line(g,line,24,62,830,62);Line(g,line,307,88,307,429);
        if(avatar!=null) {g.InterpolationMode=InterpolationMode.NearestNeighbor;g.DrawImage(avatar,22,88,270,270);}
        else TextAt(g,"零",90,150,130,130,90,accent,true);
        // A small activity indicator changes only once per second.
        for(int i=0;i<7;i++) Fill(g,busy&&i==now.Second%7?accent:line,107+i*10,373,5,5);
        TextAt(g,Dialogue!=null?"听见你的声音了。":Active==null?"我在这里。":Active.State=="complete"?"完成了，休息一下吧。":"正在和你一起完成。",30,389,260,31,18,white);
        TextAt(g,"PIXEL  /  COMPANION",30,422,260,20,11,muted,false,"Consolas");
        if(Dialogue!=null) {
            TextAt(g,Dialogue.Demo?"DOT · 桥接测试":"DOT · 最新对话",331,80,380,28,20,accent,true);
            TextAt(g,now.ToString("HH:mm"),732,83,97,28,20,muted,false,"Consolas");
            TextAt(g,"你："+Dialogue.UserMessage,332,119,492,49,17,muted);
            Line(g,line,332,177,824,177);
            EnsureDialoguePages(g,"landscape",new SizeF(492,132),20);
            int page=dialoguePages.Count==0?0:(int)(Math.Max(0,(DateTime.UtcNow-dialogueStart).TotalSeconds)/Settings.dialoguePageSeconds)%dialoguePages.Count;
            if(dialoguePages.Count>0) TextAt(g,dialoguePages[page],332,188,492,132,20,white);
            TextAt(g,Dialogue.Updated.ToLocalTime().ToString("HH:mm")+" 收到"+(dialoguePages.Count>1?"  ·  "+(page+1)+" / "+dialoguePages.Count+" 页":""),332,327,492,19,12,muted);
        } else if(Active==null) {
            TextAt(g,now.ToString("HH:mm"),322,77,420,105,88,white,true,"Consolas");
            if(Settings.showSeconds) TextAt(g,now.ToString("ss"),739,130,78,45,32,accent,false,"Consolas");
            TextAt(g,now.ToString("MM月dd日  dddd",System.Globalization.CultureInfo.GetCultureInfo("zh-CN")),332,185,474,40,22,muted);
            Line(g,line,332,237,824,237);
            string temp=double.IsNaN(Weather.Temperature)?"--°":Math.Round(Weather.Temperature)+"°";
            TextAt(g,temp,329,257,140,71,52,accent,true,"Consolas");
            bool stale=Weather.Updated!=DateTime.MinValue && (DateTime.UtcNow-Weather.Updated>TimeSpan.FromMinutes(Settings.weatherMinutes*2)||Weather.Error!="");
            TextAt(g,Settings.city+" · "+Weather.Summary,490,260,330,35,22,white);
            string wx=Weather.Updated==DateTime.MinValue?"Open-Meteo · 等待更新":(stale?"缓存 · ":"体感 "+Math.Round(Weather.FeelsLike)+"° · ")+Weather.Updated.ToLocalTime().ToString("HH:mm")+" 更新";
            TextAt(g,wx,491,298,330,24,13,muted);
        } else {
            TextAt(g,Active.State=="complete"?"任务完成":Active.Phase,330,84,420,56,34,white,true);
            TextAt(g,now.ToString("HH:mm"),733,93,100,34,23,accent,false,"Consolas");
            TextAt(g,Settings.showTaskTitles?Active.Title:"Codex 正在处理任务",331,152,491,87,22,muted);
            Line(g,line,332,254,824,254);
            TimeSpan elapsed=DateTime.UtcNow-Active.Started; string duration=Active.Started==DateTime.MinValue?"--":elapsed.TotalHours>=1?((int)elapsed.TotalHours)+"h "+elapsed.Minutes+"m":Math.Max(0,(int)elapsed.TotalMinutes)+"m "+Math.Max(0,elapsed.Seconds)+"s";
            TextAt(g,"已运行",331,271,160,21,12,muted);TextAt(g,"工具操作",502,271,140,21,12,muted);TextAt(g,"活跃任务",673,271,148,21,12,muted);
            TextAt(g,duration,330,299,170,40,27,accent,true,"Consolas");TextAt(g,Active.Tools.ToString(),502,299,150,40,27,white,true,"Consolas");TextAt(g,ActiveCount.ToString("00"),673,299,150,40,27,white,true,"Consolas");
        }
        Metric(g,332,"CPU",Metrics.Cpu,"");Metric(g,503,"RAM",Metrics.Ram,"");Metric(g,674,"GPU",Metrics.Gpu,"");
        Line(g,line,24,448,830,448);
        TextAt(g,Dialogue!=null?(Active!=null?"Codex · "+Active.Phase:"DOT · 本地对话桥接"):Health,25,455,305,20,11,muted);TextAt(g,"RAM "+Metrics.UsedGb.ToString("0.0")+" / "+Metrics.TotalGb.ToString("0")+" GB",332,455,230,20,11,muted,false,"Consolas");
        TextAt(g,"↓ "+Metrics.DownKbps.ToString("0")+"  ↑ "+Metrics.UpKbps.ToString("0")+" KB/s",610,455,226,20,11,muted,false,"Consolas");
    }
    void EnsureDialoguePages(Graphics g,string layout,SizeF size,float fontSize) {
        if(dialogueText!=Dialogue.Reply||dialogueLayout!=layout) {dialogueText=Dialogue.Reply;dialogueLayout=layout;dialoguePages=DotDialogueProvider.Pages(g,Dialogue.Reply,F(fontSize),size);if(dialogueStart==DateTime.MinValue)dialogueStart=DateTime.UtcNow;}
    }
    void PaintVertical(Graphics g,bool portrait) {
        int height=portrait?854:480,body=portrait?340:202,metricsY=height-112;DateTime now=DateTime.Now;Color accent=Accent;
        Fill(g,accent,24,26,5,17);TextAt(g,"REI CAST",40,20,260,32,22,white,true);
        TextAt(g,Dialogue!=null?"● DOT":Active!=null?"● CODEX":"● IDLE",338,27,125,22,12,accent,true,"Consolas");Line(g,line,24,62,456,62);
        if(avatar!=null) {g.InterpolationMode=InterpolationMode.NearestNeighbor;g.DrawImage(avatar,portrait?128:24,portrait?80:76,portrait?224:116,portrait?224:116);}
        if(portrait) TextAt(g,now.ToString("HH:mm"),338,78,116,29,21,muted,false,"Consolas");
        if(Dialogue!=null) {
            TextAt(g,Dialogue.Demo?"DOT · 桥接测试":"DOT · 最新对话",portrait?24:164,portrait?body:80,portrait?432:292,29,22,accent,true);
            TextAt(g,"你："+Dialogue.UserMessage,portrait?24:164,portrait?body+38:119,portrait?432:292,portrait?63:65,17,muted);
            int replyY=portrait?body+110:body+17,replyHeight=metricsY-replyY-40;
            Line(g,line,24,replyY-12,456,replyY-12);EnsureDialoguePages(g,portrait?"portrait":"compact",new SizeF(432,replyHeight),portrait?23:18);
            int page=dialoguePages.Count==0?0:(int)(Math.Max(0,(DateTime.UtcNow-dialogueStart).TotalSeconds)/Settings.dialoguePageSeconds)%dialoguePages.Count;
            if(dialoguePages.Count>0)TextAt(g,dialoguePages[page],24,replyY,432,replyHeight,portrait?23:18,white);
            TextAt(g,Dialogue.Updated.ToLocalTime().ToString("HH:mm")+" 收到"+(dialoguePages.Count>1?" · "+(page+1)+" / "+dialoguePages.Count+" 页":""),24,metricsY-30,432,20,12,muted);
        } else if(Active==null) {
            TextAt(g,now.ToString("HH:mm"),portrait?44:154,portrait?body:77,portrait?402:310,portrait?104:74,portrait?88:60,white,true,"Consolas");
            TextAt(g,now.ToString("MM月dd日  dddd",System.Globalization.CultureInfo.GetCultureInfo("zh-CN")),portrait?30:164,portrait?body+110:154,portrait?426:292,34,portrait?23:17,muted);
            int weatherY=portrait?body+197:body+24;Line(g,line,24,weatherY-17,456,weatherY-17);
            string temp=double.IsNaN(Weather.Temperature)?"--°":Math.Round(Weather.Temperature)+"°";
            TextAt(g,temp,24,weatherY,150,76,54,accent,true,"Consolas");TextAt(g,Settings.city+" · "+Weather.Summary,185,weatherY+4,271,35,portrait?24:21,white);
            TextAt(g,Weather.Updated==DateTime.MinValue?"等待天气更新":Weather.Updated.ToLocalTime().ToString("HH:mm")+" 更新",185,weatherY+43,271,26,15,muted);
            if(portrait)TextAt(g,"我在这里。",24,metricsY-52,432,32,22,muted);
        } else {
            TextAt(g,Active.Phase,portrait?24:164,portrait?body:78,portrait?432:292,58,portrait?34:25,white,true);
            TextAt(g,Settings.showTaskTitles?Active.Title:"Codex 正在处理任务",portrait?24:164,portrait?body+74:135,portrait?432:292,portrait?116:58,portrait?24:17,muted);
            TimeSpan elapsed=DateTime.UtcNow-Active.Started;
            TextAt(g,"已运行 "+(Active.Started==DateTime.MinValue?"--":Math.Max(0,(int)elapsed.TotalMinutes)+"m")+"  ·  工具 "+Active.Tools+"  ·  活跃 "+ActiveCount,24,portrait?body+238:body+77,432,49,portrait?22:18,accent);
            TextAt(g,now.ToString("HH:mm"),24,metricsY-43,432,29,20,muted,false,"Consolas");
        }
        VerticalMetric(g,24,metricsY,"CPU",Metrics.Cpu);VerticalMetric(g,172,metricsY,"RAM",Metrics.Ram);VerticalMetric(g,320,metricsY,"GPU",Metrics.Gpu);
        Line(g,line,24,height-34,456,height-34);TextAt(g,Dialogue!=null?(Active!=null?"Codex · "+Active.Phase:"DOT · 本地桥接"):Health,24,height-26,278,19,11,muted);TextAt(g,Metrics.UsedGb.ToString("0.0")+" / "+Metrics.TotalGb.ToString("0")+" GB",322,height-26,140,19,11,muted,false,"Consolas");
    }
    void VerticalMetric(Graphics g,int x,int y,string label,double value) {
        Fill(g,Color.FromArgb(17,27,41),x,y,136,65);TextAt(g,label,x+10,y+5,116,19,12,muted,true);TextAt(g,value<0?"--":Math.Round(value)+"%",x+9,y+22,117,35,26,white,true,"Consolas");
        Fill(g,line,x+10,y+59,116,3);if(value>=0)Fill(g,Accent,x+10,y+59,(float)(116*Math.Min(100,value)/100),3);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { closing=true;timer.Stop();SystemEvents.DisplaySettingsChanged-=ScreensChanged;SystemEvents.PowerModeChanged-=PowerChanged;SystemEvents.SessionSwitch-=SessionChanged;if(repairSignal!=null) repairSignal.Dispose();if(tray!=null) {tray.Visible=false;tray.Dispose();} if(codex!=null) codex.Dispose();if(metrics!=null && !polling) metrics.Dispose();if(avatar!=null) avatar.Dispose();foreach(var f in fonts.Values) f.Dispose();base.OnFormClosed(e); }
}
public static class Startup {
    const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool Enabled() { using(var k=Registry.CurrentUser.OpenSubKey(Key)) return k!=null&&k.GetValue("ReiCast")!=null; }
    public static void Set(bool enabled) {
        string helper=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"scripts","startup.ps1");
        if(!File.Exists(helper)) throw new FileNotFoundException("Startup task helper is missing",helper);
        var psi=new System.Diagnostics.ProcessStartInfo {FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32\\WindowsPowerShell\\v1.0\\powershell.exe"),Arguments="-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+helper+"\" -Action "+(enabled?"Enable":"Disable"),UseShellExecute=false,CreateNoWindow=true,WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden};
        using(var process=System.Diagnostics.Process.Start(psi)) {if(!process.WaitForExit(15000)) {process.Kill();throw new TimeoutException("Timed out configuring the logon task.");}if(process.ExitCode!=0) throw new Exception("Task Scheduler rejected Rei Cast's logon task (exit "+process.ExitCode+").");}
        Paths.Log("login startup "+(enabled?"enabled (run key and 30-second delayed task)":"disabled"));
    }
}
public class SettingsDialog : Form {
    public Config Result; TextBox city,lat,lon; NumericUpDown brightness; CheckBox seconds,titles,gpu; ComboBox screen;
    public SettingsDialog(Config c) {
        Result=c;Text="Rei Cast 设置";ClientSize=new Size(430,415);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);
        LabelAt("天气城市",24,22);city=Box(c.city,148,20);LabelAt("纬度 / 经度",24,64);lat=Box(c.latitude.ToString(System.Globalization.CultureInfo.InvariantCulture),148,62,117);lon=Box(c.longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),275,62,127);
        LabelAt("目标屏幕",24,107);screen=new ComboBox {Left=148,Top=103,Width=254,DropDownWidth=580,DropDownStyle=ComboBoxStyle.DropDownList};var choices=ScreenCatalog.List().Where(s=>!s.Primary).ToList();var automatic=new ScreenTarget {DeviceName="auto"};screen.Items.Add(automatic);foreach(var target in choices)screen.Items.Add(target);
        var selected=c.displayDevice=="auto"?automatic:choices.FirstOrDefault(s=>c.displayIdentity!=""&&s.Identity==c.displayIdentity)??choices.FirstOrDefault(s=>s.DeviceName==c.displayDevice);
        if(selected==null) {selected=new ScreenTarget {DeviceName=c.displayDevice,Identity=c.displayIdentity,FriendlyName="当前未连接"};screen.Items.Add(selected);}screen.SelectedItem=selected;Controls.Add(screen);
        LabelAt("画面亮度",24,149);brightness=new NumericUpDown {Left=148,Top=145,Width=100,Minimum=20,Maximum=100,Value=c.brightness};Controls.Add(brightness);
        seconds=Check("显示秒数",c.showSeconds,195);titles=Check("显示任务名称",c.showTaskTitles,232);gpu=Check("读取 GPU 使用率",c.enableGpu,269);
        var note=new Label {Left=24,Top=310,Width=382,Height=34,Text="修改城市时请同时填写坐标。成都：30.5728, 104.0668",Font=new Font(Font.FontFamily,8)};Controls.Add(note);
        var save=new Button {Text="保存",Left=292,Top=361,Width=110,Height=33};save.Click+=(s,e)=>Save();Controls.Add(save);AcceptButton=save;
    }
    void LabelAt(string s,int x,int y) {Controls.Add(new Label {Text=s,Left=x,Top=y,Width=124,Height=28});}
    TextBox Box(string s,int x,int y,int w=254) {var t=new TextBox {Text=s,Left=x,Top=y,Width=w};Controls.Add(t);return t;}
    CheckBox Check(string s,bool b,int y) {var c=new CheckBox {Text=s,Checked=b,Left=148,Top=y,Width=252,Height=27};Controls.Add(c);return c;}
    void Save() {
        double la,lo;if(!double.TryParse(lat.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out la)||!double.TryParse(lon.Text,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out lo)||Double.IsNaN(la)||Double.IsNaN(lo)||la< -90||la>90||lo< -180||lo>180) {MessageBox.Show("请填写有效的纬度和经度。");return;}
        var target=(ScreenTarget)screen.SelectedItem;var c=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Config>(Json.Encode(Result));c.city=city.Text.Trim();c.latitude=la;c.longitude=lo;c.displayDevice=target.DeviceName;c.displayIdentity=target.Identity;c.brightness=(int)brightness.Value;c.showSeconds=seconds.Checked;c.showTaskTitles=titles.Checked;c.enableGpu=gpu.Checked;Result=c;DialogResult=DialogResult.OK;Close();
    }
}
}
