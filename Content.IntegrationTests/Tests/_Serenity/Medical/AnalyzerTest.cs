using System.Collections.Generic;
using Content.Server._Serenity.Medical.Analyzer;
using Content.Server._Serenity.Medical.Sterility;
using Content.Server.Medical.Components;
using Content.Shared._Serenity.Medical.Analyzer;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared._Serenity.Skills;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.MedicalScanner;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
[TestOf(typeof(AnalyzerReadoutSystem))]
public sealed class AnalyzerTest
{
    private static HealthAnalyzerUiState FullState()
    {
        return new HealthAnalyzerUiState
        {
            Chemicals = new List<(string ReagentId, FixedPoint2 Quantity, FixedPoint2 StomachQuantity)>
            {
                ("Water", FixedPoint2.New(5), FixedPoint2.Zero),
            },
        };
    }

    /// <summary>
    /// A basic analyzer shows no chemicals and no wound detail, only whether the patient is infected. An advanced one
    /// also shows chemicals, every wound with its tier, and whether an antibiotic is holding the infection back.
    /// </summary>
    [Test]
    public async Task BasicAndAdvancedReadoutsDiffer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var readout = entMan.System<AnalyzerReadoutSystem>();
        var infections = entMan.System<InfectionSystem>();
        var wounds = entMan.System<SharedWoundSystem>();
        var effects = entMan.System<StatusEffectsSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            var basic = entMan.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var advanced = entMan.SpawnEntity("AdvancedHealthAnalyzer", mapData.GridCoords);
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var comp = entMan.GetComponent<WoundComponent>(patient);

            // A healthy patient: nothing infected, nothing to list
            var healthyBasic = FullState();
            readout.ApplyReadout(basic, patient, ref healthyBasic);
            Assert.That(healthyBasic.InfectionDetected, Is.Null);
            Assert.That(healthyBasic.Chemicals, Is.Null, "a basic analyzer doesn't show chemicals");
            Assert.That(healthyBasic.Wounds, Is.Null);

            // Injured and infected
            wounds.AddWound(patient, comp, new WoundEntry("BluntFracture", 2));
            infections.TryInfect(patient, comp);

            var basicState = FullState();
            readout.ApplyReadout(basic, patient, ref basicState);
            Assert.That(basicState.InfectionDetected, Is.True, "even a basic analyzer detects the infection");
            Assert.That(basicState.Chemicals, Is.Null);
            Assert.That(basicState.Wounds, Is.Null, "and nothing more about it");

            var advancedState = FullState();
            readout.ApplyReadout(advanced, patient, ref advancedState);
            Assert.That(advancedState.InfectionDetected, Is.True);
            Assert.That(advancedState.Chemicals, Has.Count.EqualTo(1), "an advanced analyzer keeps the chemicals");
            Assert.That(advancedState.Wounds, Is.Not.Null);
            Assert.That(advancedState.Wounds!.Wounds, Has.Count.GreaterThanOrEqualTo(2), "the fracture and the infection");
            Assert.That(advancedState.Wounds.InfectionSuppressed, Is.False);

            // An antibiotic shows up as a suppressed infection on the advanced readout
            Assert.That(effects.TryAddStatusEffectDuration(patient, InfectionSystem.AntibioticEffect, TimeSpan.FromMinutes(1)));
            var suppressed = FullState();
            readout.ApplyReadout(advanced, patient, ref suppressed);
            Assert.That(suppressed.Wounds!.InfectionSuppressed, Is.True);

            // The printed report carries the wounds and the infection for an advanced analyzer only
            var advancedReport = new FormattedMessage();
            readout.AppendReportSection(advancedReport, suppressed);
            var advancedText = advancedReport.ToString();
            Assert.That(advancedText, Does.Contain(loc.GetString("health-analyzer-report-section-wounds")));
            Assert.That(advancedText, Does.Contain(loc.GetString("wound-infection-1")));
            Assert.That(advancedText, Does.Contain(FormattedMessage.FromMarkupOrThrow(loc.GetString("health-analyzer-infection-detected")).ToString()));
            Assert.That(advancedText, Does.Contain(loc.GetString("health-analyzer-infection-suppressed")));

