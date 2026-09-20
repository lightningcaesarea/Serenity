using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// Marks a mob that can take part in intimacy acts and stores its live stat values.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IntimacyParticipantComponent : Component
{
    /// <summary>
    /// Current value of each stat, keyed by <see cref="IntimacyStatPrototype"/> id.
    /// Stats never touched are treated as zero.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, float> Stats = new();

    /// <summary>
    /// Free-form capability tags other systems add (for example body parts). Acts declare which
    /// tags they need on the actor and the target.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Features = new();

    /// <summary>
    /// Server-side cooldown bookkeeping: when each act may next be used by this participant.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, TimeSpan> ReadyAt = new();

    /// <summary>
    /// Next time the stat decay tick runs for this participant.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextDecay;

    /// <summary>
    /// The entity this participant currently has the intimacy window open on, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? WindowTarget;
}
