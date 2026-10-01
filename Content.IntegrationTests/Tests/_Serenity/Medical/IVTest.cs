using System.Linq;
using System.Numerics;
using Content.Server._Serenity.Skills;
using Content.Shared._Serenity.Medical.IV;
using Content.Shared._Starlight.Medical.Body.Components;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage.Components;
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

    private static EntityUid SpawnStand(IEntityManager entMan, EntityCoordinates coords, string bagProto, out EntityUid bag)
    {
        var stand = entMan.SpawnAtPosition("IVStand", coords);
        bag = entMan.SpawnAtPosition(bagProto, coords);
        Assert.That(entMan.System<ItemSlotsSystem>().TryInsert(stand, "pack", bag, null), "the bag hangs on the stand");
        return stand;
    }

    private static Setup Build(IEntityManager entMan, EntityCoordinates coords, string bagProto)
    {
        var setup = new Setup
        {
            Origin = coords,
            Patient = entMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(1, 0))),
            Medic = entMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(0, 1))),
            Layman = entMan.SpawnAtPosition("MobHuman", coords.Offset(new Vector2(0, -1))),
        };

        setup.Stand = SpawnStand(entMan, coords, bagProto, out setup.Bag);
        Assert.That(entMan.System<SkillSystem>().GrantSkill(setup.Medic, "Medicine"));
        return setup;
    }

    private static void Drag(IEntityManager entMan, EntityUid stand, EntityUid user, EntityUid patient)
    {
        var ev = new DragDropDraggedEvent(user, patient);
        entMan.EventBus.RaiseLocalEvent(stand, ref ev);
    }

    private static FixedPoint2 BagVolume(IEntityManager entMan, Setup setup)
    {
        Assert.That(entMan.Deleted(setup.Bag), Is.False, "the bag still exists");
        Assert.That(entMan.System<SharedSolutionContainerSystem>().TryGetSolution(setup.Bag, "pack", out _, out var solution));
        return solution!.Volume;
    }

    private static TimeSpan AttachDelay(IEntityManager entMan, EntityUid user)
    {
        return entMan.GetComponent<DoAfterComponent>(user).DoAfters.Values.Single().Args.Delay;
    }

    /// <summary>
    /// Anyone can set a line, but without Medicine it takes much longer. Anyone can take one out.
    /// </summary>
    [Test]
    public async Task SettingALineIsSlowerWithoutMedicine()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        Setup setup = default!;
        EntityUid laymanStand = default;
        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            setup = Build(entMan, map.GridCoords, "IVBagSaline");
            laymanStand = SpawnStand(entMan, map.GridCoords.Offset(new Vector2(0, -2)), "IVBagSaline", out _);

            Drag(entMan, laymanStand, setup.Layman, setup.Patient);
            Drag(entMan, setup.Stand, setup.Medic, setup.Patient);

            var layman = AttachDelay(entMan, setup.Layman);
            var medic = AttachDelay(entMan, setup.Medic);
            Assert.That(layman, Is.GreaterThanOrEqualTo(medic * 3), "an unskilled user is much slower");
        });

        await pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.EqualTo(setup.Patient), "the skilled line is in");
            Assert.That(entMan.GetComponent<IVLineComponent>(laymanStand).AttachedTo, Is.Null, "the unskilled one is still being set");
        });

        await pair.RunSeconds(10f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(laymanStand).AttachedTo, Is.EqualTo(setup.Patient), "but they get there");

            // Anyone can take a line out.
            Drag(entMan, setup.Stand, setup.Layman, setup.Patient);
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
            Drag(entMan, setup.Stand, setup.Medic, setup.Patient);
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
    /// Whoever pulls a line loose tears it out and hurts the patient, unless they know Medicine, in which case it just comes out.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task PullingALineLoose(bool skilled)
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

            // With nobody carrying or pulling the stand it's the patient who walks off, so their skill is what counts.
            if (skilled)
                Assert.That(entMan.System<SkillSystem>().GrantSkill(setup.Patient, "Medicine"));

            Drag(entMan, setup.Stand, setup.Medic, setup.Patient);
        });

        await pair.RunSeconds(5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.EqualTo(setup.Patient));
            Assert.That(entMan.GetComponent<DamageableComponent>(setup.Patient).TotalDamage, Is.EqualTo(FixedPoint2.Zero));

            entMan.System<SharedTransformSystem>().SetCoordinates(setup.Patient, setup.Origin.Offset(new Vector2(10, 0)));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.GetComponent<IVLineComponent>(setup.Stand).AttachedTo, Is.Null, "the line came out");

            var damage = entMan.GetComponent<DamageableComponent>(setup.Patient).TotalDamage;
            if (skilled)
                Assert.That(damage, Is.EqualTo(FixedPoint2.Zero), "someone who knows Medicine takes it out cleanly");
            else
                Assert.That(damage, Is.GreaterThan(FixedPoint2.Zero), "an unskilled pull tears it out");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Saline only restores blood level when it goes straight into the bloodstream; drunk, it is used up in the stomach.
    /// </summary>
    [Test]
    public async Task SalineRestoresBloodOnlyWhenInjected()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        EntityUid drinker = default, injected = default;
        var drinkerBefore = 0f;
        var injectedBefore = 0f;
        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var bloodstream = entMan.System<SharedBloodstreamSystem>();
            drinker = entMan.SpawnAtPosition("MobHuman", map.GridCoords);
            injected = entMan.SpawnAtPosition("MobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            foreach (var mob in new[] { drinker, injected })
            {
                Assert.That(bloodstream.TryModifyBloodLevel(mob, FixedPoint2.New(-150)));
            }

            drinkerBefore = bloodstream.GetBloodLevel(drinker);
            injectedBefore = bloodstream.GetBloodLevel(injected);

            var stomachs = entMan.System<SharedBodySystem>().GetBodyOrganEntityComps<StomachComponent>(drinker);
            Assert.That(stomachs, Is.Not.Empty);
            Assert.That(entMan.System<StomachSystem>().TryTransferSolution((stomachs[0].Owner, null, null), new Solution("Saline", FixedPoint2.New(60))));
            Assert.That(bloodstream.TryAddToBloodstream(injected, new Solution("Saline", FixedPoint2.New(60))));
        });

        await pair.RunSeconds(20f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var bloodstream = entMan.System<SharedBloodstreamSystem>();
            var drunkGain = bloodstream.GetBloodLevel(drinker) - drinkerBefore;
            var injectedGain = bloodstream.GetBloodLevel(injected) - injectedBefore;

            Assert.That(injectedGain, Is.GreaterThan(0.05f), "injected saline restores blood");
            Assert.That(drunkGain, Is.LessThan(injectedGain / 2), "drunk saline does not");
        });

        await pair.CleanReturnAsync();
    }
}
