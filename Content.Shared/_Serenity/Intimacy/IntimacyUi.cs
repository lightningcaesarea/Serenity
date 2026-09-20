using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Intimacy;

[Serializable, NetSerializable]
public enum IntimacyUiKey : byte
{
    Key,
}

/// <summary>
/// The window is bound to the actor's own mob; the state names who they are acting on.
/// </summary>
[Serializable, NetSerializable]
public sealed class IntimacyUiState : BoundUserInterfaceState
{
    public NetEntity Target;
    public string TargetName = string.Empty;
    public Dictionary<string, float> ActorStats = new();
    public Dictionary<string, float> TargetStats = new();

    /// <summary>
    /// Act ids the actor may currently perform on the target.
    /// </summary>
    public HashSet<string> Available = new();

    public bool CanClimax;
}

[Serializable, NetSerializable]
public sealed class IntimacyPerformActMessage(string act) : BoundUserInterfaceMessage
{
    public string Act = act;
}

[Serializable, NetSerializable]
public sealed class IntimacyClimaxMessage : BoundUserInterfaceMessage;
