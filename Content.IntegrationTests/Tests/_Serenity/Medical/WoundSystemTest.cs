using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
[TestOf(typeof(SharedWoundSystem))]
public sealed class WoundSystemTest
{
    /// <summary>
    /// Clearing a category the patient has no wounds in must not dirty the component, but clearing real wounds must
    /// clear only that category and dirty it so clients see the change.
    /// </summary>
    [Test]
    public async Task ClearWoundsOnlyDirtiesWhenSomethingWasCleared()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();

        EntityUid patient = default;
        WoundComponent comp = default!;
        GameTick tickBefore = default;

        await server.WaitAssertion(() =>
        {
            patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            comp = entMan.GetComponent<WoundComponent>(patient);
            wounds.AddWound(patient, comp, new WoundEntry("BluntFracture", 2));
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            tickBefore = comp.LastModifiedTick;

            // No burns to clear
            wounds.ClearWoundsByCategory(patient, WoundCategory.Burn, comp);
            Assert.That(comp.LastModifiedTick, Is.EqualTo(tickBefore), "clearing nothing shouldn't dirty the component");
            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(1));
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            wounds.ClearWoundsByCategory(patient, WoundCategory.Fracture, comp);
            Assert.That(comp.ActiveWounds, Is.Empty);
            Assert.That(comp.LastModifiedTick, Is.GreaterThan(tickBefore), "clearing a wound should dirty the component");
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// AddWound with an entity networks the wound (dirties the component); the threshold multiplier stays in range
    /// however many factors are stacked on it.
    /// </summary>
    [Test]
    public async Task AddWoundDirtiesAndMultiplierIsClamped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();

        EntityUid patient = default;
        WoundComponent comp = default!;
        GameTick tickBefore = default;

        await server.WaitAssertion(() =>
        {
            patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            comp = entMan.GetComponent<WoundComponent>(patient);
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            tickBefore = comp.LastModifiedTick;
            wounds.AddWound(patient, comp, new WoundEntry("HeatBurn", 1));
            Assert.That(comp.LastModifiedTick, Is.GreaterThan(tickBefore), "AddWound should dirty the component");

            wounds.ScaleThresholdMultiplier(patient, 1000f, comp);
            Assert.That(comp.ThresholdMultiplier, Is.EqualTo(WoundsConstants.MaxThresholdMultiplier));

            wounds.ScaleThresholdMultiplier(patient, 0f, comp);
            Assert.That(comp.ThresholdMultiplier, Is.EqualTo(WoundsConstants.MinThresholdMultiplier), "a zero factor must not zero the multiplier");

            wounds.ScaleThresholdMultiplier(patient, -5f, comp);
            Assert.That(comp.ThresholdMultiplier, Is.EqualTo(WoundsConstants.MinThresholdMultiplier), "a negative factor must not flip the multiplier");

            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }
}
