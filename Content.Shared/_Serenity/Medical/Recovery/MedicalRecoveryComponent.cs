using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Recovery;

/// <summary>
/// Marks a mob as recoverable via the medical recovery system. Lives on a subdermal implant —
/// see <c>MedicalRecoveryImplant</c> in Resources/Prototypes/_Serenity/Medical/MedicalRecovery.
///
/// Two trigger paths: automatically, if the carrier dies while off the round's default station
/// map (see <see cref="MedicalRecoveryOnTriggerComponent"/>, fired via the vanilla
/// TriggerOnMobstateChange component); or manually, by a paramedic activating a
/// <c>MedicalRecoveryBeaconComponent</c> item within range of the body.
///
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalRecoveryComponent : Component
{
    /// <summary>
    /// Once triggered, the implant is spent — set true and never fires again. A fresh implant
    /// must be installed to restore recovery capability.
    /// </summary>
    [DataField]
    public bool Used;

    /// <summary>
    /// How long the "balloon ascends, gets snagged" animation beat takes before the body actually
    /// relocates. Gives onlookers/rescuers a window to notice it happening.
    /// </summary>
    [DataField]
    public TimeSpan RecoveryDelay = TimeSpan.FromSeconds(6);
}
