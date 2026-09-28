using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Medical.Wounds;

[Serializable, NetSerializable]
public enum WoundCategory : byte
{
    Bleeding,
    Fracture,
    Burn,
}
