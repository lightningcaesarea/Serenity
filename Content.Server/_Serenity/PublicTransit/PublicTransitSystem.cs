// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity: modern map loading, and the bus is
// set up lazily once the round is running and at least one stop exists, instead of on a round event.

using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Serenity.CCVar;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tiles;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Serenity.PublicTransit;

/// <summary>
/// Runs a bus on a loop between every grid marked with <see cref="StationTransitComponent"/>.
/// </summary>
public sealed partial class PublicTransitSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private ChatSystem _chat = default!;

    public const string DockTag = "DockTransit";

    public bool Enabled { get; private set; }
    public float FlyTime { get; private set; } = 145f;

    private int _counter;
    private readonly List<EntityUid> _stops = new();
    private TimeSpan _nextSetupCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationTransitComponent, ComponentStartup>(OnStopStartup);
        SubscribeLocalEvent<StationTransitComponent, ComponentShutdown>(OnStopShutdown);
        SubscribeLocalEvent<TransitShuttleComponent, ComponentStartup>(OnBusStartup);
        SubscribeLocalEvent<TransitShuttleComponent, EntityUnpausedEvent>(OnBusUnpaused);
        SubscribeLocalEvent<TransitShuttleComponent, FTLCompletedEvent>(OnBusArrival);
        SubscribeLocalEvent<TransitShuttleComponent, FTLTagEvent>(OnBusTag);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => { _counter = 0; _stops.Clear(); });

        Enabled = _cfg.GetCVar(SerenityCCVars.PublicTransit);
        FlyTime = _cfg.GetCVar(SerenityCCVars.PublicTransitFlyTime);
        _cfg.OnValueChanged(SerenityCCVars.PublicTransit, SetEnabled);
        _cfg.OnValueChanged(SerenityCCVars.PublicTransitFlyTime, value => FlyTime = value);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(SerenityCCVars.PublicTransit, SetEnabled);
    }

    /// <summary>
    /// The bus always looks for a DockTransit-tagged airlock so mappers can steer it away from the main docks.
    /// </summary>
    private void OnBusTag(EntityUid uid, TransitShuttleComponent component, ref FTLTagEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.Tag = DockTag;
    }

    private void OnStopStartup(EntityUid uid, StationTransitComponent component, ComponentStartup args)
    {
        if (Transform(uid).MapID == _ticker.DefaultMap && !_stops.Contains(uid))
            _stops.Add(uid);
    }

    private void OnStopShutdown(EntityUid uid, StationTransitComponent component, ComponentShutdown args)
    {
        _stops.Remove(uid);
    }

    private void OnBusStartup(EntityUid uid, TransitShuttleComponent component, ComponentStartup args)
    {
        EnsureComp<PreventPilotComponent>(uid);
        EnsureComp<ProtectedGridComponent>(uid);
    }

    private void OnBusUnpaused(EntityUid uid, TransitShuttleComponent component, ref EntityUnpausedEvent args)
    {
        component.NextTransfer += args.PausedTime;
    }

    private void OnBusArrival(EntityUid uid, TransitShuttleComponent comp, ref FTLCompletedEvent args)
    {
        var wait = _cfg.GetCVar(SerenityCCVars.PublicTransitWaitTime);
        var destination = Exists(comp.NextStation) ? MetaData(comp.NextStation).EntityName : "?";
        AnnounceOnBus(uid, Loc.GetString("public-transit-arrival", ("destination", destination), ("waittime", (int) wait)));
    }

    private void AnnounceOnBus(EntityUid bus, string text)
    {
        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent>();
        while (consoles.MoveNext(out var console, out _))
        {
            if (Transform(console).GridUid != bus)
                continue;

            _chat.TrySendInGameICMessage(console, text, InGameICChatType.Speak, ChatTransmitRange.HideChat,
                hideLog: true, checkRadioPrefix: false, ignoreActionBlocker: true);
        }
    }

    private bool TryGetNextStop(out EntityUid stop)
    {
        stop = EntityUid.Invalid;

        _stops.RemoveAll(uid => !Exists(uid));
        if (_stops.Count == 0)
            return false;

        if (_counter >= _stops.Count)
            _counter = 0;

        stop = _stops[_counter++];
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (Enabled && curTime >= _nextSetupCheck)
        {
            _nextSetupCheck = curTime + TimeSpan.FromSeconds(10);
            if (_ticker.RunLevel == GameRunLevel.InRound && _stops.Count > 0 && !BusExists())
                SetupPublicTransit();
        }

        var query = EntityQueryEnumerator<TransitShuttleComponent, ShuttleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var shuttle))
        {
            if (comp.NextTransfer > curTime)
                continue;

            if (!Exists(comp.NextStation) && !(TryGetNextStop(out var replacement) && (comp.NextStation = replacement) != EntityUid.Invalid))
            {
                // Nowhere to go; try again later rather than FTLing into nothing.
                comp.NextTransfer = curTime + TimeSpan.FromSeconds(30);
                continue;
            }

            var destination = MetaData(comp.NextStation).EntityName;
            AnnounceOnBus(uid, Loc.GetString("public-transit-departure", ("destination", destination), ("flytime", (int) FlyTime)));
            _shuttles.FTLToDock(uid, shuttle, comp.NextStation, hyperspaceTime: FlyTime, priorityTag: DockTag);

            if (TryGetNextStop(out var next))
                comp.NextStation = next;

            comp.NextTransfer += TimeSpan.FromSeconds(FlyTime + _cfg.GetCVar(SerenityCCVars.PublicTransitWaitTime));
        }
    }

    private bool BusExists()
    {
        var query = EntityQueryEnumerator<TransitShuttleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid))
                return true;
        }

        return false;
    }

    private void SetEnabled(bool value)
    {
        Enabled = value;
        if (value)
            return;

        var query = AllEntityQuery<TransitShuttleComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            QueueDel(uid);
        }
    }

    private void SetupPublicTransit()
    {
        if (BusExists())
            return;

        var stagingMap = _map.CreateMap(out var stagingId);
        var busPath = new ResPath(_cfg.GetCVar(SerenityCCVars.PublicTransitBusMap));

        if (!_loader.TryLoadGrid(stagingId, busPath, out var grid))
        {
            Log.Error($"Public transit: failed to load bus {busPath}.");
            Del(stagingMap);
            return;
        }

        var bus = grid.Value.Owner;
        if (!TryComp<ShuttleComponent>(bus, out var shuttle) || !TryGetNextStop(out var first))
        {
            Log.Warning("Public transit: bus has no ShuttleComponent or there are no stops; deleting it.");
            Del(bus);
            Del(stagingMap);
            return;
        }

        var transit = EnsureComp<TransitShuttleComponent>(bus);
        transit.NextStation = first;
        _shuttles.FTLToDock(bus, shuttle, first, hyperspaceTime: 5f, priorityTag: DockTag);
        transit.NextTransfer = _timing.CurTime + TimeSpan.FromSeconds(_cfg.GetCVar(SerenityCCVars.PublicTransitWaitTime));

        // The first cached stop is where we start, so advance once more for the first real trip.
        if (TryGetNextStop(out var second))
            transit.NextStation = second;

        var despawn = EnsureComp<TimedDespawnComponent>(stagingMap);
        despawn.Lifetime = 15f;
    }
}
