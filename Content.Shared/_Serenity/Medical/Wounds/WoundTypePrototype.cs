using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Defines a wound type: which damage causes it and how hard a single hit has to be for each tier.
/// </summary>
[Prototype]
public sealed partial class WoundTypePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<WoundCategoryPrototype> Category;

    /// <summary>
    /// The damage types that cause this wound, each with a weight. Empty for wounds of a category that damage never
    /// causes (<see cref="WoundCategoryPrototype.DamageTriggered"/> false). A hit's <em>score</em> is the sum of
    /// <c>weight × damage dealt</c> over these types, so several damage types in one hit combine, and one wound
    /// type can respond to more than one kind of damage.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<DamageTypePrototype>, float> Damage = new();

    /// <summary>
    /// Hit score needed for tier 1, 2 and 3.
    /// </summary>
    [DataField]
    public float[] Thresholds = [];

    /// <summary>
    /// The score of a hit that dealt <paramref name="delta"/>: damage of a type this wound ignores adds nothing.
    /// </summary>
    public float Score(DamageSpecifier delta)
    {
        var score = 0f;
        foreach (var (type, weight) in Damage)
        {
            if (delta.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                score += weight * amount.Float();
        }

        return score;
    }

    /// <summary>
    /// The tier (1-based) a hit score reaches, or 0 if it is below the first threshold.
    /// </summary>
    public int TierFor(float score)
    {
        var tier = 0;
        for (var i = 0; i < Thresholds.Length; i++)
        {
            if (score >= Thresholds[i])
                tier = i + WoundsConstants.TierIndexToTierOffset;
        }

        return tier;
    }
}
