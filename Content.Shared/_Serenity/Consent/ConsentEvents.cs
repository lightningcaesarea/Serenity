namespace Content.Shared._Serenity.Consent;

/// <summary>
/// Raised on a mob after its <see cref="PlayerConsentComponent"/> has been rebuilt from the
/// player's preferences. Systems that grant abilities based on consent should re-evaluate here.
/// </summary>
[ByRefEvent]
public readonly record struct ConsentChangedEvent(EntityUid Mob);
