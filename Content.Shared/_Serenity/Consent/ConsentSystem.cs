using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Consent;

/// <summary>
/// Reads a player's consent flags. Anything that can subject a player to adult or
/// irreversible content should check here first.
/// </summary>
public static class ConsentCheck
{
    /// <summary>
    /// Whether the player allows <paramref name="toggleId"/>. Falls back to the prototype's
    /// default when the player has never set it, and denies outright if the toggle is unknown.
    /// </summary>
    public static bool HasConsent(
        IReadOnlyDictionary<string, bool>? toggles,
        string toggleId,
        IPrototypeManager protoManager)
    {
        if (!protoManager.TryIndex<ConsentTogglePrototype>(toggleId, out var proto))
            return false;

        if (toggles != null && toggles.TryGetValue(toggleId, out var set))
            return set;

        return proto.Default;
    }
}
