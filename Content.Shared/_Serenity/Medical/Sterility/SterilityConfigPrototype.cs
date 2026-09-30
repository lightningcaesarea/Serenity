using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Sterility;

/// <summary>
/// All tuning for surgical sterility: how fast tools and gloves get dirty, how dirty surgery has to be before
/// it harms the patient, and how much harm. Dirtiness runs 0-100 on every tool and pair of gloves.
/// </summary>
[Prototype]
public sealed partial class SterilityConfigPrototype : IPrototype
{
    public const string DefaultId = "DefaultSterility";

    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Dirtiness never exceeds this.
    /// </summary>
    [DataField]
    public float MaxDirtiness = 100f;

    /// <summary>
    /// Dirt a surgery step adds to each surgical tool used, and to the surgeon's gloves, unless the step sets its own.
    /// </summary>
    [DataField]
    public float StepToolDirt = 4f;

    [DataField]
    public float StepGloveDirt = 4f;

    /// <summary>
    /// Counted as dirt when the surgeon wears no gloves / no mask.
    /// </summary>
    [DataField]
    public float MissingGlovesDirt = 25f;

    [DataField]
    public float MissingMaskDirt = 10f;

    /// <summary>
    /// Dirt added per patient, other than the one being operated on, whose DNA is on the tools or gloves.
    /// </summary>
    [DataField]
    public float CrossContaminationDirt = 60f;

    /// <summary>
    /// Total dirtiness at which surgery starts to harm the patient.
    /// </summary>
    [DataField]
    public float SepsisThreshold = 40f;

    /// <summary>
    /// Harm per step once over the threshold: <c>SepsisBaseDamage + excess² / SepsisDamageDivisor</c>, capped at
    /// <see cref="SepsisMaxDamage"/>. Damage is of the type <see cref="SepsisDamageType"/>.
    /// </summary>
    [DataField]
    public float SepsisBaseDamage = 1f;

    [DataField]
    public float SepsisDamageDivisor = 200f;

    [DataField]
    public float SepsisMaxDamage = 12f;

    [DataField]
    public ProtoId<DamageTypePrototype> SepsisDamageType = "Poison";

    /// <summary>
    /// Damage done to a patient by an operation with the given total dirtiness; 0 at or below the threshold.
    /// </summary>
    public float SepsisDamage(float totalDirtiness)
    {
        if (totalDirtiness <= SepsisThreshold)
            return 0f;

        var excess = totalDirtiness - SepsisThreshold;
        return Math.Min(SepsisBaseDamage + excess * excess / SepsisDamageDivisor, SepsisMaxDamage);
    }
}
