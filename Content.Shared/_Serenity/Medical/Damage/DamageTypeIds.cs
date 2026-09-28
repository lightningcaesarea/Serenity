using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Medical.Damage;

/// <summary>
/// Typed references for the standard SS14 damage-type prototypes, so fork code can
/// name a damage type without a raw string literal. A renamed or removed prototype
/// then surfaces here in one place instead of failing silently as a dictionary miss
/// scattered across systems.
/// </summary>
public static class DamageTypeIds
{
    public static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    public static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    public static readonly ProtoId<DamageTypePrototype> Piercing = "Piercing";
    public static readonly ProtoId<DamageTypePrototype> Heat = "Heat";
    public static readonly ProtoId<DamageTypePrototype> Shock = "Shock";
    public static readonly ProtoId<DamageTypePrototype> Cold = "Cold";
    public static readonly ProtoId<DamageTypePrototype> Caustic = "Caustic";
    public static readonly ProtoId<DamageTypePrototype> Poison = "Poison";
    public static readonly ProtoId<DamageTypePrototype> Radiation = "Radiation";
    public static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    public static readonly ProtoId<DamageTypePrototype> Bloodloss = "Bloodloss";
    public static readonly ProtoId<DamageTypePrototype> Cellular = "Cellular";
    public static readonly ProtoId<DamageTypePrototype> Structural = "Structural";
}
