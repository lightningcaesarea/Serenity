using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// Lets a participant climax, either on demand or automatically when pleasure peaks.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ClimaxComponent : Component
{
    /// <summary>
    /// Stat that drives climax.
    /// </summary>
    [DataField]
    public ProtoId<IntimacyStatPrototype> Stat = "Pleasure";

    /// <summary>
    /// Pleasure at or above this triggers an automatic climax, if the player consents to that.
    /// </summary>
    [DataField]
    public float AutoThreshold = 85f;

    /// <summary>
    /// Minimum pleasure needed before the manual climax button works.
    /// </summary>
    [DataField]
    public float ManualThreshold = 40f;

    [DataField]
    public TimeSpan Refractory = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Stat values written after a climax. Stats not listed are left alone.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<IntimacyStatPrototype>, float> After = new()
    {
        ["Pleasure"] = 0f,
        ["Arousal"] = 15f,
    };

    [DataField]
    public List<LocId> Messages = new();

    [DataField]
    public SoundSpecifier? Sound;

    [ViewVariables, AutoNetworkedField]
    public TimeSpan ReadyAt;
}
