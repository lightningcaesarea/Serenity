using Content.Server._Serenity.Medical.Sterility;
using Content.Shared._Serenity.Medical.Sterility;
using Content.Shared.Damage.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using System.Linq;
using Robust.Shared.Prototypes;
using Content.Shared._Starlight;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Body.Systems;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
[TestOf(typeof(SterilitySystem))]
public sealed class SterilityTest
{
    private static float Poison(IEntityManager entMan, EntityUid uid)
    {
        return entMan.GetComponent<DamageableComponent>(uid).Damage.DamageDict.TryGetValue("Poison", out var poison) ? poison.Float() : 0f;
    }

    /// <summary>
    /// The harm curve is zero up to the threshold, grows past it, and never exceeds the cap.
    /// </summary>
    [Test]
    public async Task SepsisCurveBehaves()
    {
        await using var pair = await PoolManager.GetServerClient();
        var config = pair.Server.ProtoMan.Index<SterilityConfigPrototype>(SterilityConfigPrototype.DefaultId);

        Assert.Multiple(() =>
        {
            Assert.That(config.SepsisDamage(0f), Is.EqualTo(0f));
            Assert.That(config.SepsisDamage(config.SepsisThreshold), Is.EqualTo(0f), "exactly at the threshold is still safe");
            Assert.That(config.SepsisDamage(config.SepsisThreshold + 1f), Is.GreaterThan(0f));
            Assert.That(config.SepsisDamage(config.SepsisThreshold + 20f), Is.GreaterThan(config.SepsisDamage(config.SepsisThreshold + 10f)));
            Assert.That(config.SepsisDamage(10000f), Is.EqualTo(config.SepsisMaxDamage), "damage is capped");
            Assert.That(config.SepsisThreshold, Is.LessThan(config.MaxDirtiness * 2), "the threshold must be reachable");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every surgery step carries the dirtiness component (inherited from the step base), or its tools would
    /// never get dirty.
    /// </summary>
    [Test]
    public async Task EveryStepDirtiesTools()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var factory = server.EntMan.ComponentFactory;

        Assert.Multiple(() =>
        {
            var steps = 0;
            foreach (var entity in server.ProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (!entity.TryGetComponent<SurgeryStepComponent>(out _, factory))
                    continue;

                steps++;
                Assert.That(entity.TryGetComponent<SurgeryStepDirtinessComponent>(out var dirt, factory), $"{entity.ID} has no SurgeryStepDirtiness");
                if (dirt?.ToolDirt is { } tool)
                    Assert.That(tool, Is.GreaterThanOrEqualTo(0f), $"{entity.ID} tool dirt");
                if (dirt?.GloveDirt is { } glove)
                    Assert.That(glove, Is.GreaterThanOrEqualTo(0f), $"{entity.ID} glove dirt");
            }

            Assert.That(steps, Is.GreaterThan(20), "expected to find the surgery steps");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirtySurgeryHarmsPatientsAndSpreadsContamination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var sterility = entMan.System<SterilitySystem>();
        var inventory = entMan.System<InventorySystem>();

        await server.WaitAssertion(() =>
        {
            var surgeon = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var first = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var second = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var scalpel = entMan.SpawnEntity("Scalpel", mapData.GridCoords);
            entMan.EnsureComponent<DnaComponent>(first).DNA = "DNA-FIRST";
            entMan.EnsureComponent<DnaComponent>(second).DNA = "DNA-SECOND";

            var step = new SurgeryStepDirtinessComponent();
            var tools = new[] { scalpel };

            // A surgeon with no gloves or mask is already exposed to that much dirt, even with a clean scalpel
            Assert.That(sterility.ProcessStep(surgeon, first, tools, step), Is.EqualTo(35f), "no gloves (25) + no mask (10)");
            Assert.That(sterility.GetDirtiness(scalpel), Is.EqualTo(4f), "the step dirties the tool");

            // Properly dressed, a fresh scalpel and gloves are fully clean
            var cleanScalpel = entMan.SpawnEntity("Scalpel", mapData.GridCoords);
            Assert.That(inventory.SpawnItemInSlot(surgeon, "gloves", "ClothingHandsGlovesLatex", force: true));
            Assert.That(inventory.SpawnItemInSlot(surgeon, "mask", "ClothingMaskSterile", force: true));
            Assert.That(sterility.ProcessStep(surgeon, first, new[] { cleanScalpel }, step), Is.EqualTo(0f));
            Assert.That(Poison(entMan, first), Is.EqualTo(0f), "a clean operation does no harm");
            inventory.TryGetSlotEntity(surgeon, "gloves", out var gloves);
            Assert.That(sterility.GetDirtiness(gloves!.Value), Is.EqualTo(4f), "the gloves got dirty too");

            // Keep operating with the same tool and gloves: 8 dirt per step, harmless until the total passes 40
            for (var i = 0; i < 5; i++)
            {
                sterility.ProcessStep(surgeon, first, new[] { cleanScalpel }, step);
            }

            Assert.That(Poison(entMan, first), Is.EqualTo(0f), "still under the threshold");

            sterility.ProcessStep(surgeon, first, new[] { cleanScalpel }, step);
            Assert.That(Poison(entMan, first), Is.GreaterThan(0f), "dirty tools and gloves start poisoning the patient");

            // A different patient is exposed to the first patient's DNA on the same tools
            var before = Poison(entMan, second);
            var total = sterility.ProcessStep(surgeon, second, new[] { cleanScalpel }, step);
            Assert.That(total, Is.GreaterThanOrEqualTo(60f), "cross-contamination adds a lot of dirt");
            Assert.That(Poison(entMan, second), Is.GreaterThan(before));
            Assert.That(sterility.GetDnas(cleanScalpel), Is.SupersetOf(new[] { "DNA-FIRST", "DNA-SECOND" }));

            // Wiping removes dirt and traces, one wipe at a time
            sterility.Clean(cleanScalpel, 1000f, 1);
            Assert.That(sterility.GetDirtiness(cleanScalpel), Is.EqualTo(0f));
            Assert.That(sterility.GetDnas(cleanScalpel), Has.Count.EqualTo(1));
            Assert.That(sterility.RequiresCleaning(cleanScalpel), Is.True, "a trace of another patient is left");
            sterility.Clean(cleanScalpel, 0f, 1);
            Assert.That(sterility.RequiresCleaning(cleanScalpel), Is.False);

            foreach (var uid in new[] { surgeon, first, second, scalpel, cleanScalpel })
            {
                entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The real surgery pipeline reaches the sterility system: completing a step dirties the held surgical tool.
    /// </summary>
    [Test]
    public async Task CompletingAStepDirtiesTheToolInHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var sterility = entMan.System<SterilitySystem>();
        var body = entMan.System<SharedBodySystem>();
        var singletons = entMan.System<StarlightEntitySystem>();

        await server.WaitAssertion(() =>
        {
            var surgeon = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var scalpel = entMan.SpawnEntity("Scalpel", mapData.GridCoords);
            var torso = body.GetBodyChildrenOfType(patient, BodyPartType.Torso).First().Id;

            Assert.That(singletons.TryGetSingleton("SurgeryStepDebrideBurns", out var step));
            var ev = new SurgeryStepCompleteEvent(surgeon, patient, torso, [scalpel])
            {
                StepProto = "SurgeryStepDebrideBurns",
                SurgeryProto = "SurgeryTreatBurns",
                IsFinal = false,
            };
            entMan.EventBus.RaiseLocalEvent(step, ref ev);

            Assert.That(sterility.GetDirtiness(scalpel), Is.GreaterThan(0f));

            foreach (var uid in new[] { surgeon, patient, scalpel })
            {
                entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }
}
