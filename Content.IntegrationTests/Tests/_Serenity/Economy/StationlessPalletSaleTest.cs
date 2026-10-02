using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Cargo.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Events;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Serenity.Economy;

/// <summary>
/// A cargo pallet console on a grid that isn't a station (the Cargo Depot POI) pays out Federal Bills at the
/// console instead of silently doing nothing.
/// </summary>
public sealed class StationlessPalletSaleTest : InteractionTest
{
    private const string Goods = "SerenityTestPalletGoods";

    [TestPrototypes]
    private const string Prototypes = $@"
-   type: entity
    parent: BaseItem
    id: {Goods}
    components:
    -   type: StaticPrice
        price: 123
";

    [Test]
    public async Task SellsForBills()
    {
        await SpawnTarget("ComputerPalletConsole");
        ToggleNeedPower();

        var palletCoords = SEntMan.GetNetCoordinates(SEntMan.GetCoordinates(TargetCoords).Offset(new Vector2(0f, 1f)));
        await SetTile(Plating, palletCoords, MapData.Grid);

        EntityUid goods = default;
        await Server.WaitPost(() =>
        {
            var coords = SEntMan.GetCoordinates(palletCoords);
            var pallet = SEntMan.SpawnAtPosition("CargoPalletSell", coords);
            Assert.That(SEntMan.GetComponent<TransformComponent>(pallet).Anchored, "pallet should spawn anchored");
            goods = SEntMan.SpawnAtPosition(Goods, coords);
        });
        await RunTicks(5);

        var expected = 0;
        await Server.WaitPost(() => expected = (int) Math.Round(SEntMan.System<PricingSystem>().GetPrice(goods)));
        Assert.That(expected, Is.GreaterThan(0));

        await Activate();
        Assert.That(IsUiOpen(CargoPalletConsoleUiKey.Sale), "pallet console BUI failed to open");

        await SendBui(CargoPalletConsoleUiKey.Sale, new CargoPalletSellMessage());

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(goods), "goods on the pallet were not sold");

            var paid = 0;
            var query = SEntMan.AllEntityQueryEnumerator<StackComponent>();
            while (query.MoveNext(out _, out var stack))
            {
                if (stack.StackTypeId == "Credit")
                    paid += stack.Count;
            }

            Assert.That(paid, Is.EqualTo(expected), "sale should pay its value in Federal Bills at the console");
        });
    }
}
