$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
public static class HyroLogo {
    static GraphicsPath Letters(string text) {
        GraphicsPath path = new GraphicsPath();
        using (FontFamily font = new FontFamily("Segoe UI"))
            path.AddString(text, font, (int)FontStyle.Bold, 240, PointF.Empty, StringFormat.GenericTypographic);
        return path;
    }
    static GraphicsPath Wordmark() {
        using (GraphicsPath hy = Letters("HY"))
        using (GraphicsPath ro = Letters("RO")) {
            RectangleF a = hy.GetBounds(), b = ro.GetBounds();
            float scale = Math.Min(344 / Math.Max(a.Width,b.Width), 174 / Math.Max(a.Height,b.Height));
            GraphicsPath result = new GraphicsPath();
            GraphicsPath[] rows = {hy,ro};
            for (int i=0;i<2;i++) {
                RectangleF bounds = rows[i].GetBounds();
                using (Matrix transform = new Matrix(scale,0,0,scale,(512-bounds.Width*scale)/2-bounds.X*scale,72+i*194-bounds.Y*scale)) rows[i].Transform(transform);
                result.AddPath(rows[i],false);
            }
            return result;
        }
    }
    static GraphicsPath Tile() {
        GraphicsPath path = new GraphicsPath();
        path.AddArc(12,12,160,160,180,90); path.AddArc(340,12,160,160,270,90);
        path.AddArc(340,340,160,160,0,90); path.AddArc(12,340,160,160,90,90); path.CloseFigure();
        return path;
    }
    static byte[] Render(int size) {
        using (Bitmap image = new Bitmap(size,size,PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(image))
        using (GraphicsPath tile=Tile())
        using (GraphicsPath text=Wordmark())
        using (SolidBrush back=new SolidBrush(Color.FromArgb(16,23,32)))
        using (Pen line=new Pen(Color.FromArgb(42,59,74),3))
        using (LinearGradientBrush ink=new LinearGradientBrush(new Point(0,72),new Point(0,440),Color.FromArgb(93,255,237),Color.FromArgb(0,206,207))) {
            g.Clear(Color.Transparent); g.SmoothingMode=SmoothingMode.AntiAlias;
            g.ScaleTransform(size/512f,size/512f); g.FillPath(back,tile); g.DrawPath(line,tile); g.FillPath(ink,text);
            using (MemoryStream stream=new MemoryStream()) { image.Save(stream,ImageFormat.Png); return stream.ToArray(); }
        }
    }
    static string SvgPath(GraphicsPath path) {
        StringBuilder s=new StringBuilder(); PointF[] points=path.PathPoints; byte[] types=path.PathTypes;
        Func<float,string> n = v => v.ToString("0.###",CultureInfo.InvariantCulture);
        for(int i=0;i<points.Length;i++) {
            int kind=types[i]&7;
            if(kind==0 || kind==1) s.Append(kind==0?"M":"L").Append(n(points[i].X)).Append(' ').Append(n(points[i].Y));
            else if(kind==3) { s.Append("C"); for(int j=0;j<3;j++,i++) s.Append(n(points[i].X)).Append(' ').Append(n(points[i].Y)).Append(' '); i--; }
            if((types[i]&128)!=0) s.Append('Z');
        }
        return s.ToString();
    }
    public static void Save(string folder) {
        File.WriteAllBytes(Path.Combine(folder,"default-logo.png"),Render(512));
        int[] sizes={16,24,32,48,64,128,256}; List<byte[]> frames=new List<byte[]>();
        foreach(int size in sizes) frames.Add(Render(size));
        using(BinaryWriter writer=new BinaryWriter(File.Create(Path.Combine(folder,"app.ico")))) {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) { writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length; }
            foreach(byte[] frame in frames) writer.Write(frame);
        }
        using(GraphicsPath wordmark=Wordmark()) {
            string svg="<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 512 512\" role=\"img\" aria-label=\"HYRO\"><defs><linearGradient id=\"ink\" x1=\"0\" y1=\"72\" x2=\"0\" y2=\"440\" gradientUnits=\"userSpaceOnUse\"><stop stop-color=\"#5dffed\"/><stop offset=\"1\" stop-color=\"#00cecf\"/></linearGradient></defs><rect x=\"12\" y=\"12\" width=\"488\" height=\"488\" rx=\"80\" fill=\"#101720\" stroke=\"#2a3b4a\" stroke-width=\"3\"/><path fill=\"url(#ink)\" d=\""+SvgPath(wordmark)+"\"/></svg>";
            File.WriteAllText(Path.Combine(folder,"hyro-logo.svg"),svg,Encoding.UTF8);
        }
    }
}
'@
$assetFolder = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets'
[HyroLogo]::Save($assetFolder)
foreach ($size in @(16,32,48,256)) {
  $icon = New-Object System.Drawing.Icon((Join-Path $assetFolder 'app.ico'), $size, $size)
  $icon.Dispose()
}
Write-Output 'Generated HY/RO logo PNG, outlined SVG, and 7-resolution ICO. ICO loads at 16/32/48/256.'
