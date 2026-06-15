using System.Collections;
using System.Management;
using Tuna.Backends.Lenovo.Wmi;
using Tuna.Core.Abstractions;
using Tuna.Core.Hardware;

namespace Tuna.Backends.Lenovo;

/// <summary>基于 Lenovo root\WMI 接口的功耗控制后端。</summary>
public sealed class LenovoWmiController : IPowerController, IDeviceFeatures
{
    private const string GameZone = "LENOVO_GAMEZONE_DATA";
    private const string OtherMethod = "LENOVO_OTHER_METHOD";
    private const string Capability01 = "LENOVO_CAPABILITY_DATA_01";
    private const string DiscreteData = "LENOVO_DISCRETE_DATA";
    private const string FanTableData = "LENOVO_FAN_TABLE_DATA";

    public string BackendName => "Lenovo Legion (WMI)";

    private string? _machineName;
    public string MachineName => _machineName ??= ReadMachineName();

    private static string ReadMachineName()
    {
        // Lenovo 营销机型名通常在 Win32_ComputerSystem.SystemFamily(如 "Legion R9000P 2025")。
        try
        {
            using var s = new ManagementObjectSearcher(@"\\.\root\cimv2",
                "SELECT SystemFamily, Manufacturer FROM Win32_ComputerSystem");
            foreach (ManagementObject mo in s.Get())
            {
                using (mo)
                {
                    var family = mo["SystemFamily"]?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(family))
                    {
                        var mfr = mo["Manufacturer"]?.ToString()?.Trim();
                        return string.IsNullOrWhiteSpace(mfr) || family!.Contains(mfr!, StringComparison.OrdinalIgnoreCase)
                            ? family!
                            : $"{mfr} {family}";
                    }
                }
            }
        }
        catch { /* 读不到则回退 */ }
        return "Lenovo Legion";
    }

    public bool IsSupported()
    {
        try { return WmiHelper.First(GameZone) is not null; }
        catch { return false; }
    }

    public PowerMode GetPowerMode()
        => (PowerMode)WmiHelper.Invoke(GameZone, "GetThermalMode");

    private bool? _customEffective;
    public bool SupportsEffectiveCustomTuning => _customEffective ??= DetectCustomEffective();

    /// <summary>
    /// 判定本机自定义档写入是否生效。已实测:AMD 龙峰(Dragon Range,HX 后缀的桌面衍生移动 CPU)
    /// 自定义功耗走 AMD SMU,WMI 写入不被 EC 采用(8945HX 实测 CPU+GPU 均不生效);
    /// Intel / 传统 AMD APU 上 WMI 自定义通常生效(LLT 在这些机型可调)。
    /// </summary>
    private static bool DetectCustomEffective()
    {
        try
        {
            using var s = new ManagementObjectSearcher(@"\\.\root\cimv2", "SELECT Name FROM Win32_Processor");
            foreach (ManagementObject mo in s.Get())
                using (mo)
                {
                    var name = (mo["Name"]?.ToString() ?? string.Empty).ToUpperInvariant();
                    var isAmd = name.Contains("AMD") || name.Contains("RYZEN");
                    // 龙峰移动 HX(如 7945HX / 8945HX / 7845HX)→ SMU 路,WMI 自定义不生效
                    if (isAmd && name.Contains("HX"))
                        return false;
                }
        }
        catch { /* 读不到 CPU 名则乐观假设可生效 */ }
        return true;
    }

    public void SetPowerMode(PowerMode mode)
        => WmiHelper.Invoke(GameZone, "SetSmartFanMode", new Dictionary<string, object> { ["Data"] = (uint)mode });

    // ---- IDeviceFeatures:独立于功耗能力表的设备开关(GameZone 方法) ----

    private static bool TryRead(string method, out uint value)
    {
        try { value = WmiHelper.Invoke(GameZone, method); return true; }
        catch { value = 0; return false; }
    }

    public bool SupportsPanelOverdrive => TryRead("IsSupportOD", out var v) && v != 0;
    public bool GetPanelOverdrive() => TryRead("GetODStatus", out var v) && v != 0;
    public void SetPanelOverdrive(bool on)
        => WmiHelper.Invoke(GameZone, "SetODStatus", new Dictionary<string, object> { ["Data"] = on ? 1u : 0u });

    public bool SupportsGpuDisplayMode => TryRead("IsSupportIGPUMode", out var v) && v != 0;
    public int GetGpuDisplayMode() => TryRead("GetIGPUModeStatus", out var v) ? (int)v : -1;
    public void SetGpuDisplayMode(int mode)
        => WmiHelper.Invoke(GameZone, "SetIGPUModeStatus", new Dictionary<string, object> { ["mode"] = (uint)mode });

    public bool SupportsGSync => TryRead("IsSupportGSync", out var v) && v != 0;
    public int GetGSync() => TryRead("GetGSyncStatus", out var v) ? (int)v : -1;

    public bool SupportsWinKeyToggle => TryRead("IsSupportDisableWinKey", out var v) && v != 0;
    public bool GetWinKeyDisabled() => TryRead("GetWinKeyStatus", out var v) && v != 0;
    public void SetWinKeyDisabled(bool disabled)
        => WmiHelper.Invoke(GameZone, "SetWinKeyStatus", new Dictionary<string, object> { ["Data"] = disabled ? 1u : 0u });

    public bool SupportsTouchpadToggle => TryRead("IsSupportDisableTP", out var v) && v != 0;
    public bool GetTouchpadEnabled() => TryRead("GetTPStatus", out var v) && v != 0;
    public void SetTouchpadEnabled(bool enabled)
        => WmiHelper.Invoke(GameZone, "SetTPStatus", new Dictionary<string, object> { ["Data"] = enabled ? 1u : 0u });

    // 电池养护(充电类型):0x03010001,0=标准满充 1=养护(约80%)。实测写入+回读均生效。
    private const uint ChargeTypeId = 0x03010001;
    private static uint GetFeatureRaw(uint id)
        => WmiHelper.Invoke(OtherMethod, "GetFeatureValue", new Dictionary<string, object> { ["IDs"] = id });

    public bool SupportsBatteryConservation
    {
        get { try { return GetFeatureRaw(ChargeTypeId) is 0 or 1; } catch { return false; } }
    }
    public bool GetBatteryConservation()
    {
        try { return GetFeatureRaw(ChargeTypeId) == 1; } catch { return false; }
    }
    public void SetBatteryConservation(bool on)
        => WmiHelper.Invoke(OtherMethod, "SetFeatureValue",
            new Dictionary<string, object> { ["IDs"] = ChargeTypeId, ["value"] = on ? 1u : 0u });

    public int GetValue(CapabilityId id)
        => (int)WmiHelper.Invoke(OtherMethod, "GetFeatureValue",
            new Dictionary<string, object> { ["IDs"] = id.Raw });

    public void SetValue(CapabilityId id, int value)
    {
        var cap = GetCapabilities().FirstOrDefault(c => c.Id == id)
            ?? throw new InvalidOperationException($"未知参数 {id}");
        if (!cap.IsValid(value))
            throw new ArgumentOutOfRangeException(nameof(value),
                $"值 {value} 超出 {cap.NameKey} 的允许范围");

        WmiHelper.Invoke(OtherMethod, "SetFeatureValue",
            new Dictionary<string, object> { ["IDs"] = id.Raw, ["value"] = (uint)value });
    }

    public IReadOnlyList<AdjustableCapability> GetCapabilities()
    {
        var discrete = LoadDiscreteWhitelist();
        var rows = ReadCapabilityRows();

        // 注意:本机 GetFeatureValue 对所有 ID 都返回 0,读不到实时当前值。
        // 真正有意义的"当前配置值"是每条记录的 DefaultValue(各模式各一份)。
        // 因此 Current 取本模式的配置值;Default 取性能档(03)的值,作为"恢复出厂"参考目标。
        var perfDefaults = rows
            .Where(r => r.Id.Mode == CapabilityMode.Performance)
            .GroupBy(r => r.Id.ParameterKey)
            .ToDictionary(g => g.Key, g => g.First().Value);

        var list = new List<AdjustableCapability>();
        var seen = new HashSet<uint>();
        foreach (var r in rows)
        {
            if (!seen.Add(r.Id.Raw))   // CAPABILITY_DATA_01 含重复记录,去重
                continue;

            discrete.TryGetValue(r.Id.ParameterKey, out var discreteValues);
            var (nameKey, unit, known) = LenovoCapabilityCatalog.Describe(r.Id);
            list.Add(new AdjustableCapability
            {
                Id = r.Id,
                NameKey = nameKey,
                Unit = unit,
                IsKnown = known,
                Min = r.Min,
                Max = r.Max,
                Step = Math.Max(1, r.Step),
                Default = perfDefaults.TryGetValue(r.Id.ParameterKey, out var pd) ? pd : r.Value,
                Current = r.Value,
                DiscreteValues = discreteValues,
            });
        }

        return list;
    }

    public IReadOnlyList<FanCurve> GetFanCurves()
    {
        var list = new List<FanCurve>();
        foreach (var mo in WmiHelper.Query(FanTableData))
        {
            using (mo)
            {
                var fan = ToIntArray(mo["FanTable_Data"]);
                var sensor = ToIntArray(mo["SensorTable_Data"]);
                var len = new[]
                {
                    Convert.ToInt32(mo["FanTable_Len"]), Convert.ToInt32(mo["SensorTable_Len"]),
                    fan.Length, sensor.Length,
                }.Min();
                if (len <= 0)
                    continue;

                var points = new List<FanCurvePoint>(len);
                for (var i = 0; i < len; i++)
                    points.Add(new FanCurvePoint(sensor[i], fan[i]));

                var modeRaw = Convert.ToInt32(mo["Mode"]);
                list.Add(new FanCurve
                {
                    FanId = Convert.ToInt32(mo["Fan_Id"]),
                    SensorId = Convert.ToInt32(mo["Sensor_ID"]),
                    ModeRaw = modeRaw,
                    Mode = MapFanMode(modeRaw),
                    Points = points,
                    MinRpm = Convert.ToInt32(mo["CurrentFanMinSpeed"]),
                    MaxRpm = Convert.ToInt32(mo["CurrentFanMaxSpeed"]),
                });
            }
        }
        return list;
    }

    private static PowerMode MapFanMode(int raw) => raw switch
    {
        1 => PowerMode.Quiet,
        2 => PowerMode.Balanced,
        3 => PowerMode.Performance,
        _ => PowerMode.Custom,   // 0xE0 自定义 / 0xFF 当前快照 等
    };

    private static int[] ToIntArray(object? value)
    {
        if (value is IEnumerable seq and not string)
        {
            var list = new List<int>();
            foreach (var item in seq)
                list.Add(Convert.ToInt32(item));
            return list.ToArray();
        }
        return Array.Empty<int>();
    }

    private readonly record struct CapRow(CapabilityId Id, int Min, int Max, int Step, int Value);

    private static List<CapRow> ReadCapabilityRows()
    {
        var rows = new List<CapRow>();
        foreach (var mo in WmiHelper.Query(Capability01))
        {
            using (mo)
            {
                rows.Add(new CapRow(
                    new CapabilityId(Convert.ToUInt32(mo["IDs"])),
                    Convert.ToInt32(mo["MinValue"]),
                    Convert.ToInt32(mo["MaxValue"]),
                    Convert.ToInt32(mo["Step"]),
                    Convert.ToInt32(mo["DefaultValue"])));
            }
        }
        return rows;
    }

    /// <summary>读取离散值白名单,按"参数键"(忽略模式字节)归并。</summary>
    private static Dictionary<uint, IReadOnlyList<int>> LoadDiscreteWhitelist()
    {
        var map = new Dictionary<uint, List<int>>();
        foreach (var mo in WmiHelper.Query(DiscreteData))
        {
            using (mo)
            {
                var raw = Convert.ToUInt32(mo["IDs"]);
                if (raw == 0)
                    continue;
                var key = raw & 0xFFFF0000u;
                var value = Convert.ToInt32(mo["Value"]);
                if (!map.TryGetValue(key, out var values))
                    map[key] = values = new List<int>();
                if (!values.Contains(value))
                    values.Add(value);
            }
        }
        return map.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<int>)kv.Value);
    }
}
