namespace Tuna.Core.Hardware;

/// <summary>硬件类别(功能ID 最高字节)。</summary>
public enum HardwareCategory : byte
{
    System = 0x00,
    Cpu = 0x01,
    Gpu = 0x02,
    Display = 0x03,
    Battery = 0x04,
    Fan = 0x05,
    Keyboard = 0x06,
    Other = 0x08,
}

/// <summary>参数所属的性能模式(功能ID 第 1 字节)。</summary>
public enum CapabilityMode : byte
{
    None = 0x00,
    Quiet = 0x01,
    Balanced = 0x02,
    Performance = 0x03,
    Custom = 0xE0,
    Current = 0xFF,
}

/// <summary>
/// Lenovo 功能ID,结构 0x[类别][参数][模式][00]。
/// 例:0x0102E000 = CPU(01) 长时功率(02) 自定义档(E0)。
/// </summary>
public readonly record struct CapabilityId(uint Raw)
{
    public HardwareCategory Category => (HardwareCategory)((Raw >> 24) & 0xFF);
    public byte Parameter => (byte)((Raw >> 16) & 0xFF);
    public CapabilityMode Mode => (CapabilityMode)((Raw >> 8) & 0xFF);

    /// <summary>忽略模式字节的"参数键",用于跨模式匹配(如离散值白名单)。</summary>
    public uint ParameterKey => Raw & 0xFFFF0000u;

    public static CapabilityId From(HardwareCategory category, byte parameter, CapabilityMode mode)
        => new(((uint)category << 24) | ((uint)parameter << 16) | ((uint)mode << 8));

    public override string ToString() => $"0x{Raw:X8}";
}
