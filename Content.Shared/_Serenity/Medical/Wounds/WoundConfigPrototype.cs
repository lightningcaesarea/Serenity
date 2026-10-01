using Content.Shared._Starlight.Medical.Body.Part;
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
    /// Where a damaging hit can land, and how likely each place is. A wound gets one of the locations the mob actually
    /// has (a missing limb is never picked). Empty means wounds have no location.
    /// </summary>
    [DataField]
    public List<HitLocationWeight> HitLocations = new();

    /// <summary>
    /// How clicking high or low on this mob's sprite steers a melee hit. Null means attacks can't be aimed.
    /// </summary>
    [DataField]
    public HitAimConfig? Aim;

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
    /// Body part types where a wound causes the slow (a broken leg, not a broken arm). Empty means anywhere.
    /// A wound with no location (a mob without a body) always counts.
    /// </summary>
    [DataField]
    public List<BodyPartType> SlowLocations = new();

    /// <summary>
    /// Wounds of this category at this tier or worse can make the mob drop a held item when hit. 0 means never.
    /// </summary>
    [DataField]
    public int DropTier;

    [DataField]
    public float DropChance;

    /// <summary>
    /// Body part types where a wound causes drops. A wound on one side drops from the hand on that side. Empty
    /// means anywhere.
    /// </summary>
    [DataField]
    public List<BodyPartType> DropLocations = new();

    /// <summary>
    /// A status effect given for <see cref="StatusSeconds"/> whenever the mob is hurt while it has a wound of this
    /// category at <see cref="StatusTier"/> or worse in one of <see cref="StatusLocations"/> (e.g. a concussion).
    /// </summary>
    [DataField]
    public EntProtoId? StatusEffect;

    /// <summary>
    /// 0 means never.
    /// </summary>
    [DataField]
    public int StatusTier;

    [DataField]
    public float StatusSeconds = 10f;

    /// <summary>
    /// Empty means anywhere.
    /// </summary>
    [DataField]
    public List<BodyPartType> StatusLocations = new();
}

/// <summary>
/// Aiming by where you click on the target: the click's height on the (upright) sprite picks a zone, and hits are
/// more likely to land in that zone's body parts. It shifts the odds, it never guarantees the part.
/// </summary>
[DataDefinition]
public sealed partial class HitAimConfig
{
    /// <summary>
    /// Multiplies the hit weight of the aimed zone's body parts.
    /// </summary>
    [DataField]
    public float Multiplier = 3f;

    /// <summary>
    /// A click farther than this (in tiles) from the target's centre isn't aiming at it, e.g. a wide swing.
    /// </summary>
    [DataField]
    public float MaxDistance = 0.7f;

    /// <summary>
    /// Height bands on the sprite, in tiles from its centre (up is positive). The first band containing the click
    /// wins; a click in no band isn't aimed.
    /// </summary>
    [DataField]
    public List<HitAimZone> Zones = new();

    /// <summary>
    /// The body part types aimed at by a click this far above (negative: below) the target's centre, or null.
    /// </summary>
    public List<BodyPartType>? ZoneAt(float height)
    {
        foreach (var zone in Zones)
        {
            if (height >= zone.Min && height < zone.Max)
                return zone.Types;
        }

        return null;
    }
}

[DataDefinition]
public sealed partial class HitAimZone
{
    [DataField]
    public float Min = float.NegativeInfinity;

    [DataField]
    public float Max = float.PositiveInfinity;

    [DataField(required: true)]
    public List<BodyPartType> Types = new();
}

[DataDefinition]
public sealed partial class HitLocationWeight
{
    [DataField(required: true)]
    public BodyPartType Type;

    [DataField]
    public BodyPartSymmetry Symmetry;

    /// <summary>
    /// Relative chance of a hit landing here. Only the ratios between locations matter.
    /// </summary>
    [DataField(required: true)]
    public float Weight;
}
