using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Display info for a wound, used in health analyzer and examine.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct WoundDisplayInfo(string LocKey, int Tier, ProtoId<WoundCategoryPrototype> Category);
