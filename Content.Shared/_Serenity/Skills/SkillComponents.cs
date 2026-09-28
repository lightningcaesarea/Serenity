using Content.Shared.DoAfter;
using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Skills;

/// <summary>
/// The skills a character knows. Lives on the mind so it survives cloning, borging and body swaps.
/// Minds are only sent to their own player, so nobody else can read this client-side.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSkillSystem))]
public sealed partial class SkillsComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<SkillPrototype>> Skills = new();
}

/// <summary>
/// Skills granted to a body before it had a mind (job and trait effects run during spawning).
/// Moved onto the mind when one arrives; also read directly for mindless mobs.
/// </summary>
[RegisterComponent]
[Access(typeof(SharedSkillSystem))]
public sealed partial class PendingSkillsComponent : Component
{
    [DataField]
    public HashSet<ProtoId<SkillPrototype>> Skills = new();
}

/// <summary>
/// Needs every listed skill to open this entity's UI. On a surgery prototype, needs them to perform its steps.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkillRequiredComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<SkillPrototype>> Skills = new();

    [DataField]
    public LocId Popup = "skill-required-popup";
}

/// <summary>
/// Reading this teaches one skill. Used up on success.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SkillBookComponent : Component
{
    [DataField(required: true)]
    public ProtoId<SkillPrototype> Skill;

    [DataField]
    public TimeSpan ReadTime = TimeSpan.FromSeconds(15);

    /// <summary>
    /// If set, only these jobs can learn from the book.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>>? Jobs;

    /// <summary>
    /// If set, only jobs in these departments can learn from the book. Combined with <see cref="Jobs"/> as either-or.
    /// </summary>
    [DataField]
    public List<ProtoId<DepartmentPrototype>>? Departments;
}

[Serializable, NetSerializable]
public sealed partial class SkillBookDoAfterEvent : SimpleDoAfterEvent;
