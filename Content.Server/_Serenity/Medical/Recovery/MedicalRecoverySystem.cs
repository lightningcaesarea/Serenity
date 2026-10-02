using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Serenity.Medical.Recovery;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Salvage.Fulton;
using Content.Shared.Trigger;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Serenity.Medical.Recovery;

/// <summary>
/// Drives the medical recovery implant. Three trigger paths:
/// 1. Auto: fires when the carrier dies off the round's default station map.
/// 2. Handheld beacon: a paramedic manually recalls a dead carrier within range, same map.
/// 3. Recovery console: a fixed station device whose UI lists Critical OR Dead carriers within
///    range on the same map and recovers the chosen one to its linked pad.
/// All three run the same deploy → fulton transit sequence; see MedicalRecoveryComponent.
/// </summary>
public sealed partial class MedicalRecoverySystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<RadioChannelPrototype> MedicalChannel = "Medical";

    private static readonly SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/Items/Mining/fultext_deploy.ogg");

    /// <summary>
    /// The vanilla fulton lands within 1.5 tiles of its beacon; anything inside this counts as
    /// having arrived and is snapped onto the pad itself.
    /// </summary>
    private const float ArrivalTolerance = 2.5f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedicalRecoveryOnTriggerComponent, TriggerEvent>(OnDeathTrigger);
        SubscribeLocalEvent<MedicalRecoveryBeaconComponent, UseInHandEvent>(OnBeaconUsed);
        SubscribeLocalEvent<MedicalRecoveryConsoleComponent, MapInitEvent>(OnConsoleMapInit);

        Subs.BuiEvents<MedicalRecoveryConsoleComponent>(MedicalRecoveryConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>((uid, comp, _) => UpdateConsoleUi((uid, comp)));
            subs.Event<MedicalRecoveryConsoleScanMessage>((uid, comp, _) => UpdateConsoleUi((uid, comp)));
            subs.Event<MedicalRecoveryConsoleRecoverMessage>(OnConsoleRecover);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var recoveries = EntityQueryEnumerator<MedicalRecoveryInProgressComponent>();
        while (recoveries.MoveNext(out var carrier, out var progress))
        {
            switch (progress.Phase)
            {
                case MedicalRecoveryPhase.Deploying when now >= progress.PhaseEnd:
                    Launch((carrier, progress));
                    break;
                // The vanilla FultonSystem moves the body and removes FultonedComponent when the
                // timer runs out; it also removes it early if the body is stuffed in a container.
                case MedicalRecoveryPhase.InTransit when !HasComp<FultonedComponent>(carrier):
                    Land((carrier, progress));
                    break;
            }
        }

        var consoles = EntityQueryEnumerator<MedicalRecoveryConsoleComponent>();
        while (consoles.MoveNext(out var uid, out var console))
        {
            if (now < console.NextUiUpdate || !_ui.IsUiOpen(uid, MedicalRecoveryConsoleUiKey.Key))
                continue;

            UpdateConsoleUi((uid, console));
        }
    }

    private void OnDeathTrigger(Entity<MedicalRecoveryOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (!TryComp<SubdermalImplantComponent>(ent.Owner, out var implant) || implant.ImplantedEntity is not { Valid: true } carrier)
            return;

        if (!TryComp<MobStateComponent>(carrier, out var mobState) || mobState.CurrentState != MobState.Dead)
            return;

        if (!TryComp<MedicalRecoveryComponent>(ent.Owner, out var recovery) || recovery.Used || HasComp<MedicalRecoveryInProgressComponent>(carrier))
            return;

        // Auto-trigger only fires off the main station map. On-map deaths are left for a
        // paramedic to handle manually with a recovery beacon or console instead.
        if (Transform(carrier).MapID == _ticker.DefaultMap)
            return;

        args.Handled = true;

        if (FindAnyRecoveryPad() is not { } pad)
        {
            Announce(carrier, Loc.GetString("medical-recovery-announce-no-pad", ("name", Name(carrier))));
            return;
        }

        StartRecovery(carrier, (ent.Owner, recovery), pad, console: null);
    }

    private void OnBeaconUsed(Entity<MedicalRecoveryBeaconComponent> ent, ref UseInHandEvent args)
    {
        if (_timing.CurTime < ent.Comp.NextUseTime)
            return;

        ent.Comp.NextUseTime = _timing.CurTime + ent.Comp.UseCooldown;
        args.Handled = true;

        // Handheld beacon stays Dead-only — it's a body-recovery tool. Delivers to any pad on
        // the default map since it isn't linked to one specifically.
        Candidate? nearest = null;
        foreach (var candidate in FindRecoverable(_transform.GetMapCoordinates(args.User), ent.Comp.Range, allowCritical: false))
        {
            if (nearest == null || candidate.Distance < nearest.Value.Distance)
                nearest = candidate;
        }

        if (nearest is not { } target)
        {
            _audio.PlayPvs(ent.Comp.NoTargetSound, ent.Owner);
            _popup.PopupEntity(Loc.GetString("medical-recovery-beacon-no-target"), args.User, args.User);
            return;
        }

        if (FindAnyRecoveryPad() is not { } pad)
        {
            _audio.PlayPvs(ent.Comp.NoTargetSound, ent.Owner);
            _popup.PopupEntity(Loc.GetString("medical-recovery-no-pad"), args.User, args.User);
            return;
        }

        _audio.PlayPvs(ent.Comp.ActivateSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("medical-recovery-beacon-target-found", ("target", target.Carrier)), args.User, args.User);
        StartRecovery(target.Carrier, target.Implant, pad, console: null);
    }

    private void OnConsoleMapInit(Entity<MedicalRecoveryConsoleComponent> ent, ref MapInitEvent args)
    {
        GetLinkedPad(ent);
    }

    private void OnConsoleRecover(Entity<MedicalRecoveryConsoleComponent> ent, ref MedicalRecoveryConsoleRecoverMessage args)
    {
        var target = GetEntity(args.Target);

        if (GetLinkedPad(ent) is not { } pad)
        {
            _popup.PopupEntity(Loc.GetString("medical-recovery-console-no-pad-linked"), ent.Owner, args.Actor);
            UpdateConsoleUi(ent);
            return;
        }

        // Re-validate against a fresh scan rather than trusting the client's list.
        Candidate? chosen = null;
        foreach (var candidate in FindRecoverable(_transform.GetMapCoordinates(ent.Owner), ent.Comp.Range, allowCritical: true))
        {
            if (candidate.Carrier == target)
                chosen = candidate;
        }

        if (chosen is not { } found)
        {
            _popup.PopupEntity(Loc.GetString("medical-recovery-console-target-lost"), ent.Owner, args.Actor);
            UpdateConsoleUi(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("medical-recovery-console-target-found", ("target", found.Carrier)), ent.Owner, args.Actor);
        StartRecovery(found.Carrier, found.Implant, pad, ent.Owner);
    }

    /// <summary>
    /// Returns the console's pad, re-linking to the first pad on its grid if it has none or the
    /// old one was destroyed — so building the pad after the console still works.
    /// </summary>
    private EntityUid? GetLinkedPad(Entity<MedicalRecoveryConsoleComponent> ent)
    {
        if (ent.Comp.LinkedPad is { } linked && !TerminatingOrDeleted(linked) && HasComp<MedicalRecoveryPadComponent>(linked))
            return linked;

        ent.Comp.LinkedPad = null;

        if (Transform(ent.Owner).GridUid is not { } grid)
            return null;

        var query = EntityQueryEnumerator<MedicalRecoveryPadComponent, TransformComponent>();
        while (query.MoveNext(out var padUid, out _, out var padXform))
        {
            if (padXform.GridUid != grid)
                continue;

            ent.Comp.LinkedPad = padUid;
            return padUid;
        }

        return null;
    }

    private void UpdateConsoleUi(Entity<MedicalRecoveryConsoleComponent> ent)
    {
        ent.Comp.NextUiUpdate = _timing.CurTime + ent.Comp.UiUpdateInterval;

        var targets = new List<MedicalRecoveryTargetEntry>();
        foreach (var candidate in FindRecoverable(_transform.GetMapCoordinates(ent.Owner), ent.Comp.Range, allowCritical: true))
        {
            targets.Add(new MedicalRecoveryTargetEntry(
                GetNetEntity(candidate.Carrier),
                Name(candidate.Carrier),
                candidate.Dead,
                candidate.Distance));
        }

        targets.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var active = new List<MedicalRecoveryActiveEntry>();
        var query = EntityQueryEnumerator<MedicalRecoveryInProgressComponent>();
        while (query.MoveNext(out var carrier, out var progress))
        {
            if (progress.Console != ent.Owner)
                continue;

            active.Add(new MedicalRecoveryActiveEntry(
                GetNetEntity(carrier),
                Name(carrier),
                progress.Phase,
                progress.PhaseStart,
                progress.PhaseEnd));
        }

        var state = new MedicalRecoveryConsoleState(targets, active, GetLinkedPad(ent) != null, ent.Comp.Range);
        _ui.SetUiState(ent.Owner, MedicalRecoveryConsoleUiKey.Key, state);
    }

    private void RefreshConsole(EntityUid? console)
    {
        if (TryComp<MedicalRecoveryConsoleComponent>(console, out var comp))
            UpdateConsoleUi((console.Value, comp));
    }

    private record struct Candidate(EntityUid Carrier, Entity<MedicalRecoveryComponent> Implant, bool Dead, float Distance);

    /// <summary>
    /// Every carrier of an unused recovery implant within range, on the same map as
    /// <paramref name="origin"/>, who is Dead (always eligible) or Critical (only when
    /// <paramref name="allowCritical"/> is set) and isn't already being recovered.
    /// </summary>
    private List<Candidate> FindRecoverable(MapCoordinates origin, float range, bool allowCritical)
    {
        var found = new List<Candidate>();

        var query = EntityQueryEnumerator<MedicalRecoveryComponent, SubdermalImplantComponent>();
        while (query.MoveNext(out var implantUid, out var recovery, out var implant))
        {
            if (recovery.Used || implant.ImplantedEntity is not { Valid: true } carrier)
                continue;

            if (HasComp<MedicalRecoveryInProgressComponent>(carrier) || !TryComp<MobStateComponent>(carrier, out var mobState))
                continue;

            var dead = mobState.CurrentState == MobState.Dead;
            if (!dead && !(allowCritical && mobState.CurrentState == MobState.Critical))
                continue;

            var targetCoords = _transform.GetMapCoordinates(carrier);
            if (targetCoords.MapId != origin.MapId)
                continue;

            var distance = (targetCoords.Position - origin.Position).Length();
            if (distance > range)
                continue;

            found.Add(new Candidate(carrier, (implantUid, recovery), dead, distance));
        }

        return found;
    }

    private void StartRecovery(EntityUid carrier, Entity<MedicalRecoveryComponent> implant, EntityUid pad, EntityUid? console)
    {
        var now = _timing.CurTime;
        var progress = EnsureComp<MedicalRecoveryInProgressComponent>(carrier);
        progress.Implant = implant.Owner;
        progress.Pad = pad;
        progress.Console = console;
        progress.Phase = MedicalRecoveryPhase.Deploying;
        progress.PhaseStart = now;
        progress.PhaseEnd = now + implant.Comp.DeployDuration;

        _popup.PopupEntity(Loc.GetString("medical-recovery-deploying"), carrier, PopupType.Medium);
        Announce(console ?? carrier, Loc.GetString("medical-recovery-announce-launch", ("name", Name(carrier))));

        RefreshConsole(console);
    }

    /// <summary>
    /// End of the deploy phase: the implant is spent and a fulton balloon goes up. From here the
    /// vanilla FultonSystem owns the trip — balloon effect, launch sound, and the move to the pad.
    /// </summary>
    private void Launch(Entity<MedicalRecoveryInProgressComponent> ent)
    {
        var (carrier, progress) = ent;

        if (TerminatingOrDeleted(progress.Pad))
        {
            Fail(ent, "medical-recovery-announce-no-pad");
            return;
        }

        // Same conditions the vanilla fulton refuses; failing here keeps the implant unspent.
        if (_container.IsEntityInContainer(carrier) || Transform(carrier).Anchored)
        {
            Fail(ent, "medical-recovery-announce-interrupted");
            return;
        }

        if (TryComp<MedicalRecoveryComponent>(progress.Implant, out var recovery))
        {
            recovery.Used = true;
            Dirty(progress.Implant, recovery);
        }

        var now = _timing.CurTime;
        var transit = recovery?.TransitDuration ?? TimeSpan.FromSeconds(10);

        // A salvage fulton already on the body would point at the wrong beacon.
        RemComp<FultonedComponent>(carrier);
        var fultoned = AddComp<FultonedComponent>(carrier);
        fultoned.Beacon = progress.Pad;
        fultoned.FultonDuration = transit;
        fultoned.NextFulton = now + transit;
        fultoned.Removeable = false;
        Dirty(carrier, fultoned);

        _audio.PlayPvs(DeploySound, carrier);
        _popup.PopupEntity(Loc.GetString("medical-recovery-launching"), carrier, PopupType.LargeCaution);

        progress.Phase = MedicalRecoveryPhase.InTransit;
        progress.PhaseStart = now;
        progress.PhaseEnd = now + transit;

        RefreshConsole(progress.Console);
    }

    private void Land(Entity<MedicalRecoveryInProgressComponent> ent)
    {
        var (carrier, progress) = ent;
        var name = Name(carrier);

        RemCompDeferred<MedicalRecoveryInProgressComponent>(carrier);
        RefreshConsole(progress.Console);

        if (TerminatingOrDeleted(progress.Pad))
        {
            Announce(progress.Console ?? carrier, Loc.GetString("medical-recovery-announce-no-pad", ("name", name)));
            return;
        }

        var padCoords = _transform.GetMapCoordinates(progress.Pad);
        var carrierCoords = _transform.GetMapCoordinates(carrier);
        if (padCoords.MapId != carrierCoords.MapId || (padCoords.Position - carrierCoords.Position).Length() > ArrivalTolerance)
        {
            Announce(progress.Console ?? carrier, Loc.GetString("medical-recovery-announce-interrupted", ("name", name)));
            return;
        }

        _transform.SetCoordinates(carrier, Transform(progress.Pad).Coordinates);
        Announce(progress.Console ?? carrier, Loc.GetString("medical-recovery-announce-arrived",
            ("name", name),
            ("location", GetLocationName(progress.Pad))));
    }

    private void Fail(Entity<MedicalRecoveryInProgressComponent> ent, string announcement)
    {
        var console = ent.Comp.Console;
        var name = Name(ent.Owner);

        RemCompDeferred<MedicalRecoveryInProgressComponent>(ent.Owner);
        Announce(console ?? ent.Owner, Loc.GetString(announcement, ("name", name)));
        RefreshConsole(console);
    }

    /// <summary>
    /// Destination for the auto-trigger and handheld-beacon paths, which aren't linked to a
    /// specific pad — whichever pad is found first on the default map.
    /// </summary>
    private EntityUid? FindAnyRecoveryPad()
    {
        var query = EntityQueryEnumerator<MedicalRecoveryPadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID == _ticker.DefaultMap)
                return uid;
        }

        return null;
    }

    /// <summary>
    /// What to call the place a pad is in: the name of the grid it stands on ("NT Serenity"), or "the station"
    /// for a pad that isn't on a named grid.
    /// </summary>
    public string GetLocationName(EntityUid pad)
    {
        if (Transform(pad).GridUid is { } grid && !string.IsNullOrWhiteSpace(Name(grid)))
            return Name(grid);

        return Loc.GetString("medical-recovery-location-fallback");
    }

    private void Announce(EntityUid source, string message)
    {
        _radio.SendRadioMessage(source, message, MedicalChannel, source);
    }
}
