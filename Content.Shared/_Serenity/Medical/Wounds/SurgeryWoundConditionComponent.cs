using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Makes a surgery available only while the patient has a wound of the given category, so wound-treating
/// procedures don't clutter the surgery list for patients who have nothing to treat.
/// Wounds aren't tracked per limb, so pair this with a part condition (torso) to offer the surgery in one place.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryWoundConditionComponent : Component
{
    [DataField(required: true)]
    public ProtoId<WoundCategoryPrototype> Category;

    /// <summary>
    /// The patient's worst wound in the category must be at least this tier.
    /// </summary>
    [DataField]
    public int MinTier = 1;
}
