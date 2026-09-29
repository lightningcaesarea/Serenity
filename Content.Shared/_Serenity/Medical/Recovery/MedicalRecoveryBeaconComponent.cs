using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Recovery;

/// <summary>
/// Handheld paramedic tool. Activating it (use-in-hand) scans nearby space for a dead mob
/// carrying an unused <see cref="MedicalRecoveryComponent"/> implant and, if one is found within
/// <see cref="Range"/>, remotely triggers its recovery — no need to physically reach the body.
/// Only finds targets sharing the beacon holder's current map (tile distance is meaningless
/// across different maps/away sites), which is the point: take a shuttle out to the site, get
/// within range, and pull the body home without manually carrying it back.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalRecoveryBeaconComponent : Component
{
    [DataField]
    public float Range = 90f;

    /// <summary>
    /// Minimum time between uses, so it can't be spammed to continuously re-scan.
    /// </summary>
    [DataField]
    public TimeSpan UseCooldown = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan NextUseTime = TimeSpan.Zero;

    [DataField]
    public SoundSpecifier ActivateSound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");

    [DataField]
    public SoundSpecifier NoTargetSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}
