using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Tuning for how wounds heal on their own and what they do to a mob. Referenced by
/// <see cref="WoundComponent.Config"/>, so a species can have its own values. What triggers a wound and its
/// tier names live in <see cref="WoundTypePrototype"/>.
/// </summary>
[Prototype]
public sealed partial class WoundConfigPrototype : IPrototype
{
    public const string DefaultId = "DefaultWounds";

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// How often the server sweeps wounds to apply natural regen. Coarse by design, since tier decay is minute-scale.
    /// Read from the default config only.
    /// </summary>
    [DataField]
    public float RegenTickSeconds = 10f;

    /// <summary>
    /// Seconds an untreated wound spends at each tier before dropping one; index 0 is tier 1.
    /// </summary>
    [DataField]
    public float[] TierDecaySeconds = [300f, 180f, 120f];

    /// <summary>
    /// Effects of each wound category on this mob (movement slow, item drops). Categories not listed have none.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<WoundCategoryPrototype>, WoundCategoryEffects> Categories = new();

    /// <summary>
    /// Damage types that make a mob bleed, for the bleed-source shown on the health analyzer. Earlier entries win:
    /// a later type never replaces an earlier one already recorded.
    /// </summary>
    [DataField]
    public List<ProtoId<DamageTypePrototype>> BleedSources = new();

    /// <summary>
    /// How many wounds of one type can be active at once.
    /// </summary>
    [DataField]
    public int MaxStackedWoundsPerType = 3;

    /// <summary>
    /// Time until a wound at the given tier next drops. Zero for tiers outside the configured range,
    /// so the caller can treat them as already expired.
    /// </summary>
    public TimeSpan GetTierDecayDuration(int tier)
    {
        if (tier < 1 || tier > TierDecaySeconds.Length)
            return TimeSpan.Zero;

        return TimeSpan.FromSeconds(TierDecaySeconds[tier - 1]);
    }

    public WoundCategoryEffects EffectsFor(ProtoId<WoundCategoryPrototype> category)
    {
        return Categories.TryGetValue(category, out var effects) ? effects : WoundCategoryEffects.None;
    }
}

[DataDefinition]
public sealed partial class WoundCategoryEffects
{
    public static readonly WoundCategoryEffects None = new();

    /// <summary>
    /// Wounds of this category at this tier or worse slow the mob by <see cref="SlowMultiplier"/>. 0 means never.
    /// </summary>
    [DataField]
    public int SlowTier;

    [DataField]
    public float SlowMultiplier = 1f;

    /// <summary>
    /// Wounds of this category at this tier or worse can make the mob drop a held item when hit. 0 means never.
    /// </summary>
    [DataField]
    public int DropTier;

    [DataField]
    public float DropChance;
}
