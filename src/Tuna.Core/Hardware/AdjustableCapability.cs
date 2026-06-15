namespace Tuna.Core.Hardware;

/// <summary>
/// 一个可调参数的完整描述(品牌无关)。连续型用 Min/Max/Step;
/// 离散型(如 GPU cTGP)用 <see cref="DiscreteValues"/> 白名单。
/// </summary>
public sealed record AdjustableCapability
{
    public required CapabilityId Id { get; init; }

    /// <summary>本地化键(语言无关),由上层 Localizer 翻译成显示名。</summary>
    public required string NameKey { get; init; }
    public string Unit { get; init; } = string.Empty;

    /// <summary>后端能否识别此参数(有明确名称)。未知参数默认不在主界面显示。</summary>
    public bool IsKnown { get; init; } = true;
    public int Min { get; init; }
    public int Max { get; init; }
    public int Step { get; init; } = 1;
    public int Default { get; init; }
    public int Current { get; init; }

    /// <summary>非空且非空集合表示这是离散型参数。</summary>
    public IReadOnlyList<int>? DiscreteValues { get; init; }

    public bool IsDiscrete => DiscreteValues is { Count: > 0 };

    /// <summary>校验目标值是否可安全写入。</summary>
    public bool IsValid(int value)
    {
        if (IsDiscrete)
            return DiscreteValues!.Contains(value);
        if (value < Min || value > Max)
            return false;
        if (Step > 0 && (value - Min) % Step != 0)
            return false;
        return true;
    }
}
