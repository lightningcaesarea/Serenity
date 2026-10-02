using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Station.Systems;
using Content.Shared._Serenity.Shipyard;
using Content.Shared._Serenity.Shipyard.Components;
using Content.Shared._Serenity.Shipyard.Events;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Serenity.Economy;

/// <summary>
/// Shipyard consoles take Federal Bills in their bill slot, buy a ship with them (change stays in the slot),
/// and deed the ship to the inserted ID card.
/// </summary>
public sealed class ShipyardConsoleTest : InteractionTest
{
    private const string Vessel = "CargoGas"; // ShipyardSalvage, 21500
    private const int VesselPrice = 21500;
    private const int Paid = 25000;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: BaseStation
  id: SerenityTestShipyardStation
  components:
  - type: Transform
";

    [Test]
    public async Task BuyShipWithBills()
    {
        await SpawnTarget("ComputerShipyardSalvage");
        ToggleNeedPower();
        var console = STarget!.Value;

        // The console's grid must belong to a station for purchases.
        await Server.WaitPost(() =>
        {
            var station = SEntMan.SpawnEntity("SerenityTestShipyardStation", MapCoordinates.Nullspace);
            SEntMan.System<StationSystem>().AddGridToStation(station, MapData.Grid);
        });
        await RunTicks(5);

        var slots = SEntMan.System<ItemSlotsSystem>();

        await InteractUsing("AssistantIDCard");
        var id = slots.GetItemOrNull(console, ShipyardConsoleComponent.TargetIdCardSlotId);
        Assert.That(id, Is.Not.Null, "ID card should be in the ID slot");

        await InteractUsing(("SpaceCash", Paid));
        var bills = slots.GetItemOrNull(console, ShipyardConsoleComponent.BillSlotId);
        Assert.That(bills, Is.Not.Null, "Federal Bills should be in the bill slot");
        Assert.That(SEntMan.GetComponent<StackComponent>(bills!.Value).Count, Is.EqualTo(Paid));

        await Activate();
        Assert.That(IsUiOpen(ShipyardConsoleUiKey.Shipyard), "shipyard BUI failed to open");

        await SendBui(ShipyardConsoleUiKey.Shipyard, new ShipyardConsolePurchaseMessage(Vessel));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.TryGetComponent<ShuttleDeedComponent>(id!.Value, out var deed), "ID card got no deed");
            Assert.That(deed!.ShuttleUid, Is.Not.Null);
            Assert.That(SEntMan.EntityExists(deed.ShuttleUid!.Value), "purchased ship does not exist");

            var change = slots.GetItemOrNull(console, ShipyardConsoleComponent.BillSlotId);
            Assert.That(change, Is.Not.Null, "change should stay in the bill slot");
            Assert.That(SEntMan.GetComponent<StackComponent>(change!.Value).Count, Is.EqualTo(Paid - VesselPrice));
        });
    }

    [Test]
    public async Task CommandConsoleNeedsCommandAccess()
    {
        await SpawnTarget("ComputerShipyardCommand");
        ToggleNeedPower();

        await Activate();
        Assert.That(IsUiOpen(ShipyardConsoleUiKey.Shipyard), Is.False, "command shipyard opened without Command access");

        await PlaceInHands("CaptainIDCard");
        await Activate();
        Assert.That(IsUiOpen(ShipyardConsoleUiKey.Shipyard), "command shipyard did not open for a Command ID");
    }
}
