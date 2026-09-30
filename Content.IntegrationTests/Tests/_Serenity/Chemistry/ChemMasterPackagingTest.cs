using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Serenity.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Serenity.Chemistry;

public sealed class ChemMasterPackagingTest : InteractionTest
{
    private const string Reagent = "Water";

    /// <summary>
    /// Moves reagent from a beaker into the packaging buffer (not the main buffer), then prints pills
    /// that split it evenly.
    /// </summary>
    [Test]
    public async Task PackagingBufferPrintsPillsWithoutTouchingMainBuffer()
    {
        await SpawnTarget("ChemMaster");
        ToggleNeedPower();

        await InteractUsing("Beaker");

        var solutions = Server.System<SharedSolutionContainerSystem>();
        var slots = Server.System<ItemSlotsSystem>();
        var chem = STarget!.Value;
        var beaker = slots.GetItemOrNull(chem, SharedChemMaster.InputSlotName);
        Assert.That(beaker, Is.Not.Null, "beaker should have been inserted");

        await Server.WaitPost(() =>
        {
            Assert.That(solutions.TryGetFitsInDispenser(beaker!.Value, out var beakerSoln, out _));
            solutions.TryAddReagent(beakerSoln!.Value, Reagent, FixedPoint2.New(60), out _);
        });

        // Open the UI so the client window is exercised too.
        await Interact();

        await SendBui(ChemMasterUiKey.Key,
            new ChemMasterPackagingTransferMessage(new ReagentId(Reagent, null), FixedPoint2.MaxValue, true));

        await Server.WaitAssertion(() =>
        {
            var packaging = SEntMan.GetComponent<ChemMasterPackagingComponent>(chem);
            Assert.That(solutions.TryGetSolution(chem, packaging.BufferSolution, out _, out var buffer));
            Assert.That(buffer!.Volume, Is.EqualTo(FixedPoint2.New(60)), "all reagent should be in the packaging buffer");
            Assert.That(solutions.TryGetSolution(chem, SharedChemMaster.BufferSolutionName, out _, out var main));
            Assert.That(main!.Volume, Is.EqualTo(FixedPoint2.Zero), "main buffer must stay untouched");
        });

        var pills = 0;

        await SendBui(ChemMasterUiKey.Key, new ChemMasterPackagingSelectMessage(1, "ChemMasterPill3"));
        await SendBui(ChemMasterUiKey.Key, new ChemMasterPackagingSetAmountMessage(3));
        await SendBui(ChemMasterUiKey.Key, new ChemMasterPackagingPrintMessage(""));

        await Server.WaitAssertion(() =>
        {
            var query = SEntMan.AllEntityQueryEnumerator<PillComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out var pill, out var meta))
            {
                if (meta.EntityPrototype?.ID != "ChemMasterPill3")
                    continue;

                pills++;
                Assert.That(pill.PillType, Is.EqualTo(2u));
                Assert.That(solutions.TryGetSolution(uid, SharedChemMaster.PillSolutionName, out _, out var soln));
                Assert.That(soln!.Volume, Is.EqualTo(FixedPoint2.New(20)), "60u over 3 pills is 20u each");
            }

            Assert.That(pills, Is.EqualTo(3));
            Assert.That(solutions.TryGetSolution(chem, SEntMan.GetComponent<ChemMasterPackagingComponent>(chem).BufferSolution, out _, out var buffer));
            Assert.That(buffer!.Volume, Is.EqualTo(FixedPoint2.Zero));
        });
    }
}
