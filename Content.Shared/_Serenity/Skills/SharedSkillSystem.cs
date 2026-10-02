using System.Linq;
using Content.Shared._Serenity.DoAfter;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.UserInterface;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Skills;

public abstract partial class SharedSkillSystem : EntitySystem
{
    [Dependency] protected SharedMindSystem Mind = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    /// <summary>
    /// Skills that carry do-after rules, so the per-do-after scan skips the rest.
    /// </summary>
    private readonly List<SkillPrototype> _ruleSkills = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoAfterComponent, DoAfterStartingEvent>(OnDoAfterStarting);
        SubscribeLocalEvent<SkillRequiredComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
        SubscribeLocalEvent<SkillRequiredComponent, ExaminedEvent>(OnRequiredExamined);
        SubscribeLocalEvent<SkillBookComponent, ExaminedEvent>(OnBookExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        CacheRuleSkills();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SkillPrototype>())
            CacheRuleSkills();
    }

    private void CacheRuleSkills()
    {
        _ruleSkills.Clear();
        _ruleSkills.AddRange(Proto.EnumeratePrototypes<SkillPrototype>().Where(s => s.DoAfters.Count > 0));
    }

    #region Queries

    /// <summary>
    /// Where a character's skills are stored: their mind, or the body itself if it has none.
    /// </summary>
    public EntityUid GetHolder(EntityUid user)
        => Mind.TryGetMind(user, out var mindId, out _) ? mindId : user;

    public bool HasSkill(EntityUid user, ProtoId<SkillPrototype> skill)
    {
        if (Mind.TryGetMind(user, out var mindId, out _)
            && TryComp<SkillsComponent>(mindId, out var skills)
            && skills.Skills.Contains(skill))
            return true;

        return TryComp<PendingSkillsComponent>(user, out var pending) && pending.Skills.Contains(skill);
    }

    public bool HasAllSkills(EntityUid user, IEnumerable<ProtoId<SkillPrototype>> skills)
        => skills.All(s => HasSkill(user, s));

    /// <summary>
    /// Every skill the character knows, from the mind and anything still pending on the body.
    /// </summary>
    public HashSet<ProtoId<SkillPrototype>> GetSkills(EntityUid user)
    {
        var result = new HashSet<ProtoId<SkillPrototype>>();

        if (Mind.TryGetMind(user, out var mindId, out _) && TryComp<SkillsComponent>(mindId, out var skills))
            result.UnionWith(skills.Skills);

        if (TryComp<PendingSkillsComponent>(user, out var pending))
            result.UnionWith(pending.Skills);

        return result;
    }

    /// <summary>
    /// Prerequisites of <paramref name="skill"/> the character doesn't know yet.
    /// </summary>
    public List<ProtoId<SkillPrototype>> GetMissingPrerequisites(EntityUid user, ProtoId<SkillPrototype> skill)
    {
        if (!Proto.TryIndex(skill, out var proto))
            return new List<ProtoId<SkillPrototype>>();

        return proto.Requires.Where(r => !HasSkill(user, r)).ToList();
    }

    /// <summary>
    /// Extra produce the character gets when harvesting a plant, summed over the skills they know.
    /// </summary>
    public int GetPlantHarvestBonus(EntityUid user)
    {
        var bonus = 0;
        foreach (var skill in GetSkills(user))
        {
            if (Proto.TryIndex(skill, out var proto))
                bonus += proto.PlantHarvestBonus;
        }

        return bonus;
    }

    public string SkillName(ProtoId<SkillPrototype> skill)
        => Proto.TryIndex(skill, out var proto) ? Loc.GetString(proto.Name) : skill.Id;

    #endregion

    #region Mutation

    /// <summary>
    /// Grants a skill without checking prerequisites. Returns false if it was already known.
    /// </summary>
    public bool GrantSkill(EntityUid user, ProtoId<SkillPrototype> skill)
    {
        if (Mind.TryGetMind(user, out var mindId, out _))
            return GrantToMind(mindId, skill);

        return EnsureComp<PendingSkillsComponent>(user).Skills.Add(skill);
    }

    public bool GrantToMind(EntityUid mindId, ProtoId<SkillPrototype> skill)
    {
        var skills = EnsureComp<SkillsComponent>(mindId);
        if (!skills.Skills.Add(skill))
            return false;

        Dirty(mindId, skills);
        return true;
    }

    public bool RevokeSkill(EntityUid user, ProtoId<SkillPrototype> skill)
    {
        var removed = false;

        if (Mind.TryGetMind(user, out var mindId, out _) && TryComp<SkillsComponent>(mindId, out var skills)
            && skills.Skills.Remove(skill))
        {
            Dirty(mindId, skills);
            removed = true;
        }

        if (TryComp<PendingSkillsComponent>(user, out var pending))
            removed |= pending.Skills.Remove(skill);

        return removed;
    }

    /// <summary>
    /// Moves skills a body collected before it had a mind onto that mind.
    /// </summary>
    protected void FlushPending(EntityUid body, EntityUid mindId)
    {
        if (!TryComp<PendingSkillsComponent>(body, out var pending))
            return;

        var skills = EnsureComp<SkillsComponent>(mindId);
        skills.Skills.UnionWith(pending.Skills);
        Dirty(mindId, skills);
        RemComp<PendingSkillsComponent>(body);
    }

    #endregion

    #region Effects

    private void OnDoAfterStarting(Entity<DoAfterComponent> user, ref DoAfterStartingEvent ev)
    {
        var args = ev.Args;
        var eventName = args.Event.GetType().Name;

        foreach (var skill in _ruleSkills)
        {
            foreach (var rule in skill.DoAfters)
            {
                if (!Matches(rule, args, eventName))
                    continue;

                var known = HasSkill(user, skill.ID);
                if (!known && rule.Required)
                {
                    ev.Cancelled = true;
                    ServerPopup(user, Loc.GetString("skill-required-popup", ("skill", Loc.GetString(skill.Name))));
                    return;
                }

                ev.DelayMultiplier *= known ? rule.Skilled : rule.Unskilled;
                break;
            }
        }

        // Surgeries are entity prototypes, so they carry their skill lock as a component.
        if (args.Event is SurgeryDoAfterEvent surgery
            && Proto.TryIndex(surgery.Surgery, out var surgeryProto)
            && surgeryProto.TryGetComponent<SkillRequiredComponent>(out var required, _compFactory)
            && FirstMissing(user, required.Skills) is { } missing)
        {
            ev.Cancelled = true;
            ServerPopup(user, Loc.GetString(required.Popup, ("skill", SkillName(missing))));
        }
    }

    private bool Matches(SkillDoAfterRule rule, DoAfterArgs args, string eventName)
    {
        if (!rule.HasFilters)
            return false;

        if (rule.Events.Count > 0 && !rule.Events.Contains(eventName))
            return false;

        if (!rule.IncludeSelf && args.Target == args.User)
            return false;

        if (rule.ToolQualities.Count > 0
            && (!TryComp<ToolComponent>(args.Used, out var tool) || !rule.ToolQualities.Any(q => _tools.HasQuality(args.Used.Value, q, tool))))
            return false;

        if (rule.Used != null && (args.Used is not { } used || !_whitelist.IsValid(rule.Used, used)))
            return false;

        if (rule.Target != null && (args.Target is not { } target || !_whitelist.IsValid(rule.Target, target)))
            return false;

        return true;
    }

    private void OnUIOpenAttempt(Entity<SkillRequiredComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || FirstMissing(args.User, ent.Comp.Skills) is not { } missing)
            return;

        args.Cancel();
        if (!args.Silent)
            Popup.PopupClient(Loc.GetString(ent.Comp.Popup, ("skill", SkillName(missing))), ent, args.User);
    }

    private ProtoId<SkillPrototype>? FirstMissing(EntityUid user, List<ProtoId<SkillPrototype>> skills)
    {
        foreach (var skill in skills)
        {
            if (!HasSkill(user, skill))
                return skill;
        }

        return null;
    }

    /// <summary>
    /// Do-afters are started on the server alone as often as they're predicted, so a client popup could go missing.
    /// </summary>
    private void ServerPopup(EntityUid user, string message)
    {
        if (_net.IsServer)
            Popup.PopupEntity(message, user, user);
    }

    private void OnRequiredExamined(Entity<SkillRequiredComponent> ent, ref ExaminedEvent args)
    {
        var names = string.Join(", ", ent.Comp.Skills.Select(SkillName));
        args.PushMarkup(Loc.GetString("skill-required-examine", ("skills", names)));
    }

    private void OnBookExamined(Entity<SkillBookComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(SkillBookComponent)))
        {
            args.PushMarkup(Loc.GetString("skill-book-examine", ("skill", SkillName(ent.Comp.Skill))));

            if (Proto.TryIndex(ent.Comp.Skill, out var proto) && proto.Requires.Count > 0)
            {
                args.PushMarkup(Loc.GetString("skill-book-examine-requires",
                    ("skills", string.Join(", ", proto.Requires.Select(SkillName)))));
            }

            var restrictions = new List<string>();
            if (ent.Comp.Departments != null)
                restrictions.AddRange(ent.Comp.Departments.Select(d => Proto.TryIndex(d, out var dept) ? Loc.GetString(dept.Name) : d.Id));
            if (ent.Comp.Jobs != null)
                restrictions.AddRange(ent.Comp.Jobs.Select(j => Proto.TryIndex(j, out var job) ? job.LocalizedName : j.Id));

            if (restrictions.Count > 0)
                args.PushMarkup(Loc.GetString("skill-book-examine-restricted", ("who", string.Join(", ", restrictions))));
        }
    }

    #endregion
}
