using System.Linq;
using Content.Server.Administration;
using Content.Shared._Serenity.Skills;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._Serenity.Skills;

public abstract partial class BaseSkillCommand : LocalizedEntityCommands
{
    [Dependency] protected IPlayerManager Players = default!;
    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SkillSystem Skills = default!;

    protected bool TryGetBody(IConsoleShell shell, string name, out EntityUid body)
    {
        body = default;
        if (!Players.TryGetSessionByUsername(name, out var session))
        {
            shell.WriteError(Loc.GetString("skill-cmd-no-player", ("player", name)));
            return false;
        }

        if (session.AttachedEntity is not { } attached)
        {
            shell.WriteError(Loc.GetString("skill-cmd-no-body", ("player", name)));
            return false;
        }

        body = attached;
        return true;
    }

    protected bool TryGetSkill(IConsoleShell shell, string id, out ProtoId<SkillPrototype> skill)
    {
        skill = id;
        if (Proto.HasIndex<SkillPrototype>(id))
            return true;

        shell.WriteError(Loc.GetString("skill-cmd-no-skill", ("skill", id)));
        return false;
    }

    protected CompletionResult Complete(string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: Players), Loc.GetString("skill-cmd-hint-player")),
            2 => CompletionResult.FromHintOptions(CompletionHelper.PrototypeIDs<SkillPrototype>(proto: Proto), Loc.GetString("skill-cmd-hint-skill")),
            _ => CompletionResult.Empty,
        };
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SkillsListCommand : BaseSkillCommand
{
    public override string Command => "skills_list";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryGetBody(shell, args[0], out var body))
            return;

        var known = Skills.GetSkills(body).Select(s => Skills.SkillName(s)).OrderBy(n => n).ToList();
        shell.WriteLine(known.Count == 0
            ? Loc.GetString("skill-cmd-list-none", ("player", args[0]))
            : Loc.GetString("skill-cmd-list", ("player", args[0]), ("skills", string.Join(", ", known))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        => args.Length == 1 ? Complete(args) : CompletionResult.Empty;
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SkillsGrantCommand : BaseSkillCommand
{
    public override string Command => "skills_grant";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryGetBody(shell, args[0], out var body) || !TryGetSkill(shell, args[1], out var skill))
            return;

        shell.WriteLine(Skills.GrantSkill(body, skill)
            ? Loc.GetString("skill-cmd-granted", ("player", args[0]), ("skill", Skills.SkillName(skill)))
            : Loc.GetString("skill-cmd-already", ("player", args[0]), ("skill", Skills.SkillName(skill))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) => Complete(args);
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SkillsRevokeCommand : BaseSkillCommand
{
    public override string Command => "skills_revoke";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryGetBody(shell, args[0], out var body) || !TryGetSkill(shell, args[1], out var skill))
            return;

        shell.WriteLine(Skills.RevokeSkill(body, skill)
            ? Loc.GetString("skill-cmd-revoked", ("player", args[0]), ("skill", Skills.SkillName(skill)))
            : Loc.GetString("skill-cmd-not-known", ("player", args[0]), ("skill", Skills.SkillName(skill))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args) => Complete(args);
}
