using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Sterility;

/// <summary>
/// On a surgery step: completing it dirties the tools used and the surgeon's gloves, and exposes the patient to
/// whatever dirt is already on them. Every step should have one (a test checks), so a step can be made messier
/// or cleaner than the default by overriding the fields.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryStepDirtinessComponent : Component
{
    /// <summary>
    /// Dirt added to each surgical tool / to the gloves. Null uses the config default.
    /// </summary>
    [DataField]
    public float? ToolDirt;

    [DataField]
    public float? GloveDirt;
}
