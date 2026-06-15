using Tuna.Core.Hardware;

namespace Tuna.Core.Abstractions;

/// <summary>
/// 与品牌无关的功耗控制抽象。每个品牌后端(Lenovo / Asus / …)实现一份。
/// </summary>
public interface IPowerController
{
    /// <summary>后端显示名,例如 "Lenovo Legion (WMI)"。</summary>
    string BackendName { get; }

    /// <summary>本机机型名(随电脑变化,用于界面显示),读不到时回退为后端名。</summary>
    string MachineName { get; }

    /// <summary>当前机器是否受此后端支持(用于自动选择后端)。</summary>
    bool IsSupported();

    PowerMode GetPowerMode();
    void SetPowerMode(PowerMode mode);

    /// <summary>
    /// 本机"自定义档值写入"是否会被 EC 实际采用。
    /// 走 SMU 旁路的平台(如 AMD 龙峰 8945HX)写了不生效 → false,界面据此隐藏滑块改显说明。
    /// </summary>
    bool SupportsEffectiveCustomTuning { get; }

    /// <summary>读取本机所有可调参数及其范围/当前值。</summary>
    IReadOnlyList<AdjustableCapability> GetCapabilities();

    int GetValue(CapabilityId id);

    /// <summary>写入参数值。实现必须先做范围/白名单校验。</summary>
    void SetValue(CapabilityId id, int value);

    /// <summary>读取各模式各风扇的曲线(温度→转速)。不支持时返回空表。</summary>
    IReadOnlyList<FanCurve> GetFanCurves();
}
