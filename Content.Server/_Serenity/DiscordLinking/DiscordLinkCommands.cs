using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Serenity.DiscordLinking;

/// <summary>
/// Shows which Discord account an SS14 player is linked to.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class DiscordLinkInfoCommand : LocalizedCommands
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerLocator _locator = default!;
    [Dependency] private DiscordAccountLinkManager _links = default!;

    public override string Command => "discordlink_info";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("serenity-discord-link-cmd-no-player", ("player", args[0])));
            return;
        }

        var link = await _db.GetDiscordLinkByPlayer(located.UserId.UserId);
        shell.WriteLine(link == null
            ? Loc.GetString("serenity-discord-link-cmd-not-linked", ("player", located.Username))
            : await _links.DescribeLink(link));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), Loc.GetString("serenity-discord-link-cmd-hint-player"))
            : CompletionResult.Empty;
    }
}

/// <summary>
/// Finds the SS14 account a Discord user ID is linked to.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class DiscordLinkLookupCommand : LocalizedCommands
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private DiscordAccountLinkManager _links = default!;

    public override string Command => "discordlink_lookup";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !ulong.TryParse(args[0], out var discordId))
        {
            shell.WriteLine(Help);
            return;
        }

        var link = await _db.GetDiscordLinkByDiscord(discordId);
        shell.WriteLine(link == null
            ? Loc.GetString("serenity-discord-link-cmd-discord-not-linked", ("discordId", discordId.ToString()))
            : await _links.DescribeLink(link));
    }
}

/// <summary>
/// Removes a player's link so they can link a different Discord account.
/// </summary>
[AdminCommand(AdminFlags.Ban)]
public sealed partial class DiscordLinkRemoveCommand : LocalizedCommands
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerLocator _locator = default!;
    [Dependency] private DiscordAccountLinkManager _links = default!;

    public override string Command => "discordlink_remove";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("serenity-discord-link-cmd-no-player", ("player", args[0])));
            return;
        }

        var link = await _db.GetDiscordLinkByPlayer(located.UserId.UserId);
        if (link == null || !await _db.RemoveDiscordLink(located.UserId.UserId))
        {
            shell.WriteLine(Loc.GetString("serenity-discord-link-cmd-not-linked", ("player", located.Username)));
            return;
        }

        var description = await _links.DescribeLink(link);
        shell.WriteLine(Loc.GetString("serenity-discord-link-unlinked", ("link", description)));
        await _links.PostLog(Loc.GetString("serenity-discord-link-log-unlinked",
            ("link", description),
            ("by", shell.Player?.Name ?? "SERVER")));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), Loc.GetString("serenity-discord-link-cmd-hint-player"))
            : CompletionResult.Empty;
    }
}
