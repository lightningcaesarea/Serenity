using System.Linq;
using Content.Server.Preferences.Managers;
using Content.Shared.Administration;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.Administration.Commands;

/// <summary>
/// Clears every job preference of a connected player.
/// </summary>
[AdminCommand(AdminFlags.Round)]
public sealed partial class ClearJobPrioritiesCommand : LocalizedCommands
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IServerPreferencesManager _preferences = default!;

    public override string Command => "clearjobpriorities";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 1),
                ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var player))
        {
            shell.WriteError(Loc.GetString("cmd-clearjobpriorities-player-not-found", ("player", args[0])));
            return;
        }

        if (!_preferences.HavePreferencesLoaded(player))
        {
            shell.WriteError(Loc.GetString("cmd-clearjobpriorities-preferences-not-loaded", ("player", player.Name)));
            return;
        }

        // Serenity - job priorities are per player here, not per selected character.
        await _preferences.SetJobPriorities(player.UserId, new Dictionary<ProtoId<JobPrototype>, JobPriority>());

        shell.WriteLine(Loc.GetString("cmd-clearjobpriorities-success", ("player", player.Name)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(_players.Sessions.Select(player => player.Name),
                Loc.GetString("cmd-clearjobpriorities-hint-player"))
            : CompletionResult.Empty;
    }
}
