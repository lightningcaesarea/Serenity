using System.Linq;
using Content.Shared.VendingMachines;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

/// <summary>
/// Where IV stands are handed out: the medbay machines and the medicine lockers, not the public civilian machines.
/// </summary>
[TestFixture]
public sealed class IVStockingTest
{
    // Same split as NanoMedAnalyzerTest: the medbay machines versus the civilian / planetary ones.
    private static readonly ProtoId<VendingMachineInventoryPrototype>[] Medbay =
    [
        "NanoMedInventory",
        "NanoMedPlusInventory",
        "GoldNanoMedInventory",
        "GoldNanoMedPlusInventory",
        "DeforestInterstellarInventory",
        "DeforestInterstellarWallInventory",
    ];

    private static readonly ProtoId<VendingMachineInventoryPrototype>[] Civilian =
    [
        "NanoMedCivilianInventory",
        "NanoMedCivilianWallInventory",
        "GoldNanoMedCivilianInventory",
        "GoldNanoMedCivilianWallInventory",
        "DeforestPlanetaryInventory",
        "DeforestPlanetaryWallInventory",
    ];

    [Test]
    public async Task MedbayMachinesStockIVStands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ProtoMan;

        Assert.Multiple(() =>
        {
            foreach (var id in Medbay)
            {
                Assert.That(proto.Index(id).StartingInventory.ContainsKey("IVStand"), $"{id} should stock an IV stand");
            }

            foreach (var id in Civilian)
            {
                Assert.That(proto.Index(id).StartingInventory.ContainsKey("IVStand"), Is.False, $"{id} shouldn't stock an IV stand");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The fill is easy to get silently wrong: a stand isn't an item, so check the locker really takes it.
    /// </summary>
    [TestCase("LockerMedicineFilled")]
    [TestCase("LockerWallMedicalFilled")]
    public async Task MedicineLockersHoldAnIVStand(string locker)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var containers = entMan.System<SharedContainerSystem>();

            var uid = entMan.SpawnAtPosition(locker, map.GridCoords);
            Assert.That(containers.TryGetContainer(uid, "entity_storage", out var storage));

            var stands = storage!.ContainedEntities.Count(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "IVStand");
            Assert.That(stands, Is.EqualTo(1), $"{locker} should hold exactly one IV stand");
        });

        await pair.CleanReturnAsync();
    }
}
