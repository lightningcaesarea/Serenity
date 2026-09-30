namespace Content.Shared._Serenity.Medical.Wounds;

public static class WoundsConstants
{
    public const int MaxWoundTier = 3;

    public const float DefaultBleedTier1Threshold = 1f;

    public const float DefaultBleedTier2Threshold = 3f;

    public const float DefaultBleedTier3Threshold = 6f;

    public const float DefaultThresholdMultiplier = 1f;

    /// <summary>
    /// Bounds for <see cref="WoundComponent.ThresholdMultiplier"/> as used when checking thresholds.
    /// </summary>
    public const float MinThresholdMultiplier = 0.1f;

    public const float MaxThresholdMultiplier = 10f;

    /// <summary>
    /// 1-based tier offset applied to a zero-based threshold-array index
    /// so tier "1" maps to <c>Thresholds[0]</c>, tier "2" to <c>Thresholds[1]</c>, …
    /// </summary>
    public const int TierIndexToTierOffset = 1;

    /// <summary>
    /// 1-based-tier to 0-based alert-severity offset used when pushing
    /// fracture / burn tiers into the alert widget (tier 1 = severity 0).
    /// </summary>
    public const short AlertSeverityTierOffset = 1;
}
