using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Tracks active wounds (fractures and burns) and bleed source info for display.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedWoundSystem), typeof(WoundDisplaySystem))]
public sealed partial class WoundComponent : Component
{
    /// <summary>
    /// Active fracture and burn wounds.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public List<WoundEntry> ActiveWounds = new();

    /// <summary>
    /// Which damage type (Slash or Piercing) most recently contributed to bleeding.
    /// Used for display name selection on health analyzer.
    /// </summary>
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public string? BleedSourceDamageType;

    /// <summary>
    /// BleedAmount breakpoints for bleeding wound tiers 1/2/3.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float[] BleedTierThresholds =
    [
        WoundsConstants.DefaultBleedTier1Threshold,
        WoundsConstants.DefaultBleedTier2Threshold,
        WoundsConstants.DefaultBleedTier3Threshold,
    ];

    /// <summary>
    /// Tuning for natural regen and wound effects on this mob.
    /// </summary>
    [DataField]
    public ProtoId<WoundConfigPrototype> Config = WoundConfigPrototype.DefaultId;

    /// <summary>
    /// Scales the effective spike amount checked against wound thresholds.
    /// Higher values make wounds harder to trigger (used by the Tough quirk).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThresholdMultiplier = WoundsConstants.DefaultThresholdMultiplier;

    /// <summary>
    /// The weapon (or projectile, or thrown item) whose hit is about to deal damage, set just before the damage so
    /// the hit counts as a weapon hit and can use the weapon's <see cref="HitLocationBiasComponent"/>. Only valid
    /// during <see cref="PendingHitTick"/>. Server only, not saved.
    /// </summary>
    [ViewVariables]
    public EntityUid? PendingHitSource;

    [ViewVariables]
    public GameTick PendingHitTick;

    /// <summary>
    /// The body part types the pending hit was aimed at, if the attacker aimed (see <see cref="HitAimConfig"/>).
    /// </summary>
    [ViewVariables]
    public List<BodyPartType>? PendingHitAim;
}
