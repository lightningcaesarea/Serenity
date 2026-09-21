// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity: values from upstream's Contraband
// severity, mobs never sellable, payout in Federal Bills, admin-logged.

using Content.Server.Administration.Logs;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Stack;
using Content.Server.Station.Systems;
using Content.Shared._Serenity.Contraband;
using Content.Shared.Contraband;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Serenity.Contraband;

public sealed partial class ContrabandExchangeSystem : SharedContrabandExchangeSystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private TransformSystem _transform = default!;

    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<CargoSellBlacklistComponent> _blacklistQuery;

    public override void Initialize()
    {
        base.Initialize();

        _mobQuery = GetEntityQuery<MobStateComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _blacklistQuery = GetEntityQuery<CargoSellBlacklistComponent>();

        SubscribeLocalEvent<ContrabandExchangeConsoleComponent, BoundUIOpenedEvent>((uid, comp, _) => UpdateUi(uid, comp));
        SubscribeLocalEvent<ContrabandExchangeConsoleComponent, ContrabandExchangeAppraiseMessage>((uid, comp, _) => UpdateUi(uid, comp));
        SubscribeLocalEvent<ContrabandExchangeConsoleComponent, ContrabandExchangeSellMessage>(OnSell);
    }

    private void UpdateUi(EntityUid uid, ContrabandExchangeConsoleComponent component)
    {
        if (Transform(uid).GridUid is not { } grid)
        {
            _ui.SetUiState(uid, ContrabandExchangeUiKey.Key, new ContrabandExchangeInterfaceState(0, 0, false));
            return;
        }

        GetPalletGoods(grid, component, out var toSell, out var amount);
        _ui.SetUiState(uid, ContrabandExchangeUiKey.Key, new ContrabandExchangeInterfaceState(amount, toSell.Count, true));
    }

    private void OnSell(EntityUid uid, ContrabandExchangeConsoleComponent component, ContrabandExchangeSellMessage args)
    {
        if (Transform(uid).GridUid is not { } grid)
        {
            _ui.SetUiState(uid, ContrabandExchangeUiKey.Key, new ContrabandExchangeInterfaceState(0, 0, false));
            return;
        }

        GetPalletGoods(grid, component, out var toSell, out var amount);
        if (toSell.Count == 0 || amount <= 0)
        {
            UpdateUi(uid, component);
            return;
        }

        var station = _station.GetOwningStation(grid);
        if (station != null)
        {
            var ev = new EntitySoldEvent(toSell, station.Value);
            RaiseLocalEvent(ref ev);
        }

        foreach (var ent in toSell)
        {
            Del(ent);
        }

        _stack.SpawnAtPosition(amount, component.RewardType, Transform(uid).Coordinates);

        _adminLogger.Add(LogType.Economy, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):player} sold {toSell.Count} contraband items for {amount} Federal Bills at {ToPrettyString(uid):console}");

        UpdateUi(uid, component);
    }

    private List<EntityUid> GetPallets(EntityUid grid)
    {
        var pallets = new List<EntityUid>();
        var query = AllEntityQuery<ContrabandExchangePalletComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.ParentUid == grid && xform.Anchored)
                pallets.Add(uid);
        }

        return pallets;
    }

    private void GetPalletGoods(EntityUid grid, ContrabandExchangeConsoleComponent console, out HashSet<EntityUid> toSell, out int amount)
    {
        amount = 0;
        toSell = new HashSet<EntityUid>();

        foreach (var pallet in GetPallets(grid))
        {
            foreach (var ent in _lookup.GetEntitiesIntersecting(pallet, LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate))
            {
                if (toSell.Contains(ent) || !_xformQuery.TryGetComponent(ent, out var xform) || xform.Anchored)
                    continue;

                if (!CanSell(ent, xform))
                    continue;

                var value = GetValue(ent, console);
                if (value <= 0)
                    continue;

                toSell.Add(ent);
                amount += value;
            }
        }
    }

    private int GetValue(EntityUid uid, ContrabandExchangeConsoleComponent console)
    {
        if (TryComp<ContrabandValueComponent>(uid, out var explicitValue))
            return explicitValue.Value;

        if (TryComp<ContrabandComponent>(uid, out var contraband) && console.SeverityValues.TryGetValue(contraband.Severity, out var value))
            return value;

        return 0;
    }

    /// <summary>
    /// Nothing living, nothing blacklisted, and nothing containing either.
    /// </summary>
    private bool CanSell(EntityUid uid, TransformComponent xform)
    {
        if (_mobQuery.HasComponent(uid) || _blacklistQuery.HasComponent(uid))
            return false;

        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!CanSell(child, _xformQuery.GetComponent(child)))
                return false;
        }

        return true;
    }
}
