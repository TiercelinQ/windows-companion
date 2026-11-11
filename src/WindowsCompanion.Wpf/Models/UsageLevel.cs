namespace WindowsCompanion.Models;

/// <summary>Space usage level of a volume or of the memory (cahier 6.8).</summary>
public enum UsageLevel
{
    /// <summary>Below 80 %.</summary>
    Normal,

    /// <summary>From 80 % to below 90 %.</summary>
    Attention,

    /// <summary>90 % or more.</summary>
    Critical,
}

/// <summary>Threshold rules of <see cref="UsageLevel"/>, applied before rounding.</summary>
public static class UsageLevels
{
    /// <summary>Gets the level of a usage percentage.</summary>
    /// <param name="percent">Usage, not rounded.</param>
    /// <returns>The level.</returns>
    public static UsageLevel From(double percent) => percent switch
    {
        >= AppConfig.UsageCriticalPercent => UsageLevel.Critical,
        >= AppConfig.UsageAttentionPercent => UsageLevel.Attention,
        _ => UsageLevel.Normal,
    };
}
