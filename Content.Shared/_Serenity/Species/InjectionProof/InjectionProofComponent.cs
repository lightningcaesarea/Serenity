using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Species.InjectionProof;

/// <summary>
/// The creature's body itself turns away injectors (syringes, hyposprays), as if it were wearing a hardsuit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InjectionProofComponent : Component;
