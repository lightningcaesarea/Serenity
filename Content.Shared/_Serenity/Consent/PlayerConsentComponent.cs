using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Consent;

/// <summary>
/// Snapshot of the controlling player's consent flags, kept on their mob so that shared and
/// predicted code can check consent on either side of an interaction without touching the
/// preferences manager. Maintained server-side; never edit it directly.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PlayerConsentComponent : Component
{
    /// <summary>
    /// Every consent toggle the player currently allows. Toggles missing from this set are denied.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public HashSet<string> Allowed = new();
}
