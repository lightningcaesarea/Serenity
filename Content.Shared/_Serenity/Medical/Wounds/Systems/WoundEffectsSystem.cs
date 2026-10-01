using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HealthExaminable;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

/// <summary>
/// Applies the gameplay effects of active wounds, per wound category as configured by the mob's
/// <see cref="WoundConfigPrototype"/>: movement slow, dropping held items, status effects, HUD alerts and examine
/// text. Where a wound is matters: each effect can be limited to some body parts.
/// </summary>
public sealed partial class WoundEffectsSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedWoundSystem _wounds = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WoundComponent, WoundsDamagedEvent>(OnWoundsDamaged);
        SubscribeLocalEvent<WoundComponent, WoundsClearedEvent>(OnWoundsCleared);
        SubscribeLocalEvent<WoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WoundComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WoundComponent, HealthBeingExaminedEvent>(OnHealthExamined);
        SubscribeLocalEvent<WoundComponent, BodyPartHitEvent>(OnBodyPartHit);
    }

    private void OnStartup(EntityUid uid, WoundComponent comp, ComponentStartup args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        RefreshAlerts(uid, comp);
    }

    private void OnRefreshSpeed(EntityUid uid, WoundComponent comp, ref RefreshMovementSpeedModifiersEvent args)
    {
        var config = _proto.Index(comp.Config);

        // The worst slow among the categories whose configured tier has been reached
        var multiplier = 1f;
        foreach (var category in _wounds.Categories)
        {
            var effects = config.EffectsFor(category.ID);
            if (effects.SlowTier <= 0 || _wounds.GetWorstTier(comp, category.ID, effects.SlowLocations) < effects.SlowTier)
                continue;

            multiplier = Math.Min(multiplier, effects.SlowMultiplier);
        }

        if (multiplier < 1f)
            args.ModifySpeed(multiplier);
    }

    private void OnHealthExamined(EntityUid uid, WoundComponent comp, ref HealthBeingExaminedEvent args)
    {
        // Bleeding is already surfaced on the health-examine path by SharedBloodstreamSystem (it has no examine
        // prefix), so only categories that define one get a line here. Self-Aware viewers get a separate
        // clinical readout via SelfAwareSystem and don't go through this event.
        foreach (var category in _wounds.Categories)
        {
            if (category.ExamineLoc is not { } prefix)
                continue;

            var tier = _wounds.GetWorstTier(comp, category.ID);
            if (tier <= 0)
                continue;

            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString($"{prefix}-{tier}", ("target", uid)));
        }

        // Where those injuries are, worst first
        var places = new List<WoundLocation>();
        var wounds = new List<WoundEntry>(comp.ActiveWounds);
        wounds.Sort((a, b) => b.Tier.CompareTo(a.Tier));
        foreach (var wound in wounds)
        {
            if (wound.Location is not { } where || places.Contains(where))
                continue;

            if (!_proto.TryIndex(wound.WoundTypeId, out var type) || !_proto.TryIndex(type.Category, out var category)
                || category.ExamineLoc == null)
                continue;

            places.Add(where);
        }

        if (places.Count == 0)
            return;

        var names = new List<string>(places.Count);
        foreach (var place in places)
        {
            names.Add(Loc.GetString(place.LocKey));
        }

        args.Message.PushNewline();
        args.Message.AddMarkupOrThrow(Loc.GetString("wound-examine-locations",
            ("target", uid),
            ("locations", string.Join(", ", names))));
    }

    private void OnBodyPartHit(Entity<WoundComponent> ent, ref BodyPartHitEvent args)
    {
        var where = Loc.GetString(args.Location.LocKey);
        _popup.PopupEntity(Loc.GetString("wound-hit-location-self", ("location", where)), ent, ent, PopupType.SmallCaution);
        if (args.Origin is { } attacker && attacker != ent.Owner)
            _popup.PopupEntity(Loc.GetString("wound-hit-location-other", ("location", where)), ent, attacker);

        _adminLog.Add(LogType.Damaged, LogImpact.Low,
            $"{ToPrettyString(ent):target} was hit in the {where} by {ToPrettyString(args.Source):weapon}");
    }

    private void OnShutdown(EntityUid uid, WoundComponent comp, ComponentShutdown args)
    {
        foreach (var category in _wounds.Categories)
        {
            if (category.Alert is { } alert)
                _alerts.ClearAlert(uid, alert);
        }
    }

    private void OnWoundsDamaged(EntityUid uid, WoundComponent comp, WoundsDamagedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        RefreshAlerts(uid, comp);

        var config = _proto.Index(comp.Config);
        foreach (var category in _wounds.Categories)
        {
            var effects = config.EffectsFor(category.ID);
            if (effects.StatusEffect is not { } status || effects.StatusTier <= 0)
                continue;

            if (_wounds.GetWorstTier(comp, category.ID, effects.StatusLocations) >= effects.StatusTier)
                _status.TryUpdateStatusEffectDuration(uid, status, TimeSpan.FromSeconds(effects.StatusSeconds));
        }

        // Severe wounds can make the mob drop what it is holding, at most once per damage event
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        foreach (var category in _wounds.Categories)
        {
            var effects = config.EffectsFor(category.ID);
            if (effects.DropTier <= 0)
                continue;

            var wound = _wounds.GetWorstWound(comp, category.ID, effects.DropLocations);
            if (wound == null || wound.Tier < effects.DropTier || !_random.Prob(effects.DropChance))
                continue;

            DropFromSide((uid, hands), wound.Location);
            return;
        }
    }

    /// <summary>
    /// Drops what the hand on the wounded side holds; a wound with no side drops the active hand's item.
    /// </summary>
    private void DropFromSide(Entity<HandsComponent> ent, WoundLocation? location)
    {
        HandLocation? side = location?.Symmetry switch
        {
            BodyPartSymmetry.Left => HandLocation.Left,
            BodyPartSymmetry.Right => HandLocation.Right,
            _ => null,
        };

        if (side == null)
        {
            _hands.TryDrop((ent, ent.Comp));
            return;
        }

        foreach (var (handId, hand) in ent.Comp.Hands)
        {
            if (hand.Location == side)
                _hands.TryDrop((ent, ent.Comp), handId);
        }
    }

    private void OnWoundsCleared(EntityUid uid, WoundComponent comp, WoundsClearedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        RefreshAlerts(uid, comp);
    }

    /// <summary>
    /// Shows or clears each category's HUD alert (one severity per tier) based on current wound state.
    /// </summary>
    public void RefreshAlerts(EntityUid uid, WoundComponent comp)
    {
        foreach (var category in _wounds.Categories)
        {
            if (category.Alert is not { } alert)
                continue;

            var tier = _wounds.GetWorstTier(comp, category.ID);
            if (tier > 0)
                _alerts.ShowAlert(uid, alert, (short) (tier - WoundsConstants.AlertSeverityTierOffset));
            else
                _alerts.ClearAlert(uid, alert);
        }
    }
}
