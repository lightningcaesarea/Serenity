using Content.Shared.DoAfter;

namespace Content.Shared._Serenity.DoAfter;

/// <summary>
/// Raised on the user as a do-after starts, after duplicate handling and before anything is recorded.
/// Handlers multiply <see cref="DelayMultiplier"/> rather than overwrite it, so modifiers stack.
/// </summary>
[ByRefEvent]
public record struct DoAfterStartingEvent(DoAfterArgs Args)
{
    public float DelayMultiplier = 1f;
    public bool Cancelled;
}
