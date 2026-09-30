using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Analyzer;

/// <summary>
/// Marks a health analyzer as advanced: its readout also shows the patient's metabolising chemicals and the
/// detail of their wounds and infection, where a basic analyzer shows the damage breakdown only. Using one takes
/// the Diagnostics skill (a do-after rule on that skill, see skills.yml).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AdvancedHealthAnalyzerComponent : Component;
