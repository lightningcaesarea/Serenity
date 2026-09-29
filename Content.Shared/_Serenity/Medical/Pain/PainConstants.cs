using Content.Shared._Serenity.Medical.Wounds;

namespace Content.Shared._Serenity.Medical.Pain;

public static class PainConstants
{
    /// <summary>
    /// Raw and effective pain never exceed this value.
    /// </summary>
    public const float MaxPain = 100f;

    /// <summary>
    /// Effective-pain floors for each <see cref="PainLevel"/> above None.
    /// </summary>
    public const float MildThreshold = 10f;
    public const float ModerateThreshold = 25f;
    public const float SevereThreshold = 50f;
    public const float AgonizingThreshold = 75f;

    /// <summary>
    /// Every painkiller after the strongest active one only counts for this fraction of its strength,
    /// so stacking several drugs is a poor substitute for a stronger one.
    /// </summary>
    public const float SecondaryPainkillerFactor = 0.25f;

    /// <summary>
    /// Masked pain below this amount isn't reported as "masked".
    /// </summary>
    public const float MaskedReportThreshold = 5f;

    /// <summary>
    /// Chance to drop a held item when hit while at Severe or worse pain.
    /// </summary>
    public const float SevereDropChance = 0.25f;
    public const float AgonizingDropChance = 0.5f;

    /// <summary>
    /// Seconds between server-side pain recalculations. Catches bleed changes and natural wound decay,
    /// which don't raise their own events.
    /// </summary>
    public const float TickSeconds = 2f;

    public static float SpeedMultiplier(PainLevel level)
    {
        return level switch
        {
            PainLevel.Moderate => 0.9f,
            PainLevel.Severe => 0.75f,
            PainLevel.Agonizing => 0.6f,
            _ => 1f,
        };
    }

    public static PainLevel LevelFor(float pain)
    {
        if (pain >= AgonizingThreshold)
            return PainLevel.Agonizing;
        if (pain >= SevereThreshold)
            return PainLevel.Severe;
        if (pain >= ModerateThreshold)
            return PainLevel.Moderate;
        if (pain >= MildThreshold)
            return PainLevel.Mild;
        return PainLevel.None;
    }

    /// <summary>
    /// Raw pain contributed by one wound (or bleed) of the given category and tier.
    /// </summary>
    public static float WoundWeight(WoundCategory category, int tier)
    {
        var index = Math.Clamp(tier, 0, WoundsConstants.MaxWoundTier);
        return category switch
        {
            WoundCategory.Fracture => FractureWeights[index],
            WoundCategory.Burn => BurnWeights[index],
            WoundCategory.Bleeding => BleedWeights[index],
            _ => 0f,
        };
    }

    private static readonly float[] FractureWeights = { 0f, 10f, 25f, 45f };
    private static readonly float[] BurnWeights = { 0f, 8f, 20f, 40f };
    private static readonly float[] BleedWeights = { 0f, 5f, 12f, 25f };
}
