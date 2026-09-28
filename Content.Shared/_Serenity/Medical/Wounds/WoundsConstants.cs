namespace Content.Shared._Serenity.Medical.Wounds;

public static class WoundsConstants
{
    public const float MovementSlowMultiplier = 0.7f;

    public const float FractureDropChance = 0.5f;

    public const int MovementSlowTier = 2;

    public const int FractureDropTier = 3;

    public const int MaxWoundTier = 3;

    public const int MaxStackedWoundsPerType = 3;

    public const float DefaultBleedTier1Threshold = 1f;

    public const float DefaultBleedTier2Threshold = 3f;

    public const float DefaultBleedTier3Threshold = 6f;

    public const float DefaultThresholdMultiplier = 1f;

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

    /// <summary>
    /// How often the server sweeps <see cref="WoundComponent"/> entries
    /// to apply natural regen. Coarse by design — tier decay is minute-scale.
    /// </summary>
    public const float RegenTickSeconds = 10f;

    /// <summary>
    /// Seconds an untreated tier-3 wound spends before dropping to tier 2.
    /// </summary>
    public const float Tier3DecaySeconds = 120f;

    /// <summary>
    /// Seconds an untreated tier-2 wound spends before dropping to tier 1.
    /// </summary>
    public const float Tier2DecaySeconds = 180f;

    /// <summary>
    /// Seconds an untreated tier-1 wound spends before being removed.
    /// </summary>
    public const float Tier1DecaySeconds = 300f;
}
