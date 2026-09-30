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
/// Applies gameplay effects based on active wounds:
/// movement slow, and item drop on hit for tier 3 fractures.
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
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        var burnTier = _wounds.GetWorstTier(comp, WoundCategory.Burn);
        var config = _proto.Index(comp.Config);

        // Fractures and burns at the configured tier slow movement
        if (fractureTier >= config.MovementSlowTier || burnTier >= config.MovementSlowTier)
            args.ModifySpeed(config.MovementSlowMultiplier);
    }

    private void OnHealthExamined(EntityUid uid, WoundComponent comp, ref HealthBeingExaminedEvent args)
    {
        // Bleeding is already surfaced on the health-examine path by SharedBloodstreamSystem,
        // so we only flavor-text fractures and burns here. Self-Aware viewers get a separate
        // clinical readout via SelfAwareSystem and don't go through this event.
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        if (fractureTier > 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(
                Loc.GetString($"wound-examine-fracture-{fractureTier}", ("target", uid)));
        }

        var burnTier = _wounds.GetWorstTier(comp, WoundCategory.Burn);
        if (burnTier > 0)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(
                Loc.GetString($"wound-examine-burn-{burnTier}", ("target", uid)));
        }
    }

    private void OnShutdown(EntityUid uid, WoundComponent comp, ComponentShutdown args)
    {
        _alerts.ClearAlert(uid, comp.FractureAlert);
        _alerts.ClearAlert(uid, comp.BurnAlert);
    }

    private void OnWoundsDamaged(EntityUid uid, WoundComponent comp, WoundsDamagedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        RefreshAlerts(uid, comp);

        // Severe fracture: chance to drop held items on hit
        var config = _proto.Index(comp.Config);
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        if (fractureTier < config.FractureDropTier)
            return;

        if (!_random.Prob(config.FractureDropChance))
            return;

        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        _hands.TryDrop((uid, hands));
    }

    private void OnWoundsCleared(EntityUid uid, WoundComponent comp, WoundsClearedEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        RefreshAlerts(uid, comp);
    }

    /// <summary>
    /// Shows or clears fracture/burn HUD alerts based on current wound state.
    /// </summary>
    public void RefreshAlerts(EntityUid uid, WoundComponent comp)
    {
        var fractureTier = _wounds.GetWorstTier(comp, WoundCategory.Fracture);
        var burnTier = _wounds.GetWorstTier(comp, WoundCategory.Burn);

        if (fractureTier > 0)
            _alerts.ShowAlert(uid, comp.FractureAlert, (short) (fractureTier - WoundsConstants.AlertSeverityTierOffset));
        else
            _alerts.ClearAlert(uid, comp.FractureAlert);

        if (burnTier > 0)
            _alerts.ShowAlert(uid, comp.BurnAlert, (short) (burnTier - WoundsConstants.AlertSeverityTierOffset));
        else
            _alerts.ClearAlert(uid, comp.BurnAlert);
    }
}
