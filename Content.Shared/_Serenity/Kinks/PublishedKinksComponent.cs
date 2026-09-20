using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Kinks;

/// <summary>
/// The controlling player's kink list, mirrored onto their mob so other players can read it from
/// the character inspect window. Present <b>only</b> while the player's "Publish my kink list"
/// consent toggle is on; removed the moment they turn it off. Maintained server-side by
/// <c>PublishedKinksSystem</c>; never edit it directly.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PublishedKinksComponent : Component
{
    /// <summary>Kink prototype id → the player's stated preference. Unlisted kinks have no stated preference.</summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, KinkPreferenceLevel> Kinks = new();
}
