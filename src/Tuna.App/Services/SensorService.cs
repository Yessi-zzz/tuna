using LibreHardwareMonitor.Hardware;

namespace Tuna.App.Services;

/// <summary>一次采样的关键读数(单位:功率 W,温度 °C);null 表示未读到。</summary>
public readonly record struct SensorReadings(
    float? CpuPower, float? CpuTemp, float? GpuPower, float? GpuTemp);

/// <summary>用 LibreHardwareMonitor 读 CPU/GPU 实时功耗与温度。需管理员(加载内核驱动)。</summary>
public sealed class SensorService : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private bool _opened;

    public SensorService()
    {
        _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
        try { _computer.Open(); _opened = true; }
        catch { _opened = false; }
    }

    public SensorReadings Read()
    {
        if (!_opened) return default;

        try { _computer.Accept(_visitor); }
        catch { return default; }

        float? cpuPower = null, cpuTemp = null, gpuPower = null, gpuTemp = null;

        foreach (var hw in _computer.Hardware)
        {
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu:
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Power && Has(s, "Package"))
                            cpuPower ??= Positive(s.Value);
                        else if (s.SensorType == SensorType.Temperature &&
                                 (Has(s, "Tdie") || Has(s, "Tctl") || Has(s, "Package")))
                            cpuTemp ??= Positive(s.Value);
                    }
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    foreach (var s in hw.Sensors)
                    {
                        if (s.SensorType == SensorType.Power &&
                            (Has(s, "GPU Package") || Has(s, "GPU Power")))
                            gpuPower ??= Positive(s.Value);
                        else if (s.SensorType == SensorType.Temperature && Has(s, "GPU Core"))
                            gpuTemp ??= Positive(s.Value);
                    }
                    break;
            }
        }

        return new SensorReadings(cpuPower, cpuTemp, gpuPower, gpuTemp);
    }

    private static bool Has(ISensor s, string keyword)
        => s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    /// <summary>把 0/负值视为"未读到"(idle 下 CPU/GPU 功耗温度都不会真为 0)。</summary>
    private static float? Positive(float? v) => v is > 0 ? v : null;

    public void Dispose()
    {
        if (_opened) { try { _computer.Close(); } catch { } }
    }

    /// <summary>遍历器:递归 Update 各级硬件,否则子硬件(GPU)读数不刷新。</summary>
    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var sub in hardware.SubHardware) sub.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
