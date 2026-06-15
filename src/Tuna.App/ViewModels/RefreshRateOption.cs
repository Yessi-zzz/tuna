namespace Tuna.App.ViewModels;

/// <summary>一个可选刷新率(Hz)及其是否为当前值(用于高亮)。</summary>
public sealed class RefreshRateOption
{
    public int Hz { get; }
    public bool IsCurrent { get; }
    public string Label => $"{Hz} Hz";

    public RefreshRateOption(int hz, bool isCurrent)
    {
        Hz = hz;
        IsCurrent = isCurrent;
    }
}
