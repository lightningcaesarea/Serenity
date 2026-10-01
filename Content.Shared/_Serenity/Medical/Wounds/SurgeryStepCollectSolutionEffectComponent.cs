using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Surgery step that drains a reagent from the patient into a container the surgeon holds. The step can only be
/// performed while a held container has room for it (checked by <c>SurgeryStepCollectSolutionSystem</c>), and
/// completing it pours <see cref="Quantity"/> of <see cref="Reagent"/> into that container, as much as fits.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepCollectSolutionEffectComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ReagentPrototype> Reagent;

    [DataField]
    public FixedPoint2 Quantity = FixedPoint2.New(10);
}
