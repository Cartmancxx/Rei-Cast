using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ReiCast {
public sealed class ScreenTarget {
    public string DeviceName="", Identity="", FriendlyName="", AdapterName="";
    public Rectangle Bounds;
    public bool Primary;
    public bool Dcast {get {string name=(FriendlyName+" "+AdapterName).ToLowerInvariant();return name.Contains("d-cast")||name.Contains("dcast")||name.Contains("d cast")||name.Contains("deepcool")||name.Contains("jzfsdisplaydriver");}}
    public override string ToString() {return DeviceName=="auto"?"自动识别 D-Cast":DeviceName+" · "+FriendlyName+(Bounds.Width>0?" · "+Bounds.Width+"×"+Bounds.Height:"");}
}
public static class ScreenCatalog {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Device {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string DeviceKey;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplayDevices(string device,uint index,ref Device info,uint flags);
    public static List<ScreenTarget> List() {
        var adapters=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for(uint i=0;i<64;i++) {var d=new Device {cb=Marshal.SizeOf(typeof(Device))};if(!EnumDisplayDevices(null,i,ref d,0))break;adapters[d.DeviceName]=d.DeviceString;}
        var result=new List<ScreenTarget>();
        foreach(var screen in Screen.AllScreens) {
            var monitor=new Device {cb=Marshal.SizeOf(typeof(Device))};bool known=EnumDisplayDevices(screen.DeviceName,0,ref monitor,1);string adapter;adapters.TryGetValue(screen.DeviceName,out adapter);
            result.Add(new ScreenTarget {DeviceName=screen.DeviceName,Identity=known?monitor.DeviceID:"",FriendlyName=known?monitor.DeviceString:"显示器",AdapterName=adapter??"",Bounds=screen.Bounds,Primary=screen.Primary});
        }
        return result;
    }
    public static ScreenTarget Select(Config config,IEnumerable<ScreenTarget> screens) {
        var secondary=screens.Where(s=>!s.Primary&&s.Bounds.Width>0&&s.Bounds.Height>0).ToList();
        if(config.displayDevice!="auto") {
            if(!String.IsNullOrWhiteSpace(config.displayIdentity)) return secondary.FirstOrDefault(s=>String.Equals(s.Identity,config.displayIdentity,StringComparison.OrdinalIgnoreCase));
            return secondary.FirstOrDefault(s=>String.Equals(s.DeviceName,config.displayDevice,StringComparison.OrdinalIgnoreCase));
        }
        var branded=secondary.Where(s=>s.Dcast).ToList();
        if(branded.Count==1)return branded[0];if(branded.Count>1)return null;
        // Backward-compatible fallback only; all other sizes can be selected explicitly.
        var legacy=secondary.Where(s=>s.Bounds.Size==new Size(854,480)||s.Bounds.Size==new Size(480,854)).ToList();
        return legacy.Count==1?legacy[0]:null;
    }
}
public sealed class SceneLayout {
    public string Mode;
    public Size Canvas;
    public float Scale,OffsetX,OffsetY;
    public static SceneLayout Fit(Size output,string requested="auto") {
        string mode=requested;
        if(mode=="auto"||!(new[]{"landscape","portrait","compact"}).Contains(mode)) mode=output.Width>output.Height*1.25?"landscape":output.Height>output.Width*1.25?"portrait":"compact";
        Size canvas=mode=="landscape"?new Size(854,480):mode=="portrait"?new Size(480,854):new Size(480,480);
        float width=Math.Max(1,output.Width),height=Math.Max(1,output.Height),scale=Math.Min(width/canvas.Width,height/canvas.Height);
        return new SceneLayout {Mode=mode,Canvas=canvas,Scale=scale,OffsetX=(width-canvas.Width*scale)/2,OffsetY=(height-canvas.Height*scale)/2};
    }
}
}
