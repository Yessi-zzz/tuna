using Tuna.Core.Hardware;

namespace Tuna.App.ViewModels;

/// <summary>自定义档单个参数的可编辑行:连续型用滑块(Min/Max/Step),离散型用下拉(Choices)。</summary>
public sealed class CustomParamEditor : ViewModelBase
{
    public CapabilityId CustomId { get; }
    public string Name { get; }
    public string Unit { get; }
    public string Range { get; }

    public bool IsDiscrete { get; }
    public bool IsContinuous => !IsDiscrete;

    public double Min { get; }
    public double Max { get; }
    public double Step { get; }
    public IReadOnlyList<int> Choices { get; }

    private double _value;
    public double Value
    {
        get => _value;
        set { if (Set(ref _value, value)) Raise(nameof(ResolvedValue)); }
    }

    private int _selectedChoice;
    public int SelectedChoice
    {
        get => _selectedChoice;
        set { if (Set(ref _selectedChoice, value)) Raise(nameof(ResolvedValue)); }
    }

    /// <summary>当前要写入的整数值(连续取滑块四舍五入,离散取选中项)。</summary>
    public int ResolvedValue => IsDiscrete ? _selectedChoice : (int)Math.Round(_value);

    public CustomParamEditor(AdjustableCapability cap, string name)
    {
        CustomId = cap.Id;
        Name = name;
        Unit = cap.Unit;
        IsDiscrete = cap.IsDiscrete;

        if (IsDiscrete)
        {
            Choices = cap.DiscreteValues!;
            _selectedChoice = Choices.Contains(cap.Current) ? cap.Current : Choices[^1];
            Range = string.Join(" / ", Choices);
        }
        else
        {
            Choices = Array.Empty<int>();
            Min = cap.Min;
            Max = cap.Max;
            Step = Math.Max(1, cap.Step);
            _value = Math.Clamp(cap.Current, cap.Min, cap.Max);
            Range = $"{cap.Min}–{cap.Max}";
        }
    }
}
