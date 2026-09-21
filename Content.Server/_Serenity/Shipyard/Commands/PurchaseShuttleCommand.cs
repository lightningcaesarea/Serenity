// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Server._Serenity.Shipyard.Systems;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Utility;

namespace Content.Server._Serenity.Shipyard.Commands;

/// <summary>
/// Spawns a shuttle from a grid file and docks it to a station, free of charge and without a deed.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed partial class PurchaseShuttleCommand : LocalizedEntityCommands
{
    [Dependency] private ShipyardSystem _shipyard = default!;

    public override string Command => "purchaseshuttle";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                ("properAmount", 2), ("currentAmount", args.Length)));
            shell.WriteLine(Help);
            return;
        }

        if (!NetEntity.TryParse(args[0], out var stationNet) || !EntityManager.TryGetEntity(stationNet, out var station))
        {
            shell.WriteError(Loc.GetString("cmd-purchaseshuttle-no-entity", ("uid", args[0])));
            return;
        }

        var delay = 1f;
        if (args.Length >= 3 && !float.TryParse(args[2], out delay))
        {
            shell.WriteError(Loc.GetString("cmd-purchaseshuttle-invalid-delay", ("value", args[2])));
            return;
        }

        if (!_shipyard.TryPurchaseShuttle(station.Value, new ResPath(args[1]), delay, out _))
        {
            shell.WriteError(Loc.GetString("cmd-purchaseshuttle-failed"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-purchaseshuttle-success", ("path", args[1]), ("station", args[0])));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint(Loc.GetString("station-id")),
            2 => CompletionResult.FromHint(Loc.GetString("cmd-hint-savemap-path")),
            _ => CompletionResult.Empty,
        };
    }
}
