using Tuna.Core.Hardware;

namespace Tuna.Backends.Lenovo;

/// <summary>
/// 把 Lenovo 功能ID 映射成「本地化键」+ 单位(对照 FINDINGS.md 的编码规律)。
/// 返回语言无关的 NameKey,由 App 层 Localizer 翻译,后端不依赖本地化。
/// </summary>
internal static class LenovoCapabilityCatalog
{
    public static (string NameKey, string Unit, bool Known) Describe(CapabilityId id)
    {
        var match = (id.Category, id.Parameter) switch
        {
            (HardwareCategory.Cpu, 0x01) => ("Param.Cpu.Pl2", "W"),
            (HardwareCategory.Cpu, 0x02) => ("Param.Cpu.Pl1", "W"),
            (HardwareCategory.Cpu, 0x03) => ("Param.Cpu.Sppt", "W"),
            (HardwareCategory.Cpu, 0x04) => ("Param.Cpu.Temp", "°C"),
            (HardwareCategory.Cpu, 0x05) => ("Param.Cpu.Apu", "W"),
            (HardwareCategory.Cpu, 0x06) => ("Param.Cpu.CrossLoad", "W"),
            (HardwareCategory.Gpu, 0x01) => ("Param.Gpu.Boost", "W"),
            (HardwareCategory.Gpu, 0x02) => ("Param.Gpu.Ctgp", "W"),
            (HardwareCategory.Gpu, 0x03) => ("Param.Gpu.Temp", "°C"),
            (HardwareCategory.Gpu, 0x04) => ("Param.Gpu.Tpp", "W"),
            (HardwareCategory.Gpu, 0x05) => ("Param.Gpu.DynBoost", "W"),
            _ => (string.Empty, string.Empty),
        };

        var known = match.Item1.Length > 0;
        var nameKey = known ? match.Item1 : $"{id.Category} 0x{id.Parameter:X2}";
        return (nameKey, match.Item2, known);
    }
}
