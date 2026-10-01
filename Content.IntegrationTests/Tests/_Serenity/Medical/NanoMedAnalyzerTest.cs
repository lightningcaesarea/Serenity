using Content.Shared.VendingMachines;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
public sealed class NanoMedAnalyzerTest
{
    private static readonly ProtoId<VendingMachineInventoryPrototype>[] Medbay =
    [
        "NanoMedInventory",
        "NanoMedPlusInventory",
        "GoldNanoMedInventory",
        "GoldNanoMedPlusInventory",
    ];

    private static readonly ProtoId<VendingMachineInventoryPrototype>[] Civilian =
    [
        "NanoMedCivilianInventory",
        "NanoMedCivilianWallInventory",
        "GoldNanoMedCivilianInventory",
        "GoldNanoMedCivilianWallInventory",
    ];

    /// <summary>
    /// The medbay NanoMeds sell the advanced health analyzer instead of the basic one; the civilian NanoMeds still
    /// sell the basic one, which is the only analyzer anyone untrained can use.
    /// </summary>
    [Test]
    public async Task MedbayMachinesStockTheAdvancedAnalyzer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ProtoMan;

        Assert.Multiple(() =>
        {
            foreach (var id in Medbay)
            {
                var stock = proto.Index(id).StartingInventory;
                Assert.That(stock.ContainsKey("AdvancedHealthAnalyzer"), $"{id} should stock the advanced analyzer");
                Assert.That(stock.ContainsKey("HandheldHealthAnalyzer"), Is.False, $"{id} should no longer stock the basic analyzer");
            }

            foreach (var id in Civilian)
            {
                var stock = proto.Index(id).StartingInventory;
                Assert.That(stock.ContainsKey("HandheldHealthAnalyzer"), $"{id} should still stock the basic analyzer");
                Assert.That(stock.ContainsKey("AdvancedHealthAnalyzer"), Is.False, $"{id} shouldn't stock the advanced analyzer");
            }
        });

        await pair.CleanReturnAsync();
    }
}
