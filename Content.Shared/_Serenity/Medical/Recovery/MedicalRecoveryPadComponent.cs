using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Recovery;

/// <summary>
/// Marker for a landing spot recovered bodies are delivered to. Place one (or more — the system
/// picks one at random) on the round's default station map, typically in Medbay. If none exist,
/// recovery silently does nothing rather than erroring — map it before relying on this feature.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class MedicalRecoveryPadComponent : Component;
