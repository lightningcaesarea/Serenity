using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Recovery;

/// <summary>
/// A stationary medical recovery console. Unlike the handheld beacon, this scans for both
/// Critical AND Dead carriers of a MedicalRecoveryComponent implant — a deliberate rescue tool,
/// not just a body-recovery one. Delivers to a specific <see cref="LinkedPad"/> rather than any
/// pad on the map; auto-links to the nearest unlinked MedicalRecoveryPadComponent on the same
/// grid when it's first initialized. Activated by clicking it (ActivateInWorldEvent), same
/// immediate-action model as the beacon — no BUI.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalRecoveryConsoleComponent : Component
{
    [DataField]
    public EntityUid? LinkedPad;

    [DataField]
    public float Range = 90f;

    [DataField]
    public TimeSpan UseCooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan NextUseTime = TimeSpan.Zero;
}
