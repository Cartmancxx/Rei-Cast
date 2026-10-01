using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ReiCast {
public class Metrics {
    public double Cpu=-1, Ram=-1, Gpu=-1, UsedGb, TotalGb, DownKbps, UpKbps;
    public DateTime Updated;
}
public sealed class MetricsProvider : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct Memory { public uint Length, Load; public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExtended; }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref Memory m);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] static extern uint PdhOpenQuery(string source,IntPtr user,out IntPtr query);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] static extern uint PdhAddEnglishCounter(IntPtr query,string path,IntPtr user,out IntPtr counter);
    [DllImport("pdh.dll")] static extern uint PdhCollectQueryData(IntPtr query);
    [DllImport("pdh.dll",CharSet=CharSet.Unicode)] static extern uint PdhGetFormattedCounterArray(IntPtr counter,uint format,ref uint size,out uint count,IntPtr items);
    [DllImport("pdh.dll")] static extern uint PdhCloseQuery(IntPtr query);
    [StructLayout(LayoutKind.Explicit,Size=16)] struct Value { [FieldOffset(0)] public uint Status; [FieldOffset(8)] public double Number; }
    [StructLayout(LayoutKind.Sequential)] struct Item { public IntPtr Name; public Value V; }
    long prevIdle,prevKernel,prevUser,prevRx,prevTx; DateTime prevTime; IntPtr query,counter;
    public MetricsProvider(bool gpu) {
        GetSystemTimes(out prevIdle,out prevKernel,out prevUser); prevTime=DateTime.UtcNow; Network(out prevRx,out prevTx);
        if(gpu) try { if(PdhOpenQuery(null,IntPtr.Zero,out query)==0 && PdhAddEnglishCounter(query,@"\GPU Engine(*)\Utilization Percentage",IntPtr.Zero,out counter)==0) PdhCollectQueryData(query); else Dispose(); } catch { Dispose(); }
    }
    static void Network(out long rx,out long tx) {
        rx=tx=0;
        foreach(var n in NetworkInterface.GetAllNetworkInterfaces()) if(n.OperationalStatus==OperationalStatus.Up && n.NetworkInterfaceType!=NetworkInterfaceType.Loopback && n.NetworkInterfaceType!=NetworkInterfaceType.Tunnel) try { var s=n.GetIPv4Statistics(); rx+=s.BytesReceived; tx+=s.BytesSent; } catch {}
    }
    public Metrics Sample() {
        var m=new Metrics(); m.Updated=DateTime.UtcNow;
        long idle,kernel,user; if(GetSystemTimes(out idle,out kernel,out user)) { long total=kernel-prevKernel+user-prevUser; if(total>0) m.Cpu=Math.Max(0,Math.Min(100,100.0*(total-(idle-prevIdle))/total)); prevIdle=idle;prevKernel=kernel;prevUser=user; }
        var mem=new Memory(); mem.Length=(uint)Marshal.SizeOf(typeof(Memory)); if(GlobalMemoryStatusEx(ref mem)) { m.Ram=100.0*(mem.TotalPhys-mem.AvailPhys)/mem.TotalPhys; m.UsedGb=(mem.TotalPhys-mem.AvailPhys)/1073741824.0; m.TotalGb=mem.TotalPhys/1073741824.0; }
        try { long rx,tx; Network(out rx,out tx); double elapsed=(m.Updated-prevTime).TotalSeconds; if(elapsed>0) { m.DownKbps=Math.Max(0,rx-prevRx)/elapsed/1024; m.UpKbps=Math.Max(0,tx-prevTx)/elapsed/1024; } prevRx=rx;prevTx=tx;prevTime=m.Updated; } catch {}
        if(query!=IntPtr.Zero) try {
            if(PdhCollectQueryData(query)==0) {
                uint size=0,count; uint rc=PdhGetFormattedCounterArray(counter,0x200,ref size,out count,IntPtr.Zero);
                if(size>0 && size<2*1024*1024) { IntPtr buf=Marshal.AllocHGlobal((int)size); try {
                    if(PdhGetFormattedCounterArray(counter,0x200,ref size,out count,buf)==0) {
                        var engines=new Dictionary<string,double>(); int stride=Marshal.SizeOf(typeof(Item));
                        for(int i=0;i<count;i++) { var item=(Item)Marshal.PtrToStructure(IntPtr.Add(buf,i*stride),typeof(Item)); if(item.V.Status>1 || double.IsNaN(item.V.Number)) continue; string name=Marshal.PtrToStringUni(item.Name); int at=name.IndexOf("_luid_",StringComparison.Ordinal); string key=at>=0?name.Substring(at):name; double old; engines.TryGetValue(key,out old); engines[key]=old+Math.Max(0,item.V.Number); }
                        if(engines.Count>0) m.Gpu=Math.Min(100,engines.Values.Max());
                    }
                } finally { Marshal.FreeHGlobal(buf); } }
            }
        } catch {}
        return m;
    }
    public void Dispose() { if(query!=IntPtr.Zero) { PdhCloseQuery(query); query=IntPtr.Zero; } }
}
public class Weather {
    public string City="成都", Summary="等待天气", Error=""; public double Temperature=double.NaN, FeelsLike=double.NaN;
    public DateTime Updated=DateTime.MinValue; public double Latitude,Longitude;
}
public sealed class WeatherProvider {
    public Weather Current = new Weather(); DateTime next=DateTime.MinValue; bool busy;
    public WeatherProvider() { try { string p=Path.Combine(Paths.Root,"weather-cache.json"); if(File.Exists(p)) Current=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Weather>(File.ReadAllText(p)); } catch {} }
    public async Task Refresh(Config c,bool force=false) {
        bool same=Current.City==c.city && Math.Abs(Current.Latitude-c.latitude)<0.001 && Math.Abs(Current.Longitude-c.longitude)<0.001;
        if(!c.enableWeather) { Current=new Weather { City=c.city, Summary="天气已关闭" }; return; }
        if(busy || (!force && same && DateTime.UtcNow<next)) return;
        busy=true; next=DateTime.UtcNow.AddMinutes(c.weatherMinutes);
        if(!same) Current=new Weather { City=c.city,Latitude=c.latitude,Longitude=c.longitude };
        try {
            ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
            string url="https://api.open-meteo.com/v1/forecast?latitude="+c.latitude.ToString(CultureInfo.InvariantCulture)+"&longitude="+c.longitude.ToString(CultureInfo.InvariantCulture)+"&current=temperature_2m,apparent_temperature,weather_code&timezone=Asia%2FShanghai&forecast_days=1";
            var req=(HttpWebRequest)WebRequest.Create(url); req.Timeout=8000; req.ReadWriteTimeout=8000; req.UserAgent="ReiCast/0.1 (personal desktop weather)";
            string body=await Task.Run(()=> { using(var res=req.GetResponse()) using(var reader=new StreamReader(res.GetResponseStream())) { char[] chars=new char[65536]; int n=reader.ReadBlock(chars,0,chars.Length); if(n>=chars.Length) throw new Exception("天气响应过大"); return new string(chars,0,n); } });
            var current=Json.Obj(Json.Parse(body),"current"); if(current==null) throw new Exception("天气返回格式变更");
            Current=new Weather { City=c.city, Latitude=c.latitude,Longitude=c.longitude,Temperature=Json.Num(current,"temperature_2m",double.NaN),FeelsLike=Json.Num(current,"apparent_temperature",double.NaN),Summary=Code((int)Json.Num(current,"weather_code",-1)),Updated=DateTime.UtcNow };
            if(double.IsNaN(Current.Temperature)) throw new Exception("暂无气温"); Json.Atomic(Path.Combine(Paths.Root,"weather-cache.json"),Current);
        } catch(Exception e) { Current.Error="天气暂不可用"; if(Current.Updated==DateTime.MinValue) Current.Summary="天气暂不可用"; next=DateTime.UtcNow.AddMinutes(15); Paths.Log("weather: "+e.GetType().Name); }
        finally { busy=false; }
    }
    public static string Code(int c) { if(c==0) return "晴"; if(c==1) return "晴间多云"; if(c==2) return "多云"; if(c==3) return "阴"; if(c==45||c==48) return "雾"; if(c>=51&&c<=57) return "毛毛雨"; if(c>=61&&c<=67) return "雨"; if(c>=71&&c<=77) return "雪"; if(c>=80&&c<=82) return "阵雨"; if(c==85||c==86) return "阵雪"; if(c>=95) return "雷雨"; return "天气未知"; }
}
// Optional extension interface: a local JSON snapshot, no executable plugins loaded in-process.
public static class ExtensionProvider {
    public static TaskInfo Read(DateTime now) {
        string p=Path.Combine(Paths.Root,"external-task.json"); if(!File.Exists(p)||new FileInfo(p).Length>8192) return null;
        try { var d=Json.Parse(File.ReadAllText(p)); DateTime stamp; if(!DateTime.TryParse(Json.Str(d,"updatedUtc"),null,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out stamp)) return null;
            int ttl=(int)Math.Max(5,Math.Min(3600,Json.Num(d,"ttlSeconds",60))); if(now-stamp>TimeSpan.FromSeconds(ttl)||stamp>now.AddSeconds(30)) return null;
            string state=Json.Str(d,"state"); if(!new[]{"running","waiting","complete","interrupted"}.Contains(state)) return null;
            return new TaskInfo { Id="external",Title=Json.Str(d,"title","外部任务"),State=state,Phase=Json.Str(d,"phase","处理中"),Updated=stamp,Started=stamp,Source="extension" };
        } catch { return null; }
    }
}
}
