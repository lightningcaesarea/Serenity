using System.Linq;
using System.Numerics;
using Content.Shared._Serenity.Medical.IV;
using Content.Shared.Interaction;
using Content.Shared.Labels.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

/// <summary>
/// How the IV equipment is handed out and labelled. Whether the stand can be click-dragged is a client behaviour this
/// harness can't test (its client runs no content entity systems), so that is checked in game.
/// </summary>
[TestFixture]
public sealed class IVHandlingTest
{
    /// <summary>
    /// The vending machines sell the stand as a flatpack, which unpacks like a flatpacked machine: a multitool on the floor,
    /// with room, turns it into a stand.
    /// </summary>
    [Test]
    public async Task FlatpackUnpacksIntoAStand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        EntityUid pack = default, user = default, multitool = default, screwdriver = default;
        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            pack = entMan.SpawnAtPosition("IVStandFlatpack", map.GridCoords);

            // Everything else stands off the flatpack's tile: anything dynamic on it counts as "no room".
            user = entMan.SpawnAtPosition("MobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
            multitool = entMan.SpawnAtPosition("Multitool", map.GridCoords.Offset(new Vector2(1, 1)));
            screwdriver = entMan.SpawnAtPosition("Screwdriver", map.GridCoords.Offset(new Vector2(1, 1)));

            // A tool without the right quality does nothing.
            entMan.EventBus.RaiseLocalEvent(pack, new InteractUsingEvent(user, screwdriver, pack, map.GridCoords));
            Assert.That(entMan.EntityQuery<IVStandComponent>().Count(), Is.Zero, "a screwdriver doesn't unpack it");

            entMan.EventBus.RaiseLocalEvent(pack, new InteractUsingEvent(user, multitool, pack, map.GridCoords));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            Assert.That(entMan.EntityQuery<IVStandComponent>().Count(), Is.EqualTo(1), "a multitool unpacks it into one stand");
            Assert.That(entMan.Deleted(pack), "and the flatpack is used up");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The filled bags carry a label so they read "IV bag (Blood)" and "IV bag (Saline)"; the empty bag stays unlabelled.
    /// </summary>
    [TestCase("IVBagBlood", "Blood")]
    [TestCase("IVBagSaline", "Saline")]
    [TestCase("IVBag", "")]
    public async Task BagsAreLabelledByContents(string proto, string label)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var bag = entMan.SpawnAtPosition(proto, map.GridCoords);

            if (string.IsNullOrEmpty(label))
                Assert.That(entMan.HasComponent<LabelComponent>(bag), Is.False);
            else
                Assert.That(entMan.GetComponent<LabelComponent>(bag).CurrentLabel, Is.EqualTo(label));
        });

        await pair.CleanReturnAsync();
    }
}
