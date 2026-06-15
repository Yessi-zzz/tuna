using Tuna.Backends.Lenovo;
using Tuna.Core.Abstractions;

namespace Tuna.App;

/// <summary>多品牌扩展点:依次探测各后端,返回第一个支持当前机器的。</summary>
public static class BackendLocator
{
    public static IPowerController? Resolve()
    {
        IPowerController[] candidates =
        {
            new LenovoWmiController(),
            // 未来:new AsusWmiController(), new MsiEcController(), ...
        };

        return candidates.FirstOrDefault(SafeSupported);
    }

    private static bool SafeSupported(IPowerController controller)
    {
        try { return controller.IsSupported(); }
        catch { return false; }
    }
}
