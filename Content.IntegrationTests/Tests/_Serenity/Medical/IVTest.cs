using System.Numerics;
using Content.Server._Serenity.Skills;
using Content.Shared._Serenity.Medical.IV;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
public sealed class IVTest
{
    private sealed class Setup
    {
        public EntityUid Stand;
        public EntityUid Bag;
        public EntityUid Patient;
        public EntityUid Medic;
        public EntityUid Layman;
        public EntityCoordinates Origin;
    }

    private static Setup Build(IEntityManager entMan, EntityCoordinates coords, string bagProto)
    {
        var setup = new Setup
        {
            Origin = coords,
            Stand = entMan.SpawnAtPosition("IVStand", coords),
            Bag = entMan.SpawnAtPosition(bagProto, coords),
            Patient = entMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(1, 0))),
            Medic = entMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(0, 1))),
            Layman = entMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(0, -1))),
        };

        Assert.That(entMan.System<ItemSlotsSystem>().TryInsert(setup.Stand, "pack", setup.Bag, null), "the bag hangs on the stand");
        Assert.That(entMan.System<SkillSystem>().GrantSkill(setup.Medic, "Medicine"));
        return setup;
    }

    private static void Drag(IEntityManager entMan, Setup setup, EntityUid user)
    {
        var ev = new DragDropDraggedEvent(user, setup.Patient);
        entMan.EventBus.RaiseLocalEvent(setup.Stand, ref ev);
    }

    private static FixedPoint2 BagVolume(IEntityManager entMan, Setup setup)
    {
        Assert.That(entMan.Deleted(setup.Bag), Is.False, "the bag still exists");
        Assert.That(entMan.System<SharedSolutionContainerSystem>().TryGetSolution(setup.Bag, "pack", out _, out var solution));
        return solution!.Volume;
    }

    /// <summary>
    /// Setting a line needs the Medicine skill; anyone can still take one out.
    /// </summary>
    [Test]
    public async Task SettingALineNeedsMedicine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        Setup setup = default!;
        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            setup = Build(entMan, map.GridCoords, "IVBagSaline");

            Drag(entMan, setup, setup.Layman);
            Assert.That(entMan.GetComponent<DoAfterComponent>(setup.Layman).DoAfters, Is.Empty, "an unskilled user can't start a line");

            Drag(entMan, setup, setup.Medic);
            Assert.That(entMan.GetComponent<DoAfterComponent>(setup.Medic).DoAfters, Is.Not.Empty, "a skilled user can");
        });

        await pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.EqualTo(setup.Patient));

            // Someone who can't set a line can still take it out.
            Drag(entMan, setup, setup.Layman);
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.Null);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A hooked-up bag gives fluid to the patient, and drawing stops at the blood floor.
    /// </summary>
    [Test]
    public async Task LineMovesFluid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        Setup setup = default!;
        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            setup = Build(entMan, map.GridCoords, "IVBagSaline");
            Drag(entMan, setup, setup.Medic);
        });

        await pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.EqualTo(setup.Patient));
        });

        await pair.RunSeconds(6f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(BagVolume(entMan, setup), Is.LessThan(FixedPoint2.New(100)), "fluid left the bag");

            // Switch to drawing, swap in an empty bag and drop the patient's blood below the floor.
            var line = entMan.GetComponent<IVLineComponent>(setup.Stand);
            line.Injecting = false;
            var slots = entMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryEject(setup.Stand, "pack", null, out var old));
            entMan.DeleteEntity(old);
            setup.Bag = entMan.SpawnAtPosition("IVBag", setup.Origin);
            Assert.That(slots.TryInsert(setup.Stand, "pack", setup.Bag, null));

            Assert.That(entMan.System<SharedBloodstreamSystem>().TryModifyBloodLevel(setup.Patient, FixedPoint2.New(-300)));
            Assert.That(entMan.System<SharedBloodstreamSystem>().GetBloodLevel(setup.Patient), Is.LessThan(line.DrawFloor));
        });

        await pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(BagVolume(entMan, setup), Is.EqualTo(FixedPoint2.Zero), "no blood is drawn below the floor");

            // Back to a healthy level, blood comes.
            entMan.System<SharedBloodstreamSystem>().TryModifyBloodLevel(setup.Patient, FixedPoint2.New(300));
        });

        await pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(BagVolume(entMan, setup), Is.GreaterThan(FixedPoint2.Zero), "blood is drawn above the floor");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Walking away from the stand tears the line out.
    /// </summary>
    [Test]
    public async Task LineIsTornOutOnDistance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        Setup setup = default!;
        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            setup = Build(entMan, map.GridCoords, "IVBagSaline");
            Drag(entMan, setup, setup.Medic);
        });

        await pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.EqualTo(setup.Patient));

            entMan.System<SharedTransformSystem>().SetCoordinates(setup.Patient, setup.Origin.Offset(new Vector2(10, 0)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.Null, "the line came out");
        });

        await pair.CleanReturnAsync();
    }
}
