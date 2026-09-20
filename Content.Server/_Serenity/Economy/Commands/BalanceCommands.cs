using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Shared._Serenity.Economy;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Serenity.Economy.Commands;

/// <summary>
/// Shared plumbing for the balance commands: player lookup, and reading/writing a balance whether
/// the player is online (through the manager, so the in-memory cache stays coherent) or offline
/// (straight to the DB).
/// </summary>
public abstract partial class BaseBalanceCommand : LocalizedCommands
{
    [Dependency] protected IPlayerLocator Locator = default!;
    [Dependency] protected IPlayerManager Players = default!;
    [Dependency] protected IServerDbManager Db = default!;
    [Dependency] protected ISerenityPlayerResourcesManager Resources = default!;
    [Dependency] protected IAdminLogManager AdminLog = default!;

    protected const string Credits = "credits";

    protected async Task<LocatedPlayerData?> Locate(IConsoleShell shell, string nameOrId)
    {
        var located = await Locator.LookupIdByNameOrIdAsync(nameOrId);
        if (located == null)
            shell.WriteError(Loc.GetString("cmd-balance-player-not-found", ("player", nameOrId)));
        return located;
    }

    protected bool TryGetSession(NetUserId userId, out ICommonSession session)
        => Players.TryGetSessionById(userId, out session!);

    protected async Task<double> GetBalance(LocatedPlayerData located)
    {
        if (TryGetSession(located.UserId, out var session)
            && Resources.TryGetResource(session, Credits, out var live))
            return live.Value;

        var stored = await Db.GetPlayerResources(located.UserId.UserId);
        return stored.GetValueOrDefault(Credits);
    }

    /// <summary>Apply a delta online-or-offline. Returns the resulting balance.</summary>
    protected async Task<double> Adjust(LocatedPlayerData located, double delta, string reason)
    {
        if (TryGetSession(located.UserId, out var session))
        {
            Resources.TryUpdateResource(session, Credits, delta, reason);
            Resources.TryGetResource(session, Credits, out var after);
            return after ?? 0;
        }

        var stored = await Db.GetPlayerResources(located.UserId.UserId);
        var value = stored.GetValueOrDefault(Credits) + delta;
        await Db.SetPlayerResource(located.UserId.UserId, Credits, value, delta, reason);
        return value;
    }

    protected static string Actor(IConsoleShell shell)
        => shell.Player?.Name ?? "SERVER";

    protected static string Fmt(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class BalanceCommand : BaseBalanceCommand
{
    public override string Command => "balance";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (await Locate(shell, args[0]) is not { } located)
            return;

        var online = TryGetSession(located.UserId, out _);
        var balance = await GetBalance(located);
        var ledger = await Db.GetPlayerResourceTransactions(located.UserId.UserId, 5);

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("cmd-balance-header",
            ("player", located.Username),
            ("state", online ? "online" : "offline"),
            ("balance", Fmt(balance))));

        foreach (var row in ledger)
            sb.AppendLine(FormatLedgerRow(row));

        shell.WriteLine(sb.ToString().TrimEnd());
    }

    internal static string FormatLedgerRow(PlayerResourceTransaction row)
        => $"  {row.CreatedAt:yyyy-MM-dd HH:mm:ss}  {(row.Delta >= 0 ? "+" : "")}{Fmt(row.Delta),10}  => {Fmt(row.BalanceAfter),10}  {row.Reason ?? "unspecified"}";
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class BalanceLedgerCommand : BaseBalanceCommand
{
    public override string Command => "balance_ledger";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        var count = 20;
        if (args.Length == 2 && (!int.TryParse(args[1], out count) || count < 1 || count > 500))
        {
            shell.WriteError(Loc.GetString("cmd-balance-bad-count"));
            return;
        }

        if (await Locate(shell, args[0]) is not { } located)
            return;

        var ledger = await Db.GetPlayerResourceTransactions(located.UserId.UserId, count);
        if (ledger.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-balance-ledger-empty", ("player", located.Username)));
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(Loc.GetString("cmd-balance-ledger-header", ("player", located.Username), ("count", ledger.Count)));
        foreach (var row in ledger)
            sb.AppendLine(BalanceCommand.FormatLedgerRow(row));
        shell.WriteLine(sb.ToString().TrimEnd());
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class BalanceAdjustCommand : BaseBalanceCommand
{
    public override string Command => "balance_adjust";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var delta) || delta == 0)
        {
            shell.WriteError(Loc.GetString("cmd-balance-bad-amount"));
            return;
        }

        if (await Locate(shell, args[0]) is not { } located)
            return;

        var reason = $"admin:{Actor(shell)}: {string.Join(' ', args.Skip(2))}";
        var after = await Adjust(located, delta, reason);

        AdminLog.Add(LogType.Economy, LogImpact.High,
            $"{Actor(shell)} adjusted {located.Username}'s credits by {Fmt(delta)} (now {Fmt(after)}). Reason: {string.Join(' ', args.Skip(2))}");

        shell.WriteLine(Loc.GetString("cmd-balance-adjusted",
            ("player", located.Username), ("delta", Fmt(delta)), ("balance", Fmt(after))));
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed partial class BalanceSetCommand : BaseBalanceCommand
{
    public override string Command => "balance_set";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            shell.WriteError(Loc.GetString("cmd-balance-bad-amount"));
            return;
        }

        if (await Locate(shell, args[0]) is not { } located)
            return;

        var before = await GetBalance(located);
        var reason = $"admin:{Actor(shell)}: {string.Join(' ', args.Skip(2))}";
        var after = await Adjust(located, value - before, reason);

        AdminLog.Add(LogType.Economy, LogImpact.High,
            $"{Actor(shell)} set {located.Username}'s credits to {Fmt(after)} (was {Fmt(before)}). Reason: {string.Join(' ', args.Skip(2))}");

        shell.WriteLine(Loc.GetString("cmd-balance-set",
            ("player", located.Username), ("before", Fmt(before)), ("balance", Fmt(after))));
    }
}
