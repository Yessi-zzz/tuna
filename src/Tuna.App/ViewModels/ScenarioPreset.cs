using Tuna.Core.Hardware;

namespace Tuna.App.ViewModels;

/// <summary>
/// 场景预设:面向「用什么场景」而非「哪个档」,降低小白门槛。
/// 点击后切到推荐预设档(可生效),并展示该场景的参数建议(细调待 v2)。
/// </summary>
public sealed class ScenarioPreset
{
    public string Icon { get; }
    public string Name { get; }
    public string Desc { get; }
    public PowerMode TargetMode { get; }

    public ScenarioPreset(string icon, string name, string desc, PowerMode targetMode)
    {
        Icon = icon;
        Name = name;
        Desc = desc;
        TargetMode = targetMode;
    }
}
