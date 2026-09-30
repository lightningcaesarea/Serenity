using Robust.Shared.GameObjects;

namespace Content.Shared._Serenity.Medical.Sterility;

/// <summary>
/// Raised on the patient (server-side) after a surgery step completes, with how dirty the operation was.
/// Other systems (infection) use it to react to an unsterile operation.
/// </summary>
/// <param name="User">The surgeon.</param>
/// <param name="TotalDirtiness">Tool, glove, contamination and missing-protection dirt combined, before this step added its own.</param>
/// <param name="SepsisDamage">Damage the operation dealt to the patient for being dirty (0 if clean enough).</param>
[ByRefEvent]
public readonly record struct SurgeryStepDirtiedEvent(EntityUid User, float TotalDirtiness, float SepsisDamage);
