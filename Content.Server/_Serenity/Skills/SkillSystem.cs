using System.Linq;
using Content.Shared._Serenity.Skills;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server._Serenity.Skills;

public sealed partial class SkillSystem : SharedSkillSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedJobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PendingSkillsComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<SkillBookComponent, UseInHandEvent>(OnBookUse);
        SubscribeLocalEvent<SkillBookComponent, SkillBookDoAfterEvent>(OnBookRead);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !Proto.TryIndex<JobSkillsPrototype>(ev.JobId, out var jobSkills))
            return;

        foreach (var skill in jobSkills.Skills)
        {
            GrantSkill(ev.Mob, skill);
        }
    }

    private void OnMindAdded(Entity<PendingSkillsComponent> ent, ref MindAddedMessage args)
    {
        FlushPending(ent, args.Mind);
    }

    #region Skill books

    private void OnBookUse(Entity<SkillBookComponent> book, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!CanLearn(args.User, book, out var reason))
        {
            Popup.PopupEntity(reason, book, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, book.Comp.ReadTime, new SkillBookDoAfterEvent(), book, used: book)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            Popup.PopupEntity(Loc.GetString("skill-book-start", ("skill", SkillName(book.Comp.Skill))), book, args.User);
        }
    }

    private void OnBookRead(Entity<SkillBookComponent> book, ref SkillBookDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Re-check: the reader may have learned it or changed jobs while reading.
        if (!CanLearn(args.User, book, out var reason))
        {
            Popup.PopupEntity(reason, book, args.User);
            return;
        }

        GrantSkill(args.User, book.Comp.Skill);
        Popup.PopupEntity(Loc.GetString("skill-book-learned", ("skill", SkillName(book.Comp.Skill))), args.User, args.User);
        QueueDel(book);
    }

    private bool CanLearn(EntityUid user, Entity<SkillBookComponent> book, out string reason)
    {
        var skill = book.Comp.Skill;

        if (HasSkill(user, skill))
        {
            reason = Loc.GetString("skill-book-already-known", ("skill", SkillName(skill)));
            return false;
        }

        var missing = GetMissingPrerequisites(user, skill);
        if (missing.Count > 0)
        {
            reason = Loc.GetString("skill-book-missing-prerequisites",
                ("skills", string.Join(", ", missing.Select(SkillName))));
            return false;
        }

        if (!MeetsJobRestriction(user, book.Comp))
        {
            reason = Loc.GetString("skill-book-wrong-job");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private bool MeetsJobRestriction(EntityUid user, SkillBookComponent book)
    {
        if (book.Jobs == null && book.Departments == null)
            return true;

        if (!Mind.TryGetMind(user, out var mindId, out _) || !_jobs.MindTryGetJobId(mindId, out var job) || job == null)
            return false;

        if (book.Jobs != null && book.Jobs.Contains(job.Value))
            return true;

        if (book.Departments == null || !_jobs.TryGetAllDepartments(job.Value, out var departments))
            return false;

        return departments.Any(d => book.Departments.Contains(d.ID));
    }

    #endregion
}
