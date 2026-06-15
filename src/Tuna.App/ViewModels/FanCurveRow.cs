using System.Windows;
using System.Windows.Media;
using Tuna.Core.Hardware;

namespace Tuna.App.ViewModels;

/// <summary>把一条 <see cref="FanCurve"/> 投影到固定画布(260×60)用于 Polyline 绘制。</summary>
public sealed class FanCurveRow
{
    // 与 XAML 里 Canvas 尺寸一致。
    private const double W = 260, H = 60, PadX = 4, PadY = 6;

    public string Name { get; }
    public string RangeLabel { get; }
    public string TempLabel { get; }
    public PointCollection Points { get; }

    public FanCurveRow(string name, FanCurve curve)
    {
        Name = name;
        RangeLabel = $"{curve.MinRpm}–{curve.MaxRpm} RPM";

        var pts = curve.Points;
        int tMin = pts.Min(p => p.TempC), tMax = pts.Max(p => p.TempC);
        TempLabel = $"{tMin}–{tMax} °C";
        if (tMax <= tMin) tMax = tMin + 1;

        double rMax = curve.MaxRpm > 0 ? curve.MaxRpm : pts.Max(p => p.Rpm);
        if (rMax <= 0) rMax = 1;

        var col = new PointCollection(pts.Count);
        foreach (var p in pts)
        {
            double x = PadX + (p.TempC - tMin) / (double)(tMax - tMin) * (W - 2 * PadX);
            double y = PadY + (1 - p.Rpm / rMax) * (H - 2 * PadY);
            col.Add(new Point(x, y));
        }
        Points = col;
    }
}
