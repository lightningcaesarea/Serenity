namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// Raised on both participants after an act resolves. <see cref="Actor"/> and <see cref="Target"/>
/// are the same entity for self acts.
/// </summary>
[ByRefEvent]
public readonly record struct IntimacyActPerformedEvent(EntityUid Actor, EntityUid Target, IntimacyActPrototype Act);

/// <summary>
/// Raised on a participant whenever one of its stats changes by any means.
/// </summary>
[ByRefEvent]
public readonly record struct IntimacyStatChangedEvent(EntityUid Mob, string Stat, float Old, float New);

/// <summary>
/// Raised on a participant when they climax.
/// </summary>
[ByRefEvent]
public readonly record struct ClimaxEvent(EntityUid Mob, bool Automatic);
