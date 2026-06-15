using Tuna.Core.Hardware;

namespace Tuna.App.ViewModels;

/// <summary>模式大按钮的一个选项:枚举值 + 当前语言显示名 + 图标。</summary>
public sealed class ModeOption
{
    public PowerMode Value { get; }
    public string Display { get; }
    public string Icon { get; }

    public ModeOption(PowerMode value, string display, string icon)
    {
        Value = value;
        Display = display;
        Icon = icon;
    }
}
