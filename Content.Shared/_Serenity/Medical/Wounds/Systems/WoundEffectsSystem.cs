using Content.Shared.Alert;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HealthExaminable;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

/// <summary>
/// Applies the gameplay effects of active wounds, per wound category as configured by the mob's
/// <see cref="WoundConfigPrototype"/>: movement slow, dropping held items, HUD alerts and examine text.
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<WoundComponent, WoundsDamagedEvent>(OnWoundsDamaged);
        SubscribeLocalEvent<WoundComponent, WoundsClearedEvent>(OnWoundsCleared);
        SubscribeLocalEvent<WoundComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WoundComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WoundComponent, HealthBeingExaminedEvent>(OnHealthExamined);
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
            if (effects.SlowTier <= 0 || _wounds.GetWorstTier(comp, category.ID) < effects.SlowTier)
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

        // Severe wounds can make the mob drop what it is holding, at most once per damage event
        var config = _proto.Index(comp.Config);
        foreach (var category in _wounds.Categories)
        {
            var effects = config.EffectsFor(category.ID);
            if (effects.DropTier <= 0 || _wounds.GetWorstTier(comp, category.ID) < effects.DropTier)
                continue;

            if (!_random.Prob(effects.DropChance))
                continue;

            if (TryComp<HandsComponent>(uid, out var hands))
                _hands.TryDrop((uid, hands));

            return;
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
