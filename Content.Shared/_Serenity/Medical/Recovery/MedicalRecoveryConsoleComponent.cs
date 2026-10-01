using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Recovery;

/// <summary>
/// A stationary medical recovery console. Unlike the handheld beacon, this sees both Critical
/// AND Dead carriers of a MedicalRecoveryComponent implant — a deliberate rescue tool, not just a
/// body-recovery one. Its UI lists every carrier in <see cref="Range"/> on the same map and lets
/// the operator pick who to recover; they are delivered to <see cref="LinkedPad"/>, which is
/// auto-linked to the first MedicalRecoveryPadComponent on the same grid at MapInit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalRecoveryConsoleComponent : Component
{
    [DataField]
    public EntityUid? LinkedPad;

    [DataField]
    public float Range = 90f;

    /// <summary>
    /// How often an open UI is re-scanned so the target list stays current.
    /// </summary>
    [DataField]
    public TimeSpan UiUpdateInterval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextUiUpdate;
}
