namespace Tuna.Core.Hardware;

/// <summary>整机性能模式,数值对应 Lenovo GameZone ThermalMode。</summary>
public enum PowerMode : uint
{
    Quiet = 1,
    Balanced = 2,
    Performance = 3,
    Custom = 255,
}
