namespace Tuna.Core.Hardware;

/// <summary>风扇曲线上的一个采样点:温度阈值(℃)→ 目标转速(RPM)。</summary>
public readonly record struct FanCurvePoint(int TempC, int Rpm);

/// <summary>
/// 某个性能模式下、某个风扇的完整曲线。对应联想 LENOVO_FAN_TABLE_DATA 的一条实例。
/// 温度断点与转速一一对应,只读快照(写入见 LenovoFanTable 编码,需实测验证)。
/// </summary>
public sealed class FanCurve
{
    public required int FanId { get; init; }
    public required int SensorId { get; init; }

    /// <summary>原始 Mode 字节:1安静 2均衡 3性能 0xE0(224)自定义 0xFF(255)当前快照。</summary>
    public required int ModeRaw { get; init; }

    /// <summary>映射到整机性能模式(1/2/3 直映,其余归为 Custom)。</summary>
    public required PowerMode Mode { get; init; }

    public required IReadOnlyList<FanCurvePoint> Points { get; init; }

    public int MinRpm { get; init; }
    public int MaxRpm { get; init; }
}
