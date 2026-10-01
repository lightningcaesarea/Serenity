using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Systems;
using Content.Shared.Rejuvenate;
using Content.Shared.Standing;
using Content.Shared._Serenity.Medical.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

public abstract partial class SharedWoundSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager _proto = default!;
    [Dependency] protected IGameTiming _timing = default!;
    [Dependency] private WoundDisplaySystem _display = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

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
        InitializeHitLocation();
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
        List<(WoundTypePrototype Proto, int Tier)>? hits = null;
        foreach (var woundProto in _woundTypes)
        {
            // Wounds that damage never causes (infection) have nothing to score
            if (woundProto.Damage.Count == 0)
                continue;

            var tier = woundProto.TierFor(woundProto.Score(args.DamageDelta) / multiplier);
            if (tier > 0)
                (hits ??= new()).Add((woundProto, tier));
        }

        // One hit lands in one place, so every wound it causes is in the same location. A weapon hit gets a
        // location even when it is too light to wound. Chosen on the server only: a client's guess would just be
        // replaced by the server's state.
        WoundLocation? location = null;
        var (weapon, aim) = _net.IsServer ? TakePendingHit(comp) : (null, null);
        if (_net.IsServer && (hits != null || weapon != null))
        {
            var biasSource = weapon ?? args.Origin;
            location = PickLocation(uid, _proto.Index(comp.Config), CompOrNull<HitLocationBiasComponent>(biasSource), aim);
        }

        if (hits != null)
        {
            foreach (var (woundProto, tier) in hits)
            {
                changed |= ApplyWound(comp, woundProto, tier, location);
            }
        }

        if (changed)
        {
            Dirty(uid, comp);
            RaiseLocalEvent(uid, new WoundsDamagedEvent());
        }

        if (weapon != null && location != null)
        {
            var hitEv = new BodyPartHitEvent(location, weapon.Value, args.Origin);
            RaiseLocalEvent(uid, ref hitEv);
        }
    }

    private bool ApplyWound(WoundComponent comp, WoundTypePrototype proto, int tier, WoundLocation? location)
    {
        var config = _proto.Index(comp.Config);

        // A wound of this type in the same place gets worse; one in a new place is a separate wound
        var existingCount = 0;
        WoundEntry? sameSpot = null;
        foreach (var wound in comp.ActiveWounds)
        {
            if (wound.WoundTypeId != proto.ID)
                continue;

            existingCount++;
            if (sameSpot == null && Equals(wound.Location, location))
                sameSpot = wound;
        }

        if (sameSpot != null)
        {
            if (sameSpot.Tier < tier)
            {
                sameSpot.Tier = tier;
                sameSpot.NextDecayTime = _timing.CurTime + config.GetTierDecayDuration(tier);
                return true;
            }

            // Already this bad here. Wounds with no location (a mob without a body) can still pile up at the worst tier.
            if (sameSpot.Tier < WoundsConstants.MaxWoundTier || location != null)
                return false;
        }

        // Cap stacking per type
        if (existingCount >= config.MaxStackedWoundsPerType)
            return false;

        // Create new wound entry
        comp.ActiveWounds.Add(new WoundEntry(proto.ID, tier)
        {
            NextDecayTime = _timing.CurTime + config.GetTierDecayDuration(tier),
            Location = location,
        });
        return true;
    }

    /// <summary>
    /// Picks where a hit lands from the config's weighted locations, among the body parts the mob still has.
    /// Null for a mob with no body or a config with no locations. A weapon's <paramref name="bias"/> scales the
    /// weights by body part type, and <paramref name="aimed"/> body part types get the config's aim multiplier.
    /// Uses randomness, so call it on the server.
    /// </summary>
    public WoundLocation? PickLocation(EntityUid uid, WoundConfigPrototype config, HitLocationBiasComponent? bias = null, List<BodyPartType>? aimed = null)
    {
        if (config.HitLocations.Count == 0 || !HasComp<BodyComponent>(uid))
            return null;

        var total = 0f;
        var present = new List<(HitLocationWeight Location, float Weight)>();
        foreach (var candidate in config.HitLocations)
        {
            var weight = candidate.Weight;
            if (bias != null && bias.Multipliers.TryGetValue(candidate.Type, out var multiplier))
                weight *= multiplier;

            if (aimed != null && config.Aim != null && aimed.Contains(candidate.Type))
                weight *= config.Aim.Multiplier;

            if (weight <= 0f || !HasPart(uid, candidate))
                continue;

            present.Add((candidate, weight));
            total += weight;
        }

        // A bias that rules out every part the mob has left falls back to the plain weights
        if (present.Count == 0)
            return bias != null || aimed != null ? PickLocation(uid, config) : null;

        var roll = _random.NextFloat() * total;
        foreach (var (candidate, weight) in present)
        {
            roll -= weight;
            if (roll <= 0f)
                return new WoundLocation(candidate.Type, candidate.Symmetry);
        }

        var last = present[^1].Location;
        return new WoundLocation(last.Type, last.Symmetry);
    }

    private bool HasPart(EntityUid body, HitLocationWeight location)
    {
        foreach (var (_, part) in _body.GetBodyChildrenOfType(body, location.Type))
        {
            if (part.Symmetry == location.Symmetry)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a wound of the given type and tier at a location (or makes an existing one there worse), syncs it and
    /// lets listeners react. Returns false if nothing changed.
    /// </summary>
    public bool TryApplyWound(EntityUid uid, WoundComponent comp, ProtoId<WoundTypePrototype> woundType, int tier, WoundLocation? location)
    {
        if (!_proto.TryIndex(woundType, out var proto) || !ApplyWound(comp, proto, tier, location))
            return false;

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsDamagedEvent());
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
    /// Removes a wound entry, syncs it to clients and lets listeners (alerts, movement, pain) react.
    /// Returns false if the mob doesn't have that entry.
    /// </summary>
    public bool RemoveWound(EntityUid uid, WoundComponent comp, WoundEntry entry)
    {
        if (!comp.ActiveWounds.Remove(entry))
            return false;

        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsClearedEvent());
        return true;
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

    /// <summary>
    /// The worst active wound of a category in one of the given body part types (any part if the list is empty).
    /// A wound with no location (a mob without a body) counts everywhere. Null if there is none.
    /// </summary>
    public WoundEntry? GetWorstWound(WoundComponent comp, ProtoId<WoundCategoryPrototype> category, List<BodyPartType> locations)
    {
        WoundEntry? worst = null;
        foreach (var wound in comp.ActiveWounds)
        {
            if (worst != null && wound.Tier <= worst.Tier)
                continue;

            if (!_proto.TryIndex(wound.WoundTypeId, out var proto) || proto.Category != category)
                continue;

            if (locations.Count > 0 && wound.Location is { } where && !locations.Contains(where.Type))
                continue;

            worst = wound;
        }

        return worst;
    }

    /// <inheritdoc cref="GetWorstWound"/>
    public int GetWorstTier(WoundComponent comp, ProtoId<WoundCategoryPrototype> category, List<BodyPartType> locations)
    {
        return GetWorstWound(comp, category, locations)?.Tier ?? 0;
    }
}
