using Content.Server._Serenity.Medical.Sterility;
using Content.Shared._Serenity.Medical.Analyzer;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared.MedicalScanner;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

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
            // Only worth a note when there is an infection to hold back
            InfectionSuppressed = _infection.IsInfected(wounds) && _infection.HasAntibiotic(target),
        };
    }

    /// <summary>
    /// Adds the wound and infection section to a printed health report, matching what the analyzer showed: an
    /// advanced analyzer lists every wound with its tier and location, the infection, and whether an antibiotic is
    /// holding it back. Adds nothing for a basic analyzer (whose readout has no wound detail).
    /// </summary>
    public void AppendReportSection(FormattedMessage message, HealthAnalyzerUiState state)
    {
        if (state.Wounds is not { } readout)
            return;

        message.PushNewline();
        message.PushNewline();
        message.AddMarkupOrThrow($"[head=2][bold]{Loc.GetString("health-analyzer-report-section-wounds")}[/bold][/head]");
        message.PushNewline();

        if (readout.Wounds.Count == 0)
        {
            message.AddMarkupOrThrow(Loc.GetString("health-analyzer-report-no-wounds"));
            message.PushNewline();
        }

        foreach (var wound in readout.Wounds)
        {
            var name = FormattedMessage.EscapeText(Loc.GetString(wound.LocKey));
            var line = wound.Location != null
                ? Loc.GetString("health-analyzer-report-wound-line-located",
                    ("wound", name),
                    ("location", FormattedMessage.EscapeText(Loc.GetString(wound.Location.LocKey))),
                    ("tier", wound.Tier))
                : Loc.GetString("health-analyzer-report-wound-line", ("wound", name), ("tier", wound.Tier));
            message.AddMarkupOrThrow($"- {line}");
            message.PushNewline();
        }

        if (state.InfectionDetected == true)
        {
            message.AddMarkupOrThrow(Loc.GetString("health-analyzer-infection-detected"));
            message.PushNewline();
        }

        if (readout.InfectionSuppressed)
        {
            message.AddMarkupOrThrow(Loc.GetString("health-analyzer-infection-suppressed"));
            message.PushNewline();
        }
    }
}
