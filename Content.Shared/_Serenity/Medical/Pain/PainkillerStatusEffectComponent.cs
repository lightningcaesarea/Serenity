using Content.Shared._Serenity.Medical.Wounds;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Pain;

/// <summary>
/// Marks a status effect as a painkiller. While active it subtracts up to <see cref="Strength"/> raw pain
/// (only from <see cref="Scope"/>, when set) without touching the wounds themselves.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PainkillerStatusEffectComponent : Component
{
    /// <summary>
    /// Raw pain points masked while this effect is active.
    /// </summary>
    [DataField]
    public float Strength = 20f;

    /// <summary>
    /// Wound categories this painkiller can mask. Null means all pain, e.g. a systemic drug;
    /// a topical numbs only skin-level injuries and does nothing for a broken bone.
    /// </summary>
    [DataField]
    public HashSet<WoundCategory>? Scope;
}
