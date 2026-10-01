using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Globalization;

namespace ReiCast {
public sealed class DotDialogue {
    public string Id="", UserMessage="", Reply="";
    public DateTime Updated;
    public int TtlSeconds=300;
    public bool Demo;
}
public sealed class DotDialogueProvider {
    DateTime fileTime=DateTime.MinValue;
    long fileSize=-1;
    DotDialogue cached;
    public DotDialogue Read(DateTime now) {
        try {
            string path=Path.Combine(Paths.Root,"dot-dialogue.json");
            var info=new FileInfo(path);
            if(!info.Exists || info.Length>16384) {cached=null;fileTime=DateTime.MinValue;fileSize=-1;return null;}
            if(info.LastWriteTimeUtc!=fileTime || info.Length!=fileSize) {
                fileTime=info.LastWriteTimeUtc;fileSize=info.Length;cached=null;
                string text;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                    if(stream.Length>16384) return null;
                    using(var reader=new StreamReader(stream,Encoding.UTF8)) {char[] buffer=new char[16385];int count=reader.ReadBlock(buffer,0,buffer.Length);if(count>16384)return null;text=new string(buffer,0,count);}
                }
                var d=Json.Parse(text);DateTime stamp;
                if(!DateTime.TryParse(Json.Str(d,"updatedUtc"),null,DateTimeStyles.AdjustToUniversal|DateTimeStyles.AssumeUniversal,out stamp)) return null;
                string reply=Clean(Json.Str(d,"reply"),4000);if(String.IsNullOrWhiteSpace(reply)) return null;
                cached=new DotDialogue {Id=Json.Str(d,"id"),UserMessage=Clean(Json.Str(d,"userMessage"),600),Reply=reply,Updated=stamp,TtlSeconds=(int)Math.Max(10,Math.Min(1800,Json.Num(d,"ttlSeconds",300))),Demo=Json.Str(d,"source")=="demo"};
            }
            return cached!=null && cached.Updated<=now.AddSeconds(30) && now-cached.Updated<=TimeSpan.FromSeconds(cached.TtlSeconds)?cached:null;
        } catch {cached=null;return null;}
    }
    static string Clean(string text,int limit) {
        var b=new StringBuilder();foreach(char c in text.Replace("\r\n","\n")) if(!Char.IsControl(c)||c=='\n'||c=='\t') b.Append(c);
        string result=b.ToString();if(result.Length>limit) {int end=limit;if(Char.IsHighSurrogate(result[end-1]))end--;result=result.Substring(0,end)+"…";}return result;
    }
    public static List<string> Pages(Graphics g,string text,Font font,SizeF size) {
        var pages=new List<string>();
        using(var format=new StringFormat {FormatFlags=StringFormatFlags.LineLimit}) {
            while(text.Length>0 && pages.Count<64) {
                int count,lines;g.MeasureString(text,font,size,format,out count,out lines);
                count=Math.Min(text.Length,Math.Max(1,count));if(count<text.Length&&count>1&&Char.IsHighSurrogate(text[count-1]))count--;
                pages.Add(text.Substring(0,count).TrimEnd());text=text.Substring(count).TrimStart('\n','\r');
            }
        }
        return pages;
    }
}
}
