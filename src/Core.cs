using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace ReiCast {
public static class Json {
    public static Dictionary<string, object> Parse(string s) { return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Deserialize<Dictionary<string, object>>(s); }
    public static string Encode(object o) { return new JavaScriptSerializer().Serialize(o); }
    public static string Str(Dictionary<string, object> d, string k, string fallback = "") { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : fallback; }
    public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) ? v as Dictionary<string, object> : null; }
    public static double Num(Dictionary<string, object> d, string k, double fallback = 0) { double n; return double.TryParse(Str(d,k), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out n) ? n : fallback; }
    public static void Atomic(string path, object o) { string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; File.WriteAllText(tmp, Encode(o), new UTF8Encoding(false)); if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path); }
}
public class Config {
    public string city = "成都";
    public double latitude = 30.5728, longitude = 104.0668;
    public string displayDevice = "auto", accent = "#A4D7F6", avatarPath = "";
    public string displayIdentity = "", layout = "auto";
    public int metricsSeconds = 3, taskSeconds = 2, weatherMinutes = 30, brightness = 100;
    public bool showSeconds = true, showTaskTitles = true, enableGpu = true, enableWeather = true;
    public bool enableDotDialogue = true;
    public int dialoguePageSeconds = 8;
    public string codexHome = "";
    public static Config Read(string path) {
        Config c = new JavaScriptSerializer().Deserialize<Config>(File.ReadAllText(path, Encoding.UTF8)) ?? new Config();
        c.metricsSeconds = Math.Max(2, Math.Min(60,c.metricsSeconds)); c.taskSeconds = Math.Max(2,Math.Min(30,c.taskSeconds));
        c.weatherMinutes = Math.Max(15,Math.Min(240,c.weatherMinutes)); c.brightness = Math.Max(20,Math.Min(100,c.brightness));
        c.dialoguePageSeconds=Math.Max(5,Math.Min(30,c.dialoguePageSeconds));
        if(!new[]{"auto","landscape","portrait","compact"}.Contains(c.layout))c.layout="auto";
        if (double.IsNaN(c.latitude) || c.latitude < -90 || c.latitude > 90 || double.IsNaN(c.longitude) || c.longitude < -180 || c.longitude > 180) throw new Exception("天气坐标无效");
        return c;
    }
}
public static class Paths {
    // A shared physical location: packaged app tools can redirect LocalAppData,
    // while Windows logon processes see the real host filesystem.
    public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"ReiCast");
    public static string ConfigFile { get { return Path.Combine(Root,"config.json"); } }
    public static string Codex(Config c) { return !String.IsNullOrWhiteSpace(c.codexHome) ? c.codexHome : Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex"); }
    public static void Init() { Directory.CreateDirectory(Root); Directory.CreateDirectory(Path.Combine(Root,"events")); if(!File.Exists(ConfigFile)) Json.Atomic(ConfigFile,new Config()); }
    public static void Log(string s) { try { string p = Path.Combine(Root,"diagnostic.log"); if(File.Exists(p) && new FileInfo(p).Length > 128*1024) File.WriteAllText(p,""); File.AppendAllText(p,DateTime.Now.ToString("s")+" "+s+Environment.NewLine); } catch {} }
}
public class TaskInfo {
    public string Id = "", TurnId = "", Title = "Codex 任务", State = "idle", Phase = "待命", Model = "", Source = "local";
    public DateTime Updated = DateTime.MinValue, Started = DateTime.MinValue;
    public int Tools = 0;
    public TaskInfo Clone() { return (TaskInfo)MemberwiseClone(); }
}
public static class TaskReducer {
    public static bool Apply(TaskInfo t, Dictionary<string, object> e) {
        DateTime time; if(!DateTime.TryParse(Json.Str(e,"timestamp"),null,System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,out time)) return false;
        string type = Json.Str(e,"type"); var p = Json.Obj(e,"payload"); if(p == null) return false;
        if(type == "session_meta") { t.Id = Json.Str(p,"id",t.Id); return true; }
        if(type == "turn_context") { t.Model = Json.Str(p,"model",t.Model); return true; }
        if(time < t.Updated) return false;
        if(type == "event_msg") {
            string kind = Json.Str(p,"type");
            if(kind == "task_started") { t.State="running"; t.Phase="正在思考"; t.Started=time; t.Tools=0; t.TurnId=Json.Str(p,"turn_id"); }
            else if(kind == "task_complete" || kind == "task_completed" || kind == "turn_complete") { if(Json.Str(p,"turn_id",t.TurnId) != t.TurnId && t.TurnId!="") return false; t.State="complete"; t.Phase="任务完成"; }
            else if(kind == "turn_aborted" || kind == "task_aborted") { t.State="interrupted"; t.Phase="已暂停"; }
            else if(kind == "token_count" && t.State=="running") { }
            else return false;
        } else if(type == "response_item") {
            string kind = Json.Str(p,"type"), name=Json.Str(p,"name");
            if(kind == "function_call" || kind == "custom_tool_call") {
                t.State="running"; t.Tools++;
                t.Phase = name.Contains("request_user_input") ? "等待你的回复" : name.Contains("apply_patch") ? "正在修改文件" : name.Contains("web") ? "正在查找资料" : name.Contains("exec") ? "正在执行操作" : name.Contains("image") ? "正在生成图像" : "正在使用工具";
                if(name.Contains("request_user_input")) t.State="waiting";
            } else if(kind == "function_call_output" || kind == "custom_tool_call_output") { t.State="running"; t.Phase="正在处理结果"; }
            else if(kind == "message" && Json.Str(p,"role")=="assistant") {
                if(Json.Str(p,"channel")=="final") { t.State="complete"; t.Phase="任务完成"; }
                else if(Json.Str(p,"channel")=="commentary") { t.State="running"; t.Phase="正在推进任务"; }
                else return false;
            } else return false;
        } else return false;
        if(t.Started==DateTime.MinValue && t.State=="running") t.Started=time;
        t.Updated=time; return true;
    }
}
// A bounded tail reader: never loads a whole chat or retains tool output.
public class TailFile {
    public string Path; public long Offset; public bool Initialized; public string Pending=""; public bool Dropping;
    public TailFile(string path) { Path=path; }
    public IEnumerable<string> Read(int budget) {
        List<string> lines = new List<string>();
        using(var f = new FileStream(Path, FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete)) {
            if(!Initialized || f.Length<Offset) { Offset=Math.Max(0,f.Length-budget); Dropping=Offset>0; Pending=""; Initialized=true; }
            // Skip excessive output bursts. Final lifecycle records remain in the newest tail.
            if(f.Length-Offset>budget) { Offset=f.Length-budget; Pending=""; Dropping=true; }
            f.Position=Offset; byte[] b=new byte[(int)Math.Min(budget,f.Length-Offset)]; int n=f.Read(b,0,b.Length); Offset+=n;
            // Only decode complete UTF-8 lines; keep raw incomplete bytes for the next poll via Offset.
            if(n==0) return lines;
            int last=Array.LastIndexOf(b,(byte)10,n-1, n);
            if(last<0) { if(n<budget) Offset-=n; else Dropping=true; return lines; }
            Offset-=n-last-1;
            string s=Encoding.UTF8.GetString(b,0,last+1); string[] split=s.Split('\n');
            for(int i=0;i<split.Length-1;i++) { if(Dropping) { Dropping=false; continue; } if(split[i].Length<1024*1024) lines.Add(split[i]); }
        }
        return lines;
    }
}
public class CodexProvider : IDisposable {
    readonly string home; readonly Dictionary<string,TailFile> files = new Dictionary<string,TailFile>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,TaskInfo> tasks = new Dictionary<string,TaskInfo>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,string> titles=new Dictionary<string,string>();
    readonly System.Collections.Concurrent.ConcurrentDictionary<string,byte> changed = new System.Collections.Concurrent.ConcurrentDictionary<string,byte>();
    FileSystemWatcher watcher; DateTime nextScan=DateTime.MinValue, indexTime=DateTime.MinValue;
    public string Health = "本地事件";
    public CodexProvider(string root) { home=root; string sessions=System.IO.Path.Combine(home,"sessions"); if(Directory.Exists(sessions)) { watcher=new FileSystemWatcher(sessions,"*.jsonl"); watcher.IncludeSubdirectories=true; watcher.NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|NotifyFilters.Size; watcher.Changed+=(s,e)=>changed[e.FullPath]=0; watcher.Created+=(s,e)=>changed[e.FullPath]=0; watcher.Renamed+=(s,e)=>changed[e.FullPath]=0; watcher.Error+=(s,e)=>nextScan=DateTime.MinValue; watcher.EnableRaisingEvents=true; } else Health="未找到 Codex"; }
    void Track(string path) {
        path=System.IO.Path.GetFullPath(path);
        if(files.ContainsKey(path) || !File.Exists(path)) return;
        if(files.Count>=48) { string oldest=tasks.OrderBy(x=>x.Value.Updated).First().Key; files.Remove(oldest); tasks.Remove(oldest); }
        var t=new TaskInfo(); var name=System.IO.Path.GetFileNameWithoutExtension(path); if(name.Length>=36) t.Id=name.Substring(name.Length-36);
        // Session metadata can contain the system prompt. Read only the bounded first line, never persist it.
        try { using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) { byte[] b=new byte[Math.Min(256*1024,f.Length)]; int n=f.Read(b,0,b.Length); string prefix=Encoding.UTF8.GetString(b,0,n); foreach(string line in prefix.Split('\n')) { if(line.IndexOf("\"event_msg\"",StringComparison.Ordinal)<0) continue; try { TaskReducer.Apply(t,Json.Parse(line)); } catch {} } } } catch(IOException) {}
        files[path]=new TailFile(path); tasks[path]=t;
    }
    public List<TaskInfo> Poll(DateTime now) {
        if(now>=nextScan) {
            nextScan=now.AddSeconds(60);
            for(int d=0;d<3;d++) { string folder=System.IO.Path.Combine(home,"sessions",DateTime.Now.AddDays(-d).ToString("yyyy/MM/dd")); if(Directory.Exists(folder)) foreach(var p in Directory.EnumerateFiles(folder,"*.jsonl").OrderByDescending(File.GetLastWriteTimeUtc).Take(24)) Track(p); }
        }
        foreach(string p in changed.Keys.Take(96).ToArray()) { byte v; changed.TryRemove(p,out v); Track(p); }
        string index=System.IO.Path.Combine(home,"session_index.jsonl");
        if(File.Exists(index) && File.GetLastWriteTimeUtc(index)!=indexTime) {
            indexTime=File.GetLastWriteTimeUtc(index); var tail=new TailFile(index);
            foreach(string line in tail.Read(256*1024)) try { var x=Json.Parse(line); string id=Json.Str(x,"id"); if(id!="") titles[id]=Json.Str(x,"thread_name","Codex 任务"); } catch {}
        }
        int remaining=2*1024*1024;
        foreach(var entry in files.ToArray()) {
            if(remaining<=0) break; var f=entry.Value; var t=tasks[entry.Key];
            try {
                long before=f.Offset; var lines=f.Read(256*1024); remaining-=(int)Math.Min(256*1024,Math.Max(0,f.Offset-before));
                foreach(string line in lines) {
                    // Ignore reasoning and tool output before JSON deserialization.
                    if(line.IndexOf("\"event_msg\"",StringComparison.Ordinal)<0 && line.IndexOf("\"response_item\"",StringComparison.Ordinal)<0 && line.IndexOf("\"turn_context\"",StringComparison.Ordinal)<0) continue;
                    if(line.Length>256*1024) continue;
                    try { TaskReducer.Apply(t,Json.Parse(line)); } catch {}
                }
                string title; if(titles.TryGetValue(t.Id,out title)) t.Title=title;
            } catch(IOException) {} catch(UnauthorizedAccessException) { Health="部分事件不可读"; }
        }
        List<TaskInfo> result=tasks.Values.Select(t=>t.Clone()).ToList();
        foreach(var t in result) if((t.State=="running" || t.State=="waiting") && now-t.Updated>TimeSpan.FromMinutes(10)) { t.State="stale"; t.Phase="等待状态更新"; }
        return result;
    }
    public void Dispose() { if(watcher!=null) watcher.Dispose(); }
}
}
