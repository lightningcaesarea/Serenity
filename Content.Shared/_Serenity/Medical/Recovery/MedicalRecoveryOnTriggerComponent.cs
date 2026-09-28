using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Recovery;

/// <summary>
/// Attached to the medical recovery implant alongside a vanilla TriggerOnMobstateChange (fires on Dead).
/// When that trigger fires, the recovery system checks whether the carrier is currently off the
/// round's default station map — only then does it auto-launch the recovery. Death on the main
/// map does nothing automatically; that case is handled by a paramedic's recovery beacon instead.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalRecoveryOnTriggerComponent : Component;
