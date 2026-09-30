using Content.Shared.Alert;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
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

    [DataField]
    public ProtoId<AlertPrototype> FractureAlert = "Fracture";

    [DataField]
    public ProtoId<AlertPrototype> BurnAlert = "Burn";

    /// <summary>
    /// Scales the effective spike amount checked against wound thresholds.
    /// Higher values make wounds harder to trigger (used by the Tough quirk).
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float ThresholdMultiplier = WoundsConstants.DefaultThresholdMultiplier;
}
