using Content.Shared.Mobs.Systems;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Serenity.Medical.Wounds;

/// <summary>
/// Server-side natural regen for <see cref="WoundComponent"/>: once per
/// <see cref="WoundConfigPrototype.RegenTickSeconds"/> interval, walks every
/// wound whose <see cref="WoundEntry.NextDecayTime"/> has elapsed and drops
/// its tier by one. Wounds whose tier falls below 1 are removed. Dead mobs
/// are skipped — rotting flesh does not heal, and this matches the spec's
/// "untreated wounds resolve over minutes" framing for live patients.
/// </summary>
public sealed partial class WoundRegenSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedWoundSystem _wound = default!;

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(_proto.Index<WoundConfigPrototype>(WoundConfigPrototype.DefaultId).RegenTickSeconds);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(_proto.Index<WoundConfigPrototype>(WoundConfigPrototype.DefaultId).RegenTickSeconds);

        var query = EntityQueryEnumerator<WoundComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ActiveWounds.Count == 0)
                continue;

            if (_mobState.IsDead(uid))
                continue;

            DecayWounds(uid, comp);
        }
    }

    /// <summary>
    /// Drops every due wound by one tier, removes tier-1 wounds whose timer
    /// expired, and fires <see cref="WoundsClearedEvent"/> for each category
    /// that fully empties. Public so integration tests can drive the decay
    /// step without having to advance sim time past the per-tick throttle.
    /// </summary>
    public void DecayWounds(EntityUid uid, WoundComponent comp)
    {
        var changed = false;
        var clearedCategory = false;
        var now = _timing.CurTime;

        for (var i = comp.ActiveWounds.Count - 1; i >= 0; i--)
        {
            var wound = comp.ActiveWounds[i];
            if (wound.NextDecayTime > now)
                continue;

            // Some wounds (infection) don't heal by themselves; their own system moves them along
            if (_proto.TryIndex(wound.WoundTypeId, out var woundType)
                && _proto.TryIndex(woundType.Category, out var category)
                && !category.Decays)
            {
                continue;
            }

            if (wound.Tier <= 1)
            {
                _wound.RemoveWoundAt(comp, i);
                changed = true;
                if (IsCategoryCleared(comp, wound))
                    clearedCategory = true;
                continue;
            }

            wound.Tier -= 1;
            wound.NextDecayTime = now + _proto.Index(comp.Config).GetTierDecayDuration(wound.Tier);
            changed = true;
        }

        if (!changed)
            return;

        Dirty(uid, comp);
        if (clearedCategory)
            RaiseLocalEvent(uid, new WoundsClearedEvent());
    }

    /// <summary>
    /// True if no remaining wound shares the just-removed entry's category.
    /// Orphan entries whose prototype is gone are treated as cleared so stale
    /// state doesn't suppress the refresh event.
    /// </summary>
    private bool IsCategoryCleared(WoundComponent comp, WoundEntry removed)
    {
        if (!_proto.TryIndex(removed.WoundTypeId, out var removedProto))
            return true;

        foreach (var wound in comp.ActiveWounds)
        {
            if (_proto.TryIndex(wound.WoundTypeId, out var proto) && proto.Category == removedProto.Category)
                return false;
        }

        return true;
    }
}
