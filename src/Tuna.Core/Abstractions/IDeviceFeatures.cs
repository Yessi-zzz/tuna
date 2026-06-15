namespace Tuna.Core.Abstractions;

/// <summary>
/// 整机"设备"类开关(显卡模式 / 屏幕超频 / G-Sync 等),走独立于功耗能力表的 WMI 方法。
/// 由具体后端可选实现;App 端用 <c>controller as IDeviceFeatures</c> 探测是否可用。
/// 读取一律不抛(不支持返回安全默认);写入可抛由上层提示。
/// </summary>
public interface IDeviceFeatures
{
    /// <summary>屏幕超频(响应时间 Overdrive)是否可调。</summary>
    bool SupportsPanelOverdrive { get; }
    bool GetPanelOverdrive();
    void SetPanelOverdrive(bool on);

    /// <summary>显卡模式(独显直连 / 混合)是否可调。</summary>
    bool SupportsGpuDisplayMode { get; }
    /// <summary>原始状态值(本机 0=混合)。</summary>
    int GetGpuDisplayMode();
    /// <summary>切换显卡模式(需重启生效)。</summary>
    void SetGpuDisplayMode(int mode);

    /// <summary>G-Sync / Advanced Optimus 是否存在。</summary>
    bool SupportsGSync { get; }
    int GetGSync();

    /// <summary>禁用 Win 键(游戏防误触)。</summary>
    bool SupportsWinKeyToggle { get; }
    bool GetWinKeyDisabled();
    void SetWinKeyDisabled(bool disabled);

    /// <summary>触控板开关。</summary>
    bool SupportsTouchpadToggle { get; }
    bool GetTouchpadEnabled();
    void SetTouchpadEnabled(bool enabled);

    /// <summary>电池养护(限充约 80%,延长电池寿命)。走 OTHER_METHOD 充电类型 0x03010001,实测可读写。</summary>
    bool SupportsBatteryConservation { get; }
    bool GetBatteryConservation();
    void SetBatteryConservation(bool on);
}
