using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Medical.Pain;

/// <summary>
/// Banded severity of the pain a mob is actually feeling (after painkillers).
/// </summary>
[Serializable, NetSerializable]
public enum PainLevel : byte
{
    None,
    Mild,
    Moderate,
    Severe,
    Agonizing,
}
