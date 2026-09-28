using Content.Shared.Body.Components;
using Content.Shared._Serenity.Medical.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

/// <summary>
/// Builds wound display info for health analyzer and examine text.
/// Handles bleed source tracking and bleed tier calculation.
/// </summary>
public sealed partial class WoundDisplaySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// Updates which damage type (Slash or Piercing) is the bleed source for display.
    /// Prefers Slash on tie.
    /// </summary>
    public bool UpdateBleedSource(WoundComponent comp, string damageType, float amount)
    {
        var prev = comp.BleedSourceDamageType;

        if (prev == null)
        {
            comp.BleedSourceDamageType = damageType;
        }
        else if (damageType == DamageTypeIds.Slash.Id)
        {
            comp.BleedSourceDamageType = DamageTypeIds.Slash.Id;
        }
        else if (prev != DamageTypeIds.Slash.Id)
        {
            comp.BleedSourceDamageType = damageType;
        }

        return comp.BleedSourceDamageType != prev;
    }

    /// <summary>
    /// Gets the bleeding tier based on BleedAmount thresholds.
    /// Returns 0 if not bleeding.
    /// </summary>
    public int GetBleedTier(WoundComponent woundComp, BloodstreamComponent bloodComp)
    {
        var bleed = bloodComp.BleedAmount;
        if (bleed <= 0)
            return 0;

        var tier = 0;
        for (var i = 0; i < woundComp.BleedTierThresholds.Length; i++)
        {
            if (bleed >= woundComp.BleedTierThresholds[i])
                tier = i + WoundsConstants.TierIndexToTierOffset;
        }
        return tier;
    }

    /// <summary>
    /// Builds a list of wound display info for UI purposes.
    /// </summary>
    public List<WoundDisplayInfo> GetWoundDisplayInfo(EntityUid uid, WoundComponent? woundComp = null, BloodstreamComponent? bloodComp = null)
    {
        var result = new List<WoundDisplayInfo>();

        if (!Resolve(uid, ref woundComp, false))
            return result;

        // Bleeding wounds (derived from BleedAmount)
        if (Resolve(uid, ref bloodComp, false))
        {
            var bleedTier = GetBleedTier(woundComp, bloodComp);
            if (bleedTier > 0)
            {
                var source = woundComp.BleedSourceDamageType ?? DamageTypeIds.Slash.Id;
                var locKey = $"wound-bleed-{source.ToLowerInvariant()}-{bleedTier}";
                result.Add(new WoundDisplayInfo(locKey, bleedTier, WoundCategory.Bleeding));
            }
        }

        // Active wounds (fractures and burns)
        foreach (var wound in woundComp.ActiveWounds)
        {
            if (!_proto.TryIndex(wound.WoundTypeId, out var proto))
                continue;

            if (!proto.Names.TryGetValue(wound.Tier, out var name))
                continue;

            var locKey = $"wound-{proto.ID.ToLowerInvariant()}-{wound.Tier}";
            result.Add(new WoundDisplayInfo(locKey, wound.Tier, proto.Category));
        }

        // Sort by tier descending, then category
        result.Sort((a, b) =>
        {
            var tierCmp = b.Tier.CompareTo(a.Tier);
            return tierCmp != 0 ? tierCmp : a.Category.CompareTo(b.Category);
        });

        return result;
    }
}
