using Robust.Shared.GameObjects;

namespace Content.Shared._Serenity.Medical.Pain;

/// <summary>
/// Raised on a mob (server-side) after its pain level moves to a different band.
/// </summary>
public sealed class PainLevelChangedEvent(PainLevel oldLevel, PainLevel newLevel, bool masked) : EntityEventArgs
{
    public readonly PainLevel OldLevel = oldLevel;
    public readonly PainLevel NewLevel = newLevel;
    public readonly bool Masked = masked;
}
