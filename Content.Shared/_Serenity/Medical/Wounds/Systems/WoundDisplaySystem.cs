using System.Linq;
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
    /// Records <paramref name="damageType"/> as the mob's bleed source if it is one of the config's
    /// <see cref="WoundConfigPrototype.BleedSources"/> and doesn't rank below the source already recorded
    /// (earlier in the list wins). Returns true if the recorded source changed.
    /// </summary>
    public bool UpdateBleedSource(WoundComponent comp, string damageType)
    {
        var sources = _proto.Index(comp.Config).BleedSources;
        var rank = sources.FindIndex(source => source == damageType);
        if (rank < 0)
            return false;

        var previous = comp.BleedSourceDamageType;
        if (previous != null)
        {
            var previousRank = sources.FindIndex(source => source == previous);
            if (previousRank >= 0 && previousRank < rank)
                return false;
        }

        comp.BleedSourceDamageType = damageType;
        return previous != damageType;
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
                var source = woundComp.BleedSourceDamageType ?? _proto.Index(woundComp.Config).BleedSources.FirstOrDefault().Id ?? string.Empty;
                var locKey = $"wound-bleed-{source.ToLowerInvariant()}-{bleedTier}";
                result.Add(new WoundDisplayInfo(locKey, bleedTier, WoundCategoryIds.Bleeding));
            }
        }

        // Active wounds (fractures and burns)
        foreach (var wound in woundComp.ActiveWounds)
        {
            if (!_proto.TryIndex(wound.WoundTypeId, out var proto))
                continue;

            var locKey = $"wound-{proto.ID.ToLowerInvariant()}-{wound.Tier}";
            result.Add(new WoundDisplayInfo(locKey, wound.Tier, proto.Category));
        }

        // Sort by tier descending, then category
        result.Sort((a, b) =>
        {
            var tierCmp = b.Tier.CompareTo(a.Tier);
            return tierCmp != 0 ? tierCmp : string.CompareOrdinal(a.Category, b.Category);
        });

        return result;
    }
}
