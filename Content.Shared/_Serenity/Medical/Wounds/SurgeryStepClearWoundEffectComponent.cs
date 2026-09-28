using Content.Shared._Starlight.Medical.Surgery;
using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Surgery step effect that clears every active wound of a given <see cref="WoundCategory"/>
/// from the patient on step completion. Mirrors the pattern of Starlight's native
/// <c>SurgeryStepBleedEffectComponent</c> etc. — add this to a step entity's prototype to let
/// that step (e.g. bone-setting, cauterizing) resolve wounds tracked by the Wounds system.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedSurgerySystem))]
public sealed partial class SurgeryStepClearWoundEffectComponent : Component
{
    [DataField(required: true)]
    public WoundCategory Category;
}
