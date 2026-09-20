using Content.Shared._Serenity.Consent;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// One thing a participant can do to another (or to themselves) through the intimacy window.
/// </summary>
[Prototype]
public sealed partial class IntimacyActPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<IntimacyCategoryPrototype> Category;

    /// <summary>
    /// Minimum time between uses of this act by the same actor.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum actor-to-target distance in tiles. Self acts ignore this.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// The act can be used on oneself.
    /// </summary>
    [DataField]
    public bool AllowSelf;

    /// <summary>
    /// The act can only be used on oneself.
    /// </summary>
    [DataField]
    public bool SelfOnly;

    /// <summary>
    /// Stat changes applied to the actor. Negative values are allowed.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<IntimacyStatPrototype>, float> ActorStats = new();

    /// <summary>
    /// Stat changes applied to the target. Ignored for self acts (use <see cref="ActorStats"/>).
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<IntimacyStatPrototype>, float> TargetStats = new();

    /// <summary>
    /// Emote lines, one chosen at random. Each receives <c>$actor</c> and <c>$target</c>.
    /// </summary>
    [DataField]
    public List<LocId> Messages = new();

    /// <summary>
    /// Emote lines used instead when the actor targets themselves.
    /// </summary>
    [DataField]
    public List<LocId> SelfMessages = new();

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>
    /// Consent required of both parties in addition to the category's list.
    /// </summary>
    [DataField]
    public List<ProtoId<ConsentTogglePrototype>> Consent = new();

    /// <summary>
    /// Feature tags the actor must expose (see <see cref="IntimacyParticipantComponent.Features"/>).
    /// </summary>
    [DataField]
    public List<string> ActorNeeds = new();

    /// <summary>
    /// Feature tags the target must expose.
    /// </summary>
    [DataField]
    public List<string> TargetNeeds = new();
}
