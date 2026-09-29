using Content.Shared.Alert;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Pain;

/// <summary>
/// How much a mob is hurting. Raw pain is derived from wounds and bleeding; painkillers mask part of it
/// without treating anything, so effective pain climbs back up when they wear off.
/// Recomputed server-side by <see cref="SharedPainSystem"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PainComponent : Component
{
    /// <summary>
    /// Pain from wounds and bleeding before any painkillers.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float RawPain;

    /// <summary>
    /// Pain actually felt, after painkillers. Drives every effect.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EffectivePain;

    [DataField, AutoNetworkedField]
    public PainLevel Level;

    /// <summary>
    /// True when painkillers are hiding a meaningful amount of pain, so a "fine" patient isn't mistaken for a healthy one.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Masked;

    /// <summary>
    /// Tuning for how this mob feels and reacts to pain.
    /// </summary>
    [DataField]
    public ProtoId<PainConfigPrototype> Config = PainConfigPrototype.DefaultId;

    [DataField]
    public ProtoId<AlertPrototype> Alert = "Pain";
}
