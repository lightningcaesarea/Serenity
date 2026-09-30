using System.Linq;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared._Starlight;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

/// <summary>
/// The wound-treating surgeries are only offered to a patient who has that kind of wound, and finishing the
/// last step resolves it.
/// </summary>
[TestFixture]
public sealed class WoundSurgeryTest
{
    private static readonly (EntProtoId Surgery, ProtoId<WoundCategoryPrototype> Category, ProtoId<WoundTypePrototype> Wound)[] Cases =
    [
        ("SurgeryTreatFractures", WoundCategoryIds.Fracture, "BluntFracture"),
        ("SurgeryTreatBurns", WoundCategoryIds.Burn, "HeatBurn"),
        ("SurgeryTreatLacerations", WoundCategoryIds.Laceration, "SlashLaceration"),
        ("SurgeryTreatPunctures", WoundCategoryIds.Puncture, "PiercingPuncture"),
    ];

    [Test]
    public async Task SurgeriesAreOfferedForAndClearTheirWounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();
        var body = entMan.System<SharedBodySystem>();
        var singletons = entMan.System<StarlightEntitySystem>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var woundComp = entMan.GetComponent<WoundComponent>(patient);
            var torso = body.GetBodyChildrenOfType(patient, BodyPartType.Torso).First().Id;

            Assert.Multiple(() =>
            {
                foreach (var (surgery, category, woundType) in Cases)
                {
                    Assert.That(singletons.TryGetSingleton(surgery, out var surgeryUid), $"{surgery} is missing");

                    bool Offered()
                    {
                        var ev = new SurgeryValidEvent(patient, torso);
                        entMan.EventBus.RaiseLocalEvent(surgeryUid, ref ev);
                        return !ev.Cancelled;
                    }

                    Assert.That(Offered(), Is.False, $"{surgery} shouldn't be offered to a patient without {category} wounds");

                    wounds.AddWound(patient, woundComp, new WoundEntry(woundType, 1));
                    Assert.That(Offered(), Is.True, $"{surgery} should be offered to a patient with a {category} wound");

                    // Another kind of wound doesn't count: the condition is per category
                    wounds.ClearWoundsByCategory(patient, category, woundComp);
                    Assert.That(Offered(), Is.False, $"{surgery} should stop being offered once the {category} wound is gone");

                    // Finishing the last step clears the wound
                    wounds.AddWound(patient, woundComp, new WoundEntry(woundType, 2));
                    var surgeryComp = entMan.GetComponent<SurgeryComponent>(surgeryUid);
                    var lastStep = surgeryComp.Steps.Last();
                    Assert.That(singletons.TryGetSingleton(lastStep, out var stepUid));

                    var stepEv = new SurgeryStepEvent(patient, patient, torso, [])
                    {
                        StepProto = lastStep,
                        SurgeryProto = surgery,
                    };
                    entMan.EventBus.RaiseLocalEvent(stepUid, ref stepEv);

                    Assert.That(wounds.GetWorstTier(woundComp, category), Is.EqualTo(0), $"finishing {surgery} should clear the {category} wound");
                }
            });

            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every wound-treating step is wired to a real category, and each surgery's last step clears the category
    /// its condition asks for (otherwise a patient could be offered a surgery that never helps).
    /// </summary>
    [Test]
    public async Task SurgeryDataIsConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var factory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (surgery, category, _) in Cases)
                {
                    var surgeryProto = proto.Index(surgery);
                    Assert.That(surgeryProto.TryGetComponent<SurgeryWoundConditionComponent>(out var condition, factory), $"{surgery} has no wound condition");
                    Assert.That(condition?.Category.Id, Is.EqualTo(category.Id), $"{surgery} condition category");

                    Assert.That(surgeryProto.TryGetComponent<SurgeryComponent>(out var surgeryComp, factory));
                    var last = proto.Index(surgeryComp!.Steps.Last());
                    Assert.That(last.TryGetComponent<SurgeryStepClearWoundEffectComponent>(out var clear, factory), $"{surgery}'s last step clears no wounds");
                    Assert.That(clear?.Category.Id, Is.EqualTo(category.Id), $"{surgery}'s last step clears the wrong category");

                    foreach (var step in surgeryComp.Steps)
                    {
                        Assert.That(proto.HasIndex(step), $"{surgery} uses unknown step {step}");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
