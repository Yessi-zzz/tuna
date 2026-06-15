namespace Tuna.App.ViewModels;

/// <summary>
/// DataGrid 一行:一个参数在「安静/均衡/性能」三档下的配置值对比(只读)。
/// v1 不提供自定义档写入(EC 不采用 SetFeatureValue 的存储值),故仅做展示。
/// </summary>
public sealed class CapabilityComparisonRow
{
    public string DisplayName { get; }
    public string Unit { get; }
    public string Range { get; }
    public string Quiet { get; }
    public string Balanced { get; }
    public string Performance { get; }

    public CapabilityComparisonRow(
        string displayName, string unit, string range,
        int? quiet, int? balanced, int? performance)
    {
        DisplayName = displayName;
        Unit = unit;
        Range = range;
        Quiet = Fmt(quiet);
        Balanced = Fmt(balanced);
        Performance = Fmt(performance);
    }

    private static string Fmt(int? v) => v.HasValue ? v.Value.ToString() : "—";
}
