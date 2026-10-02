using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.VendingMachines;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Content.Shared.VendingMachines;

namespace Content.IntegrationTests.Tests._Serenity.Economy;

/// <summary>
/// Vending machines must charge exactly the Federal Bills price their UI shows: the round's category price,
/// not the item's <c>fallbackPrice</c>.
/// </summary>
public sealed class VendingPriceTest : InteractionTest
{
    private const string Machine = "SerenityTestPricedVendingMachine";
    private const string Item = "SerenityTestPricedItem";
    private const int CategoryPrice = 7; // matches the test priceCategory; fallbackPrice is 200

    [TestPrototypes]
    private const string Prototypes = $@"
-   type: priceCategory
    id: serenity_test_price
    min: 7
    max: 7

-   type: entity
    parent: BaseItem
    id: {Item}
    components:
    -   type: ItemPrice
        priceCategory: serenity_test_price
        fallbackPrice: 200

-   type: vendingMachineInventory
    id: SerenityTestPricedInventory
    startingInventory:
        {Item}: 5

-   type: entity
    parent: VendingMachine
    id: {Machine}
    components:
    -   type: VendingMachine
        pack: SerenityTestPricedInventory
        ejectDelay: 0
    -   type: Sprite
        sprite: error.rsi
";

    [Test]
    public async Task ChargesTheDisplayedPrice()
    {
        await SpawnTarget(Machine);
        await SpawnEntity("APCBasic", SEntMan.GetCoordinates(TargetCoords));
        await RunTicks(1);

        var vending = SEntMan.System<VendingMachineSystem>();
        var slots = SEntMan.System<ItemSlotsSystem>();
        var machine = STarget!.Value;

        await Activate();
        Assert.That(IsUiOpen(VendingMachineUiKey.Key), "vending BUI failed to open");

        // The price the client UI shows (networked entry price) and the price the server charges must agree.
        await Server.WaitAssertion(() =>
        {
            var comp = SEntMan.GetComponent<VendingMachineComponent>(machine);
            Assert.That(vending.GetVendPrice(comp.Inventory[Item], comp), Is.EqualTo(CategoryPrice));
        });
        var clientComp = CEntMan.GetComponent<VendingMachineComponent>(CTarget!.Value);
        Assert.That(clientComp.Inventory[Item].Price, Is.EqualTo(CategoryPrice), "UI should show the category price");

        // No bills: nothing is vended.
        await SendBui(VendingMachineUiKey.Key, new VendingMachineEjectMessage(InventoryType.Regular, Item));
        Assert.That(vending.GetAllInventory(machine).First().Amount, Is.EqualTo(5), "vended without payment");

        // 10 bills in the slot: one vend takes exactly the displayed price.
        await InteractUsing(("SpaceCash", 10));
        var bills = slots.GetItemOrNull(machine, "billSlot");
        Assert.That(bills, Is.Not.Null, "bills should be in the bill slot");

        await SendBui(VendingMachineUiKey.Key, new VendingMachineEjectMessage(InventoryType.Regular, Item));
        await RunTicks(5);

        Assert.That(vending.GetAllInventory(machine).First().Amount, Is.EqualTo(4), "item was not vended");
        Assert.That(SEntMan.GetComponent<StackComponent>(bills!.Value).Count, Is.EqualTo(10 - CategoryPrice),
            "vending must charge the displayed category price, not the fallback price");
    }
}
