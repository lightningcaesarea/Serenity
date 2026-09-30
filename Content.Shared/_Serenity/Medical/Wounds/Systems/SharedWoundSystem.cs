using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared._Serenity.Medical.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

public abstract partial class SharedWoundSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager _proto = default!;
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] private WoundDisplaySystem _display = default!;

    private readonly List<WoundTypePrototype> _woundTypes = new();
    private readonly List<WoundCategoryPrototype> _categories = new();

    /// <summary>
    /// Every wound category, for systems that treat injuries by kind.
    /// </summary>
    public IReadOnlyList<WoundCategoryPrototype> Categories => _categories;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<WoundComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        CacheWoundTypes();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<WoundTypePrototype>() || args.WasModified<WoundCategoryPrototype>())
            CacheWoundTypes();
    }

    private void CacheWoundTypes()
    {
        _woundTypes.Clear();
        _categories.Clear();
        foreach (var category in _proto.EnumeratePrototypes<WoundCategoryPrototype>())
        {
            _categories.Add(category);
        }

        foreach (var proto in _proto.EnumeratePrototypes<WoundTypePrototype>())
        {
            _woundTypes.Add(proto);
        }
    }

    private void OnRejuvenate(EntityUid uid, WoundComponent comp, RejuvenateEvent args)
    {
        if (comp.ActiveWounds.Count == 0 && comp.BleedSourceDamageType is null)
            return;

        comp.ActiveWounds.Clear();
        comp.BleedSourceDamageType = null;
        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsClearedEvent());
    }

    private void OnDamageChanged(EntityUid uid, WoundComponent comp, DamageChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (args.DamageDelta is null || !args.DamageIncreased)
            return;

        var changed = false;

        // Remember which damage type made the mob bleed, for the health analyzer.
        foreach (var (damageType, amount) in args.DamageDelta.DamageDict)
        {
            if (amount > FixedPoint2.Zero)
                changed |= _display.UpdateBleedSource(comp, damageType);
        }

        // Every wound type scores the whole hit, so several damage types in one hit combine and a wound type
        // can respond to more than one kind of damage. Dividing by ThresholdMultiplier means a multiplier
        // above 1 effectively raises the thresholds.
        var multiplier = ClampThresholdMultiplier(comp.ThresholdMultiplier);
        foreach (var woundProto in _woundTypes)
        {
            var tier = woundProto.TierFor(woundProto.Score(args.DamageDelta) / multiplier);
            if (tier > 0)
                changed |= ApplyWound(comp, woundProto, tier);
        }

        if (!changed)
            return;

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsDamagedEvent());
    }

    private bool ApplyWound(WoundComponent comp, WoundTypePrototype proto, int tier)
    {
        var config = _proto.Index(comp.Config);

        // Try to upgrade an existing wound of this type first
        var existingCount = 0;
        foreach (var wound in comp.ActiveWounds)
        {
            if (wound.WoundTypeId != proto.ID)
                continue;

            existingCount++;

            if (wound.Tier < tier)
            {
                wound.Tier = tier;
                wound.NextDecayTime = _timing.CurTime + config.GetTierDecayDuration(tier);
                return true;
            }

            if (wound.Tier < WoundsConstants.MaxWoundTier)
                return false; // Existing wound is same or higher tier, no action

            // At tier 3, fall through to stack a new wound
            break;
        }

        // Cap stacking at 3 wounds per type
        if (existingCount >= config.MaxStackedWoundsPerType)
            return false;

        // Create new wound entry
        comp.ActiveWounds.Add(new WoundEntry(proto.ID, tier)
        {
            NextDecayTime = _timing.CurTime + config.GetTierDecayDuration(tier),
        });
        return true;
    }

    /// <summary>
    /// Removes all wounds of the given category. Used by surgery.
    /// </summary>
    public void ClearWoundsByCategory(EntityUid uid, ProtoId<WoundCategoryPrototype> category, WoundComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        var removed = comp.ActiveWounds.RemoveAll(w =>
        {
            if (!_proto.TryIndex(w.WoundTypeId, out var proto))
                return true;
            return proto.Category == category;
        });

        // Nothing to clear (e.g. a surgery step on a patient without that kind of wound):
        // don't dirty the component or make listeners recompute for no reason.
        if (removed == 0)
            return;

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsClearedEvent());
    }

    /// <summary>
    /// Appends a wound entry directly, syncs it to clients and lets listeners (alerts, movement, pain) react.
    /// Bypasses the damage-spike pipeline in <see cref="OnDamageChanged"/>, so it's the right entry point for
    /// external systems that already decided a wound should exist.
    /// </summary>
    public void AddWound(EntityUid uid, WoundComponent comp, WoundEntry entry)
    {
        comp.ActiveWounds.Add(entry);
        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsDamagedEvent());
    }

    /// <summary>
    /// Appends a wound entry without networking it or notifying listeners. Low-level: the caller must
    /// <see cref="SharedEntitySystem.Dirty"/> the component afterwards. Prefer the overload that takes the entity.
    /// </summary>
    public void AddWound(WoundComponent comp, WoundEntry entry)
    {
        comp.ActiveWounds.Add(entry);
    }

    /// <summary>
    /// Overwrites the bleed source damage type without the precedence logic in
    /// <see cref="WoundDisplaySystem.UpdateBleedSource"/>. For tests and resets.
    /// </summary>
    public void SetBleedSource(WoundComponent comp, string? damageType)
    {
        comp.BleedSourceDamageType = damageType;
    }

    /// <summary>
    /// Removes the wound entry at <paramref name="index"/>. Caller is responsible
    /// for calling <see cref="SharedEntitySystem.Dirty"/> after a batch of changes.
    /// </summary>
    public void RemoveWoundAt(WoundComponent comp, int index)
    {
        comp.ActiveWounds.RemoveAt(index);
    }

    /// <summary>
    /// Multiplies <see cref="WoundComponent.ThresholdMultiplier"/> by the given
    /// factor. Funnel for trait systems that raise or lower a mob's wound
    /// resistance (apply with the trait's factor, remove with its reciprocal).
    /// </summary>
    public void ScaleThresholdMultiplier(EntityUid uid, float factor, WoundComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.ThresholdMultiplier = ClampThresholdMultiplier(comp.ThresholdMultiplier * factor);
    }

    /// <summary>
    /// Keeps a threshold multiplier in a sane range, so a bad YAML value or a stack of trait factors can't make
    /// a mob unwoundable (huge or negative) or divide by zero.
    /// </summary>
    private static float ClampThresholdMultiplier(float multiplier)
    {
        return Math.Clamp(multiplier, WoundsConstants.MinThresholdMultiplier, WoundsConstants.MaxThresholdMultiplier);
    }

    /// <summary>
    /// Gets the highest tier among active wounds of a given category.
    /// Returns 0 if no wounds of that category exist.
    /// </summary>
    public int GetWorstTier(WoundComponent comp, ProtoId<WoundCategoryPrototype> category)
    {
        var worst = 0;
        foreach (var wound in comp.ActiveWounds)
        {
            if (!_proto.TryIndex(wound.WoundTypeId, out var proto))
                continue;

            if (proto.Category == category && wound.Tier > worst)
                worst = wound.Tier;
        }
        return worst;
    }

}
