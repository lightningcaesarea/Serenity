using Content.Shared.Damage;
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
        if (args.WasModified<WoundTypePrototype>())
            CacheWoundTypes();
    }

    private void CacheWoundTypes()
    {
        _woundTypes.Clear();
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

        foreach (var (damageType, amount) in args.DamageDelta.DamageDict)
        {
            var amountFloat = amount.Float();
            if (amountFloat <= 0)
                continue;

            var typeStr = damageType;

            // Track bleed source damage type for display
            if (typeStr == DamageTypeIds.Slash || typeStr == DamageTypeIds.Piercing)
            {
                changed |= _display.UpdateBleedSource(comp, typeStr, amountFloat);
                continue; // Bleeding wounds are display-only, not wound entries
            }

            // Check spike thresholds for fracture/burn wound types.
            // Divide by ThresholdMultiplier so a multiplier > 1 effectively raises thresholds.
            var effectiveAmount = amountFloat / ClampThresholdMultiplier(comp.ThresholdMultiplier);

            foreach (var woundProto in _woundTypes)
            {
                if (woundProto.DamageType != typeStr)
                    continue;

                var tier = GetTierFromSpike(woundProto, effectiveAmount);
                if (tier <= 0)
                    continue;

                changed |= ApplyWound(comp, woundProto, tier);
            }
        }

        if (!changed)
            return;

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsDamagedEvent());
    }

    private static int GetTierFromSpike(WoundTypePrototype proto, float amount)
    {
        var tier = 0;
        for (var i = 0; i < proto.Thresholds.Length; i++)
        {
            if (amount >= proto.Thresholds[i])
                tier = i + WoundsConstants.TierIndexToTierOffset;
        }
        return tier;
    }

    private bool ApplyWound(WoundComponent comp, WoundTypePrototype proto, int tier)
    {
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
                wound.NextDecayTime = _timing.CurTime + GetTierDecayDuration(tier);
                return true;
            }

            if (wound.Tier < WoundsConstants.MaxWoundTier)
                return false; // Existing wound is same or higher tier, no action

            // At tier 3, fall through to stack a new wound
            break;
        }

        // Cap stacking at 3 wounds per type
        if (existingCount >= WoundsConstants.MaxStackedWoundsPerType)
            return false;

        // Create new wound entry
        comp.ActiveWounds.Add(new WoundEntry(proto.ID, tier)
        {
            NextDecayTime = _timing.CurTime + GetTierDecayDuration(tier),
        });
        return true;
    }

    /// <summary>
    /// Seconds-until-next-drop budget for a wound at the given tier. Returns
    /// <see cref="TimeSpan.Zero"/> for invalid tiers so the caller can treat
    /// them as already-expired.
    /// </summary>
    public static TimeSpan GetTierDecayDuration(int tier)
    {
        return tier switch
        {
            3 => TimeSpan.FromSeconds(WoundsConstants.Tier3DecaySeconds),
            2 => TimeSpan.FromSeconds(WoundsConstants.Tier2DecaySeconds),
            1 => TimeSpan.FromSeconds(WoundsConstants.Tier1DecaySeconds),
            _ => TimeSpan.Zero,
        };
    }

    /// <summary>
    /// Removes all wounds of the given category. Used by surgery.
    /// </summary>
    public void ClearWoundsByCategory(EntityUid uid, WoundCategory category, WoundComponent? comp = null)
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
    public int GetWorstTier(WoundComponent comp, WoundCategory category)
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
