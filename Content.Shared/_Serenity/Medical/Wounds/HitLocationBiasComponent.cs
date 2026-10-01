using Content.Shared._Starlight.Medical.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Makes hits from this entity land in some body parts more often than the target's hit-location weights say.
/// Goes on a melee weapon, a thrown item, a projectile or the gun that fires it (the projectile wins if both have
/// one), or on a mob for its unarmed attacks.
/// </summary>
[RegisterComponent]
public sealed partial class HitLocationBiasComponent : Component
{
    /// <summary>
    /// Multiplies the weight of each listed body part type. Unlisted types keep their weight; 0 means never there.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<BodyPartType, float> Multipliers = new();
}
