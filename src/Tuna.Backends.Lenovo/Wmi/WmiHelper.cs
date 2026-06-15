using System.Management;

namespace Tuna.Backends.Lenovo.Wmi;

/// <summary>对 root\WMI 命名空间下 Lenovo 类的薄封装。</summary>
internal static class WmiHelper
{
    private const string Scope = @"\\.\root\WMI";

    public static IEnumerable<ManagementObject> Query(string className)
    {
        using var searcher = new ManagementObjectSearcher(Scope, $"SELECT * FROM {className}");
        foreach (ManagementObject mo in searcher.Get())
            yield return mo;
    }

    public static ManagementObject? First(string className)
    {
        using var searcher = new ManagementObjectSearcher(Scope, $"SELECT * FROM {className}");
        foreach (ManagementObject mo in searcher.Get())
            return mo;
        return null;
    }

    /// <summary>调用一个返回单值的方法。自动从 value/Data/Value 输出参数取结果。</summary>
    public static uint Invoke(string className, string method, IReadOnlyDictionary<string, object>? args = null)
    {
        var instance = First(className)
            ?? throw new InvalidOperationException($"WMI 类 {className} 不存在");
        using (instance)
        {
            ManagementBaseObject? inParams = null;
            if (args is { Count: > 0 })
            {
                inParams = instance.GetMethodParameters(method);
                foreach (var kv in args)
                    inParams[kv.Key] = kv.Value;
            }

            using var outParams = instance.InvokeMethod(method, inParams, null);
            inParams?.Dispose();
            if (outParams is null)
                return 0;

            var val = TryGet(outParams, "value") ?? TryGet(outParams, "Data") ?? TryGet(outParams, "Value");
            return val is null ? 0u : Convert.ToUInt32(val);
        }
    }

    private static object? TryGet(ManagementBaseObject obj, string name)
    {
        try { return obj[name]; }
        catch { return null; }
    }
}
