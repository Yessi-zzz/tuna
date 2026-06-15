# 生成 Tuna 应用图标(黑橙主题:深色圆角底 + 活力橙调谐表盘 + 指针)。
# 用内联 C# 绘制(PS 5.1 下类型解析最稳),多分辨率打包为 PNG 压缩的 .ico,并另存 256px PNG。
$ErrorActionPreference = 'Stop'

$cs = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;

public static class TunaIcon
{
    static Bitmap Make(int S)
    {
        var bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float f = S / 256f;
            float r = 46 * f, d = 2 * r;
            using (var path = new GraphicsPath())
            {
                path.AddArc(0, 0, d, d, 180, 90);
                path.AddArc(S - d, 0, d, d, 270, 90);
                path.AddArc(S - d, S - d, d, d, 0, 90);
                path.AddArc(0, S - d, d, d, 90, 90);
                path.CloseFigure();
                using (var bg = new LinearGradientBrush(new RectangleF(0, 0, S, S),
                    Color.FromArgb(255, 34, 33, 38), Color.FromArgb(255, 12, 12, 16), 90f))
                    g.FillPath(bg, path);
            }

            float cx = S * 0.5f, cy = S * 0.55f, rad = S * 0.30f, pw = S * 0.088f;
            var orange = Color.FromArgb(255, 251, 140, 40);
            using (var pen = new Pen(orange, pw) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawArc(pen, cx - rad, cy - rad, 2 * rad, 2 * rad, 135, 270);

            double ang = 305.0 * Math.PI / 180.0;
            float len = rad * 0.95f;
            float tipx = cx + (float)Math.Cos(ang) * len, tipy = cy + (float)Math.Sin(ang) * len;
            float bw = S * 0.045f;
            float px = (float)Math.Cos(ang + Math.PI / 2) * bw, py = (float)Math.Sin(ang + Math.PI / 2) * bw;
            var white = Color.FromArgb(255, 244, 244, 246);
            using (var nb = new SolidBrush(white))
            {
                g.FillPolygon(nb, new[] { new PointF(cx + px, cy + py), new PointF(cx - px, cy - py), new PointF(tipx, tipy) });
                float hr = S * 0.062f;
                g.FillEllipse(nb, cx - hr, cy - hr, 2 * hr, 2 * hr);
            }
            using (var ob = new SolidBrush(orange))
            {
                float hr2 = S * 0.030f;
                g.FillEllipse(ob, cx - hr2, cy - hr2, 2 * hr2, 2 * hr2);
            }
        }
        return bmp;
    }

    public static void Build(string assetsDir)
    {
        Directory.CreateDirectory(assetsDir);
        using (var big = Make(256))
            big.Save(Path.Combine(assetsDir, "Tuna-256.png"), ImageFormat.Png);

        int[] sizes = { 256, 64, 48, 32, 16 };
        var datas = new List<byte[]>();
        foreach (var s in sizes)
            using (var b = Make(s))
            using (var ms = new MemoryStream())
            { b.Save(ms, ImageFormat.Png); datas.Add(ms.ToArray()); }

        using (var fs = File.Create(Path.Combine(assetsDir, "Tuna.ico")))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)datas.Count);
            int offset = 6 + 16 * datas.Count;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32);
                w.Write((uint)datas[i].Length); w.Write((uint)offset);
                offset += datas[i].Length;
            }
            foreach (var dch in datas) w.Write(dch);
        }
    }
}
'@

Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Drawing -ErrorAction Stop

$assets = Join-Path $PSScriptRoot '..\src\Tuna.App\Assets'
if (-not (Test-Path $assets)) { New-Item -ItemType Directory -Path $assets -Force | Out-Null }
$assets = (Resolve-Path $assets).Path

[TunaIcon]::Build($assets)

$ico = Join-Path $assets 'Tuna.ico'
Write-Host "wrote: $ico  ($([Math]::Round((Get-Item $ico).Length/1kb,1)) KB)"
Write-Host "wrote: $(Join-Path $assets 'Tuna-256.png')"
