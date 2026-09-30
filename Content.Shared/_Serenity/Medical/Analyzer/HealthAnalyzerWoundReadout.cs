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
    /// True if an antibiotic is holding an infection back.
    /// </summary>
    public bool InfectionSuppressed;
}
