using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Ids of the built-in <see cref="WoundCategoryPrototype"/>s, for the few places in code that refer to one directly.
/// A test checks that each exists.
/// </summary>
public static class WoundCategoryIds
{
    public static readonly ProtoId<WoundCategoryPrototype> Bleeding = "Bleeding";
    public static readonly ProtoId<WoundCategoryPrototype> Fracture = "Fracture";
    public static readonly ProtoId<WoundCategoryPrototype> Burn = "Burn";
    public static readonly ProtoId<WoundCategoryPrototype> Laceration = "Laceration";
    public static readonly ProtoId<WoundCategoryPrototype> Puncture = "Puncture";
    public static readonly ProtoId<WoundCategoryPrototype> Infection = "Infection";
}
