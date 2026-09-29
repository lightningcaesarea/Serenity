using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Serenity.Medical.Recovery;
using Content.Shared.IdentityManagement;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Trigger;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Serenity.Medical.Recovery;

/// <summary>
/// Drives the medical recovery implant. Three trigger paths:
/// 1. Auto: fires when the carrier dies off the round's default station map.
/// 2. Handheld beacon: a paramedic manually recalls a dead carrier within range, same map.
/// 3. Recovery console: a fixed station device that recalls Critical OR Dead carriers within
///    range on the same map, delivering specifically to its linked pad rather than any pad.
/// See MedicalRecoveryComponent for the full design rationale.
/// </summary>
public sealed partial class MedicalRecoverySystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<RadioChannelPrototype> MedicalChannel = "Medical";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedicalRecoveryOnTriggerComponent, TriggerEvent>(OnDeathTrigger);
        SubscribeLocalEvent<MedicalRecoveryBeaconComponent, UseInHandEvent>(OnBeaconUsed);
        SubscribeLocalEvent<MedicalRecoveryConsoleComponent, MapInitEvent>(OnConsoleMapInit);
        SubscribeLocalEvent<MedicalRecoveryConsoleComponent, ActivateInWorldEvent>(OnConsoleActivated);
    }

    private void OnDeathTrigger(Entity<MedicalRecoveryOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (!TryComp<SubdermalImplantComponent>(ent.Owner, out var implant) || implant.ImplantedEntity is not { Valid: true } carrier)
            return;

        if (!TryComp<MobStateComponent>(carrier, out var mobState) || mobState.CurrentState != MobState.Dead)
            return;

        if (!TryComp<MedicalRecoveryComponent>(ent.Owner, out var recovery) || recovery.Used)
            return;

        // Auto-trigger only fires off the main station map. On-map deaths are left for a
        // paramedic to handle manually with a recovery beacon or console instead.
        if (Transform(carrier).MapID == _ticker.DefaultMap)
            return;

        args.Handled = true;
        StartRecovery(carrier, ent.Owner, recovery, target: null);
    }

    private void OnBeaconUsed(Entity<MedicalRecoveryBeaconComponent> ent, ref UseInHandEvent args)
    {
        if (_timing.CurTime < ent.Comp.NextUseTime)
            return;

        ent.Comp.NextUseTime = _timing.CurTime + ent.Comp.UseCooldown;

        var userCoords = _transform.GetMapCoordinates(args.User);

        // Handheld beacon stays Dead-only — it's a body-recovery tool. Delivers to any pad on
        // the default map since it isn't linked to one specifically.
        if (TryFindNearestRecoverable(userCoords, ent.Comp.Range, allowCritical: false) is not { } target)
        {
            _audio.PlayPvs(ent.Comp.NoTargetSound, ent.Owner);
            _popup.PopupEntity(Loc.GetString("medical-recovery-beacon-no-target"), args.User, args.User);
            return;
        }

        if (!TryFindImplant(target.Owner, out var implant))
            return;

        _audio.PlayPvs(ent.Comp.ActivateSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString("medical-recovery-beacon-target-found", ("target", target.Owner)), args.User, args.User);
        StartRecovery(target.Owner, implant!.Value, target.Comp, target: null);
    }

    private void OnConsoleMapInit(Entity<MedicalRecoveryConsoleComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.LinkedPad != null)
            return;

        var grid = Transform(ent.Owner).GridUid;
        if (grid == null)
            return;

        var query = EntityQueryEnumerator<MedicalRecoveryPadComponent>();
        while (query.MoveNext(out var padUid, out _))
        {
            if (Transform(padUid).GridUid == grid)
            {
                ent.Comp.LinkedPad = padUid;
                return;
            }
        }
    }

    private void OnConsoleActivated(Entity<MedicalRecoveryConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (_timing.CurTime < ent.Comp.NextUseTime)
            return;

        ent.Comp.NextUseTime = _timing.CurTime + ent.Comp.UseCooldown;

        var consoleCoords = _transform.GetMapCoordinates(ent.Owner);

        // Console can pull back Critical patients too, not just corpses — a deliberate rescue
        // tool, not just a body-recovery one.
        if (TryFindNearestRecoverable(consoleCoords, ent.Comp.Range, allowCritical: true) is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("medical-recovery-console-no-target"), ent.Owner, args.User);
            return;
        }

        if (!TryFindImplant(target.Owner, out var implant))
            return;

        if (ent.Comp.LinkedPad is not { Valid: true } pad || !Exists(pad))
        {
            _popup.PopupEntity(Loc.GetString("medical-recovery-console-no-pad-linked"), ent.Owner, args.User);
            return;
        }

        _popup.PopupEntity(Loc.GetString("medical-recovery-console-target-found", ("target", target.Owner)), ent.Owner, args.User);
        StartRecovery(target.Owner, implant!.Value, target.Comp, target: Transform(pad).Coordinates);
    }

    /// <summary>
    /// Finds the nearest unused MedicalRecoveryComponent holder within range, on the same map as
    /// <paramref name="origin"/>, whose MobState matches Dead (always eligible) or Critical (only
    /// when <paramref name="allowCritical"/> is set).
    /// </summary>
    private Entity<MedicalRecoveryComponent>? TryFindNearestRecoverable(MapCoordinates origin, float range, bool allowCritical)
    {
        Entity<MedicalRecoveryComponent>? best = null;
        var bestDistance = float.MaxValue;

        var query = EntityQueryEnumerator<MedicalRecoveryComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var recovery, out var mobState))
        {
            if (recovery.Used)
                continue;

            var eligible = mobState.CurrentState == MobState.Dead
                || (allowCritical && mobState.CurrentState == MobState.Critical);
            if (!eligible)
                continue;

            var targetCoords = _transform.GetMapCoordinates(uid);
            if (targetCoords.MapId != origin.MapId)
                continue;

            var distance = (targetCoords.Position - origin.Position).Length();
            if (distance > range || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = (uid, recovery);
        }

        return best;
    }

    private void StartRecovery(EntityUid carrier, EntityUid implant, MedicalRecoveryComponent recovery, EntityCoordinates? target)
    {
        recovery.Used = true;
        Dirty(implant, recovery);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg"), carrier);
        _popup.PopupEntity(Loc.GetString("medical-recovery-launching"), carrier, PopupType.LargeCaution);

        var carrierName = Identity.Name(carrier, EntityManager);
        Announce(carrier, Loc.GetString("medical-recovery-announce-launch", ("name", carrierName)));

        Timer.Spawn(recovery.RecoveryDelay, () => CompleteRecovery(carrier, carrierName, target));
    }

    private void CompleteRecovery(EntityUid carrier, string carrierName, EntityCoordinates? target)
    {
        if (!Exists(carrier) || Deleted(carrier))
            return;

        var destination = target ?? FindAnyRecoveryPad();
        if (destination is not { } padCoords)
        {
            // No landing pad mapped anywhere — can't complete, but the implant already burned
            // its single use. Notify medical so someone can map a pad and admins can intervene.
            Announce(carrier, Loc.GetString("medical-recovery-announce-no-pad", ("name", carrierName)));
            return;
        }

        _transform.SetCoordinates(carrier, padCoords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg"), carrier);
        Announce(carrier, Loc.GetString("medical-recovery-announce-arrived", ("name", carrierName)));
    }

    /// <summary>
    /// Fallback destination for the auto-trigger and handheld-beacon paths, which aren't linked
    /// to a specific pad — delivers to whichever pad is found first on the default map.
    /// </summary>
    private EntityCoordinates? FindAnyRecoveryPad()
    {
        var query = EntityQueryEnumerator<MedicalRecoveryPadComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (Transform(uid).MapID == _ticker.DefaultMap)
                return Transform(uid).Coordinates;
        }

        return null;
    }

    private void Announce(EntityUid source, string message)
    {
        _radio.SendRadioMessage(source, message, MedicalChannel, source);
    }

    /// <summary>
    /// Finds the medical recovery implant entity installed in a given mob by scanning all implants for one
    /// carrying a MedicalRecoveryComponent (there should only ever be one).
    /// </summary>
    private bool TryFindImplant(EntityUid carrier, out EntityUid? implant)
    {
        implant = null;

        var query = EntityQueryEnumerator<MedicalRecoveryComponent, SubdermalImplantComponent>();
        while (query.MoveNext(out var uid, out _, out var subdermal))
        {
            if (subdermal.ImplantedEntity == carrier)
            {
                implant = uid;
                return true;
            }
        }

        return false;
    }
}
