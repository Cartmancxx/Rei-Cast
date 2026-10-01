using System.Drawing;

namespace ReiCast {
// Original programmatic pixel mascot, MIT-licensed with the application.
public static class AvatarArt {
    public static Bitmap Create() {
        var image=new Bitmap(256,256);
        using(var g=Graphics.FromImage(image)) {
            g.Clear(Color.FromArgb(11,17,27));
            Pixel(g,Color.FromArgb(70,103,133),15,3,2,6);Pixel(g,Color.FromArgb(155,226,255),14,2,4,3);
            Pixel(g,Color.FromArgb(45,75,108),5,11,22,11);Pixel(g,Color.FromArgb(102,155,196),4,13,3,6);Pixel(g,Color.FromArgb(102,155,196),25,13,3,6);
            Pixel(g,Color.FromArgb(77,116,153),7,7,18,18);Pixel(g,Color.FromArgb(150,209,235),8,8,16,16);
            Pixel(g,Color.FromArgb(212,241,248),9,9,14,13);Pixel(g,Color.FromArgb(178,218,237),10,22,12,2);
            Pixel(g,Color.FromArgb(36,73,108),10,12,5,6);Pixel(g,Color.FromArgb(36,73,108),17,12,5,6);
            Pixel(g,Color.FromArgb(89,197,237),11,13,3,4);Pixel(g,Color.FromArgb(89,197,237),18,13,3,4);
            Pixel(g,Color.FromArgb(235,254,255),11,13,1,2);Pixel(g,Color.FromArgb(235,254,255),18,13,1,2);
            Pixel(g,Color.FromArgb(77,132,164),13,20,6,1);Pixel(g,Color.FromArgb(241,179,129),9,19,2,1);Pixel(g,Color.FromArgb(241,179,129),21,19,2,1);
            Pixel(g,Color.FromArgb(77,116,153),13,25,6,2);Pixel(g,Color.FromArgb(101,158,193),8,27,16,4);Pixel(g,Color.FromArgb(159,218,240),6,29,20,2);
            Pixel(g,Color.FromArgb(212,241,248),11,27,10,3);Pixel(g,Color.FromArgb(89,197,237),15,28,2,2);
        }
        return image;
    }
    static void Pixel(Graphics g,Color color,int x,int y,int w,int h) {using(var brush=new SolidBrush(color))g.FillRectangle(brush,x*8,y*8,w*8,h*8);}
}
}