            var basicReport = new FormattedMessage();
            readout.AppendReportSection(basicReport, basicState);
            Assert.That(basicReport.IsEmpty, Is.True, "a basic analyzer's report has no wound section");

            foreach (var uid in new[] { basic, advanced, patient })
            {
                entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Anyone can scan with a basic analyzer; scanning with an advanced one needs the Diagnostics skill (first aid's
    /// Medicine isn't enough), and refuses to start without it.
    /// </summary>
    [Test]
    public async Task AdvancedAnalyzerNeedsTheDiagnosticsSkill()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var skills = entMan.System<SharedSkillSystem>();
        var doAfters = entMan.System<SharedDoAfterSystem>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var basic = entMan.SpawnEntity("HandheldHealthAnalyzer", mapData.GridCoords);
            var advanced = entMan.SpawnEntity("AdvancedHealthAnalyzer", mapData.GridCoords);

            bool Scan(EntityUid analyzer)
            {
                var args = new DoAfterArgs(entMan, user, 1f, new HealthAnalyzerDoAfterEvent(), analyzer, target: patient, used: analyzer);
                return doAfters.TryStartDoAfter(args);
            }

            Assert.That(Scan(basic), Is.True, "anyone can use a basic analyzer");
            Assert.That(Scan(advanced), Is.False, "an untrained user can't start an advanced scan");

            // First aid isn't enough
            Assert.That(skills.GrantSkill(user, "Medicine"), Is.True);
            Assert.That(Scan(advanced), Is.False, "Medicine alone isn't enough");

            Assert.That(skills.GrantSkill(user, "Diagnostics"), Is.True);
            Assert.That(Scan(advanced), Is.True, "a trained user can");

            foreach (var uid in new[] { user, patient, basic, advanced })
            {
                entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A PDA's MedTek program is an advanced analyzer (so it gets the detailed readout and takes the Diagnostics
    /// skill to use); a PDA without it isn't, and the readout follows the program being added or removed.
    /// </summary>
    [Test]
    public async Task MedTekPdaIsAnAdvancedAnalyzer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var medical = entMan.SpawnEntity("MedicalPDA", mapData.GridCoords);
            var plain = entMan.SpawnEntity("AssistantPDA", mapData.GridCoords);

            Assert.That(entMan.HasComponent<HealthAnalyzerComponent>(medical));
            Assert.That(entMan.HasComponent<AdvancedHealthAnalyzerComponent>(medical), "the MedTek PDA is advanced");
            Assert.That(entMan.HasComponent<AdvancedHealthAnalyzerComponent>(plain), Is.False);

            entMan.DeleteEntity(medical);
            entMan.DeleteEntity(plain);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The "antibiotic is holding the infection back" note only appears when there is an infection to hold back, not
    /// for a patient on an antibiotic with ordinary wounds.
    /// </summary>
    [Test]
    public async Task SuppressionNoteNeedsAnInfection()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var readout = entMan.System<AnalyzerReadoutSystem>();
        var infections = entMan.System<InfectionSystem>();
        var wounds = entMan.System<SharedWoundSystem>();
        var effects = entMan.System<StatusEffectsSystem>();

        await server.WaitAssertion(() =>
        {
            var advanced = entMan.SpawnEntity("AdvancedHealthAnalyzer", mapData.GridCoords);
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var comp = entMan.GetComponent<WoundComponent>(patient);

            wounds.AddWound(patient, comp, new WoundEntry("SlashLaceration", 2));
            Assert.That(effects.TryAddStatusEffectDuration(patient, InfectionSystem.AntibioticEffect, TimeSpan.FromMinutes(1)));

            var uninfected = FullState();
            readout.ApplyReadout(advanced, patient, ref uninfected);
            Assert.That(uninfected.Wounds, Is.Not.Null);
            Assert.That(uninfected.Wounds!.InfectionSuppressed, Is.False, "no infection, nothing to hold back");

            Assert.That(infections.TryInfect(patient, comp));
            var infected = FullState();
            readout.ApplyReadout(advanced, patient, ref infected);
            Assert.That(infected.Wounds!.InfectionSuppressed, Is.True);

            entMan.DeleteEntity(advanced);
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }
}
