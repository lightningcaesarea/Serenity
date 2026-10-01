using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HealthExaminable;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Serenity.Medical.Pain;

/// <summary>
/// Turns wounds and bleeding into a pain value, lets painkiller status effects mask it, and applies
/// the effects of whatever pain is left: slowed movement, dropped items, a HUD alert and examine text.
/// Painkillers only hide pain; the wounds underneath are untouched and the pain returns when the drug fades.
/// </summary>
public abstract partial class SharedPainSystem : EntitySystem
{
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] protected INetManager _net = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private WoundDisplaySystem _woundDisplay = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    // Reused by every recalculation (the system is single-threaded) to avoid allocating per mob per tick.
    private readonly Dictionary<ProtoId<WoundCategoryPrototype>, float> _categoryPain = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PainComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PainComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PainComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<PainComponent, WoundsDamagedEvent>(OnWoundsDamaged);
        SubscribeLocalEvent<PainComponent, WoundsClearedEvent>(OnWoundsCleared);
        SubscribeLocalEvent<PainComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<PainComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<PainComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<PainComponent, HealthBeingExaminedEvent>(OnHealthExamined);

        SubscribeLocalEvent<PainkillerStatusEffectComponent, StatusEffectAppliedEvent>(OnPainkillerApplied);
        SubscribeLocalEvent<PainkillerStatusEffectComponent, StatusEffectRemovedEvent>(OnPainkillerRemoved);
    }

    private void OnStartup(Entity<PainComponent> ent, ref ComponentStartup args)
    {
        Recalculate(ent.AsNullable());
    }

    private void OnShutdown(Entity<PainComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }

    private void OnRejuvenate(Entity<PainComponent> ent, ref RejuvenateEvent args)
    {
        // Wound clearing on rejuvenate may run after us, so zero pain directly.
        SetPain(ent, 0f, 0f, false);
    }

    private void OnWoundsDamaged(EntityUid uid, PainComponent comp, WoundsDamagedEvent args)
    {
        Recalculate((uid, comp));
    }

    private void OnWoundsCleared(EntityUid uid, PainComponent comp, WoundsClearedEvent args)
    {
        Recalculate((uid, comp));
    }

    private void OnPainkillerApplied(Entity<PainkillerStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        RecalculateTarget(args.Target, null);
    }

    private void OnPainkillerRemoved(Entity<PainkillerStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        // The effect entity may still be listed while this event runs, so leave it out of the sum.
        RecalculateTarget(args.Target, ent.Owner);
    }

    private void RecalculateTarget(EntityUid target, EntityUid? excludeEffect)
    {
        if (TryComp<PainComponent>(target, out var pain))
            Recalculate((target, pain), excludeEffect);
    }

    private void OnDamageChanged(Entity<PainComponent> ent, ref DamageChangedEvent args)
    {
        if (_timing.ApplyingState || !_net.IsServer || !args.DamageIncreased)
            return;

        var chance = _proto.Index(ent.Comp.Config).DropChance(ent.Comp.Level);

        if (chance <= 0f || !_random.Prob(chance))
            return;

        if (TryComp<HandsComponent>(ent, out var hands))
            _hands.TryDrop((ent.Owner, hands));
    }

    private void OnRefreshSpeed(Entity<PainComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var multiplier = _proto.Index(ent.Comp.Config).SpeedMultiplier(ent.Comp.Level);
        if (multiplier < 1f)
            args.ModifySpeed(multiplier);
    }

    private void OnHandleState(Entity<PainComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnHealthExamined(Entity<PainComponent> ent, ref HealthBeingExaminedEvent args)
    {
        if (ent.Comp.Level >= PainLevel.Moderate)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(
                Loc.GetString($"pain-examine-{ent.Comp.Level.ToString().ToLowerInvariant()}", ("target", ent.Owner)));
        }

        if (ent.Comp.Masked)
        {
            args.Message.PushNewline();
            args.Message.AddMarkupOrThrow(Loc.GetString("pain-examine-masked", ("target", ent.Owner)));
        }
    }

    /// <summary>
    /// Recomputes raw and effective pain from the mob's current wounds, bleeding and active painkillers.
    /// Server-side only; clients receive the result as component state.
    /// </summary>
    /// <param name="ent">The mob to recompute.</param>
    /// <param name="excludeEffect">A status effect that is being removed and shouldn't be counted.</param>
    public void Recalculate(Entity<PainComponent?> ent, EntityUid? excludeEffect = null)
    {
        if (_timing.ApplyingState || !_net.IsServer || !Resolve(ent, ref ent.Comp, false))
            return;

        var pain = (ent.Owner, ent.Comp);
        var config = _proto.Index(ent.Comp.Config);

        if (_mobState.IsDead(ent.Owner))
        {
            SetPain(pain, 0f, 0f, false);
            return;
        }

        // Pain from each wound category (bleeding is derived from the bloodstream, the rest from wound entries)
        var total = 0f;
        _categoryPain.Clear();

        if (TryComp<WoundComponent>(ent.Owner, out var wounds))
        {
            if (TryComp<BloodstreamComponent>(ent.Owner, out var blood))
            {
                var bleed = config.WoundWeight(WoundCategoryIds.Bleeding, _woundDisplay.GetBleedTier(wounds, blood));
                _categoryPain[WoundCategoryIds.Bleeding] = bleed;
                total += bleed;
            }

            foreach (var wound in wounds.ActiveWounds)
            {
                if (!_proto.TryIndex(wound.WoundTypeId, out var proto))
                    continue;

                var weight = config.WoundWeight(proto.Category, wound.Tier) * config.LocationMultiplier(wound.Location);
                _categoryPain[proto.Category] = _categoryPain.GetValueOrDefault(proto.Category) + weight;
                total += weight;
            }
        }

        var raw = Math.Min(total, config.MaxPain);

        if (raw <= 0f)
        {
            SetPain(pain, 0f, 0f, false);
            return;
        }

        // Numbness (the trait, anesthesia) removes all pain outright. PainNumbnessSystem already owns the
        // apply/remove events for that effect, so changes here are picked up by the periodic server recalculation.
        if (HasActiveEffect<PainNumbnessStatusEffectComponent>(ent.Owner, excludeEffect))
        {
            SetPain(pain, raw, 0f, true);
            return;
        }

        var masked = 0f;
        if (_statusEffects.TryEffectsWithComp<PainkillerStatusEffectComponent>(ent.Owner, out var effects))
        {
            var painkillers = new List<(float Strength, float ScopePain)>();
            foreach (var effect in effects)
            {
                if (effect.Owner == excludeEffect)
                    continue;

                var scopePain = raw;
                if (effect.Comp1.Scope is { } scope)
                {
                    scopePain = 0f;
                    foreach (var category in scope)
                    {
                        scopePain += _categoryPain.GetValueOrDefault(category);
                    }
                }

                painkillers.Add((effect.Comp1.Strength, scopePain));
            }

            // Strongest works in full; the rest are heavily discounted so drug stacking isn't a strategy.
            painkillers.Sort((a, b) => b.Strength.CompareTo(a.Strength));
            for (var i = 0; i < painkillers.Count; i++)
            {
                var (strength, scopePain) = painkillers[i];
                var factor = i == 0 ? 1f : config.SecondaryPainkillerFactor;
                masked += Math.Min(strength * factor, scopePain);
            }
        }

        masked = Math.Min(masked, raw);
        SetPain(pain, raw, raw - masked, masked >= config.MaskedReportThreshold);
    }

    private bool HasActiveEffect<T>(EntityUid uid, EntityUid? exclude) where T : IComponent
    {
        if (!_statusEffects.TryEffectsWithComp<T>(uid, out var effects))
            return false;

        foreach (var effect in effects)
        {
            if (effect.Owner != exclude)
                return true;
        }

        return false;
    }

    private void SetPain(Entity<PainComponent> ent, float raw, float effective, bool masked)
    {
        if (_timing.ApplyingState || !_net.IsServer)
            return;

        var comp = ent.Comp;
        var oldLevel = comp.Level;
        var newLevel = _proto.Index(comp.Config).LevelFor(effective);

        var changed = oldLevel != newLevel
            || comp.Masked != masked
            || Math.Abs(comp.RawPain - raw) > 0.5f
            || Math.Abs(comp.EffectivePain - effective) > 0.5f;

        if (!changed)
            return;

        comp.RawPain = raw;
        comp.EffectivePain = effective;
        comp.Level = newLevel;
        comp.Masked = masked;
        Dirty(ent);

        if (oldLevel == newLevel)
            return;

        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (newLevel == PainLevel.None)
            _alerts.ClearAlert(ent.Owner, comp.Alert);
        else
            _alerts.ShowAlert(ent.Owner, comp.Alert, (short) (newLevel - PainLevel.Mild));

        RaiseLocalEvent(ent, new PainLevelChangedEvent(oldLevel, newLevel, masked));
    }
}
