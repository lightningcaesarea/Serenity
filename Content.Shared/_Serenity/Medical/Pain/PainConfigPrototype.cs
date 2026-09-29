using Content.Shared._Serenity.Medical.Wounds;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Pain;

/// <summary>
/// All tuning for how much pain wounds cause and what pain does to a mob. Referenced by
/// <see cref="PainComponent.Config"/>, so a species can have its own values.
/// </summary>
[Prototype]
public sealed partial class PainConfigPrototype : IPrototype
{
    public const string DefaultId = "DefaultPain";

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Raw pain never exceeds this value.
    /// </summary>
    [DataField]
    public float MaxPain = 100f;

    /// <summary>
    /// Raw pain each wound category adds per wound tier; index 0 is tier 1. Categories not listed add nothing.
    /// </summary>
    [DataField]
    public Dictionary<WoundCategory, float[]> WoundWeights = new();

    /// <summary>
    /// Thresholds and effects for each pain band above None. Bands not listed are skipped.
    /// </summary>
    [DataField]
    public Dictionary<PainLevel, PainLevelConfig> Levels = new();

    /// <summary>
    /// Every painkiller after the strongest active one only counts for this fraction of its strength,
    /// so stacking several drugs is a poor substitute for a stronger one.
    /// </summary>
    [DataField]
    public float SecondaryPainkillerFactor = 0.25f;

    /// <summary>
    /// Masked pain below this amount isn't reported as "masked".
    /// </summary>
    [DataField]
    public float MaskedReportThreshold = 5f;

    /// <summary>
    /// Seconds between server-side pain recalculations. Catches bleed changes and natural wound decay,
    /// which don't raise their own events. Read from the default config only.
    /// </summary>
    [DataField]
    public float TickSeconds = 2f;

    /// <summary>
    /// The band for an effective pain value: the highest configured band whose threshold it reaches.
    /// </summary>
    public PainLevel LevelFor(float pain)
    {
        for (var level = PainLevel.Agonizing; level > PainLevel.None; level--)
        {
            if (Levels.TryGetValue(level, out var config) && pain >= config.Threshold)
                return level;
        }

        return PainLevel.None;
    }

    public float SpeedMultiplier(PainLevel level)
    {
        return Levels.TryGetValue(level, out var config) ? config.SpeedMultiplier : 1f;
    }

    public float DropChance(PainLevel level)
    {
        return Levels.TryGetValue(level, out var config) ? config.DropChance : 0f;
    }

    /// <summary>
    /// Raw pain contributed by one wound (or bleed) of the given category and tier.
    /// </summary>
    public float WoundWeight(WoundCategory category, int tier)
    {
        if (tier <= 0 || !WoundWeights.TryGetValue(category, out var weights))
            return 0f;

        return weights[Math.Min(tier, weights.Length) - 1];
    }
}

[DataDefinition]
public sealed partial class PainLevelConfig
{
    /// <summary>
    /// Effective pain at which this band begins.
    /// </summary>
    [DataField(required: true)]
    public float Threshold;

    /// <summary>
    /// Movement speed multiplier while in this band.
    /// </summary>
    [DataField]
    public float SpeedMultiplier = 1f;

    /// <summary>
    /// Chance to drop a held item when hit while in this band.
    /// </summary>
    [DataField]
    public float DropChance;
}
