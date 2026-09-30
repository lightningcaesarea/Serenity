using Content.Server._Serenity.Medical.Sterility;
using Content.Shared._Serenity.Medical.Analyzer;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Serenity.Medical.Analyzer;

/// <summary>
/// Decides how much a health analyzer reveals. A basic analyzer shows the damage breakdown only; an advanced one
/// (<see cref="AdvancedHealthAnalyzerComponent"/>) also shows metabolising chemicals and the detail of wounds and
/// infection. Called by the health analyzer whenever it builds a readout.
/// </summary>
public sealed partial class AnalyzerReadoutSystem : EntitySystem
{
    [Dependency] private WoundDisplaySystem _woundDisplay = default!;
    [Dependency] private InfectionSystem _infection = default!;

    public void ApplyReadout(EntityUid analyzer, EntityUid target, ref HealthAnalyzerUiState state)
    {
        // Every analyzer, basic or advanced, reports that there is an infection, and only that
        state.InfectionDetected = TryComp<WoundComponent>(target, out var woundComp) && _infection.IsInfected(woundComp) ? true : null;

        if (!HasComp<AdvancedHealthAnalyzerComponent>(analyzer))
        {
            state.Chemicals = null;
            state.Wounds = null;
            return;
        }

        if (!TryComp<WoundComponent>(target, out var wounds))
        {
            state.Wounds = null;
            return;
        }

        state.Wounds = new HealthAnalyzerWoundReadout
        {
            Wounds = _woundDisplay.GetWoundDisplayInfo(target, wounds),
            InfectionSuppressed = _infection.HasAntibiotic(target),
        };
    }
}
