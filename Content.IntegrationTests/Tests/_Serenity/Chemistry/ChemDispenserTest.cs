using System.Linq;
using Content.Server.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Chemistry;

[TestFixture]
public sealed class ChemDispenserTest
{
    /// <summary>
    /// The elements that used to come from finite jugs, plus the ones that were already made from energy.
    /// </summary>
    private static readonly ProtoId<ReagentPrototype>[] EnergyReagents =
    [
        "Iron", "Carbon", "Copper", "Hydrogen", "Oxygen", "Sugar", "Silicon", "Phosphorus",
        "Nitrogen", "Sodium", "Sulfur", "Chlorine", "Ethanol", "Aluminium", "Potassium", "Lithium",
        "Fluorine", "Iodine", "Mercury", "Radium",
    ];

    /// <summary>
    /// Every reagent the chemical dispenser offers is made from energy, none of it comes from jugs, and no dispense
    /// costs more than a power cell can hold (a 30u dispense is the largest).
    /// </summary>
    [Test]
    public async Task EveryReagentIsEnergyBasedAndNoJugsAreStocked()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;
        var containers = entMan.System<SharedContainerSystem>();
        var mapData = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var dispenser = entMan.SpawnEntity("ChemDispenser", mapData.GridCoords);
            var generated = entMan.GetComponent<ReagentDispenserComponent>(dispenser).GeneratableReagents;

            Assert.Multiple(() =>
            {
                foreach (var reagent in EnergyReagents)
                {
                    Assert.That(proto.HasIndex(reagent), $"{reagent} isn't a reagent");
                    Assert.That(generated.ContainsKey(reagent), $"{reagent} should be made from energy, not jugs");
                }

                foreach (var (reagent, cost) in generated)
                {
                    Assert.That(cost, Is.GreaterThanOrEqualTo(0f), $"{reagent} cost");
                    Assert.That(cost * 30f, Is.LessThanOrEqualTo(720f), $"{reagent} would cost more than a medium power cell holds for one 30u dispense");
                }

                // The jug storage starts empty
                Assert.That(containers.TryGetContainer(dispenser, "storagebase", out var storage), "the dispenser has no jug storage");
                Assert.That(storage?.ContainedEntities, Is.Empty, "a filled dispenser shouldn't be stocked with jugs");
            });

            entMan.DeleteEntity(dispenser);
        });

        await pair.CleanReturnAsync();
    }
}
