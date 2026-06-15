using System.Text;
using Tuna.Backends.Lenovo;
using Tuna.Core.Hardware;

// 只读自测:验证 Lenovo WMI 后端能识别本机、读出可调参数表。绝不写入。
Console.OutputEncoding = Encoding.UTF8;

var controller = new LenovoWmiController();

Console.WriteLine("============ Tuna 只读自测 ============");
Console.WriteLine($"后端: {controller.BackendName}");

bool supported;
try { supported = controller.IsSupported(); }
catch (Exception ex) { Console.WriteLine($"IsSupported 抛异常: {ex.Message}"); return; }
Console.WriteLine($"IsSupported: {supported}");

if (!supported)
{
    Console.WriteLine("本机不被 Lenovo 后端支持(或 WMI 不可访问 / 未提权)。");
    return;
}

try
{
    var mode = controller.GetPowerMode();
    Console.WriteLine($"当前性能模式: {mode} ({(uint)mode})");
}
catch (Exception ex) { Console.WriteLine($"读取性能模式失败: {ex.Message}"); }

Console.WriteLine();
Console.WriteLine("---- 全部可调参数(原始,含所有模式)----");
List<Tuna.Core.Hardware.AdjustableCapability> caps;
try { caps = controller.GetCapabilities().ToList(); }
catch (Exception ex) { Console.WriteLine($"GetCapabilities 失败: {ex.Message}"); return; }

Console.WriteLine($"共读到 {caps.Count} 条 capability 记录。\n");

// 按界面实际会显示的口径过滤:自定义档 + 已知 + 有范围/离散值
var ui = caps.Where(c => c.Id.Mode == CapabilityMode.Custom && c.IsKnown && (c.Max > 0 || c.IsDiscrete)).ToList();

Console.WriteLine($"{"功能ID",-12} {"类别",-8} {"参数名",-22} {"范围",-28} {"默认",-6} {"当前",-6}");
Console.WriteLine(new string('-', 92));
foreach (var c in caps.OrderBy(c => c.Id.Raw))
{
    var range = c.IsDiscrete
        ? string.Join("/", c.DiscreteValues!)
        : $"{c.Min}-{c.Max} (步{c.Step})";
    var name = c.NameKey + ModeTag(c.Id.Mode);
    Console.WriteLine($"{c.Id,-12} {c.Id.Category,-8} {name,-26} {range,-28} {c.Default,-6} {c.Current,-6}");
}

static string ModeTag(CapabilityMode mode) => mode switch
{
    CapabilityMode.Quiet => " [安静]",
    CapabilityMode.Balanced => " [均衡]",
    CapabilityMode.Performance => " [性能]",
    CapabilityMode.Custom => " [自定义]",
    CapabilityMode.Current => " [当前]",
    _ => string.Empty,
};

Console.WriteLine();
Console.WriteLine($"---- 界面将显示的「自定义档」可编辑项: {ui.Count} 项 ----");
foreach (var c in ui)
{
    var range = c.IsDiscrete ? string.Join("/", c.DiscreteValues!) : $"{c.Min}-{c.Max}";
    Console.WriteLine($"  · {c.NameKey} [{c.Unit}]  {range}  默认{c.Default} 当前{c.Current}");
}

Console.WriteLine();
Console.WriteLine("自测完成(只读,未做任何写入)。");
