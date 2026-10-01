using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Recovery;

/// <summary>
/// Marks a mob as recoverable via the medical recovery system. Lives on a subdermal implant —
/// see <c>MedicalRecoveryImplant</c> in Resources/Prototypes/_Serenity/Medical/Recovery.
///
/// Three trigger paths: automatically, if the carrier dies while off the round's default station
/// map (see <see cref="MedicalRecoveryOnTriggerComponent"/>, fired via the vanilla
/// TriggerOnMobstateChange component); manually, by a paramedic activating a
/// <c>MedicalRecoveryBeaconComponent</c> item within range of the body; or from a
/// <see cref="MedicalRecoveryConsoleComponent"/>'s UI.
///
/// Every path runs the same two phases: <see cref="DeployDuration"/> to deploy the beacon, then a
/// fulton balloon carries the body to the pad over <see cref="TransitDuration"/>.
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

    [DataField]
    public TimeSpan DeployDuration = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan TransitDuration = TimeSpan.FromSeconds(10);
}
