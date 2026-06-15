using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Tuna.App.Services;

/// <summary>监听交流/电池电源切换,用于"插拔电自动切档"。</summary>
public sealed class PowerSourceService : IDisposable
{
    /// <summary>电源状态变化时触发,参数 true=已接通交流电。</summary>
    public event Action<bool>? AcStatusChanged;

    public PowerSourceService() => SystemEvents.PowerModeChanged += OnPowerModeChanged;

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.StatusChange)
            AcStatusChanged?.Invoke(IsOnAc());
    }

    /// <summary>当前是否接通交流电(读不到时默认 true,避免误降档)。</summary>
    public bool IsOnAc()
        => !GetSystemPowerStatus(out var s) || s.ACLineStatus != 0;   // 1=AC, 0=电池, 255=未知

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    public void Dispose() => SystemEvents.PowerModeChanged -= OnPowerModeChanged;
}
