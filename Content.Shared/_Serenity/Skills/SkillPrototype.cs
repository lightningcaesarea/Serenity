using Content.Shared.Tools;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Skills;

/// <summary>
/// Something a character either knows or doesn't. Jobs, perks and skill books grant skills;
/// <see cref="DoAfters"/> rules and <see cref="SkillRequiredComponent"/> give them their effect.
/// </summary>
[Prototype]
public sealed partial class SkillPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>
    /// Heading the skill is grouped under in the character window.
    /// </summary>
    [DataField(required: true)]
    public LocId Category;

    /// <summary>
    /// Skills a character must already know before a skill book can teach this one.
    /// </summary>
    [DataField]
    public List<ProtoId<SkillPrototype>> Requires = new();

    /// <summary>
    /// Do-after speed changes and locks. For each skill, only the first matching rule applies.
    /// </summary>
    [DataField]
    public List<SkillDoAfterRule> DoAfters = new();
}

/// <summary>
/// Matches a do-after and scales its duration depending on whether the user knows the skill.
/// Every filter that is set must match; a rule with no filters never matches.
/// </summary>
[DataDefinition]
public sealed partial class SkillDoAfterRule
{
    /// <summary>
    /// Do-after event class names, e.g. <c>HealingDoAfterEvent</c>.
    /// </summary>
    [DataField]
    public List<string> Events = new();

    /// <summary>
    /// The item used must be a tool with at least one of these qualities.
    /// </summary>
    [DataField]
    public List<ProtoId<ToolQualityPrototype>> ToolQualities = new();

    [DataField]
    public EntityWhitelist? Used;

    [DataField]
    public EntityWhitelist? Target;

    /// <summary>
    /// Whether the rule also matches do-afters the user performs on themselves, e.g. breaking out of cuffs.
    /// </summary>
    [DataField]
    public bool IncludeSelf = true;

    /// <summary>
    /// Duration multiplier when the user knows the skill.
    /// </summary>
    [DataField]
    public float Skilled = 1f;

    /// <summary>
    /// Duration multiplier when they don't.
    /// </summary>
    [DataField]
    public float Unskilled = 1f;

    /// <summary>
    /// Refuse to start the do-after at all without the skill.
    /// </summary>
    [DataField]
    public bool Required;

    public bool HasFilters => Events.Count > 0 || ToolQualities.Count > 0 || Used != null || Target != null;
}

/// <summary>
/// Skills a job starts with. The ID is the job's ID.
/// </summary>
[Prototype]
public sealed partial class JobSkillsPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ProtoId<SkillPrototype>> Skills = new();
}
