using Content.Shared._Serenity.Medical.Wounds;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Medical.Analyzer;

/// <summary>
/// The wound detail an advanced health analyzer adds to its readout. Basic analyzers send none.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerWoundReadout
{
    /// <summary>
    /// Every wound and bleed the patient has, with its tier, worst first.
    /// </summary>
    public List<WoundDisplayInfo> Wounds = new();

    /// <summary>
    /// True if the patient has an infection and an antibiotic is holding it back.
    /// </summary>
    public bool InfectionSuppressed;
}
