using System.Linq;
using Content.Shared._Serenity.Medical.Damage;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
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
            wounds.ClearWoundsByCategory(patient, WoundCategoryIds.Burn, comp);
            Assert.That(comp.LastModifiedTick, Is.EqualTo(tickBefore), "clearing nothing shouldn't dirty the component");
            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(1));
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            wounds.ClearWoundsByCategory(patient, WoundCategoryIds.Fracture, comp);
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

    /// <summary>
    /// A wound type scores the whole hit: several damage types in one hit add up (by weight), and a type the
    /// wound doesn't list adds nothing.
    /// </summary>
    [Test]
    public void WoundScoreCombinesDamageTypes()
    {
        // No production wound type mixes damage types yet, so build one. Only the scoring maths is under test,
        // and prototypes aren't registered with the manager here, which is what RA0039 guards against.
#pragma warning disable RA0039
        var wound = new WoundTypePrototype
        {
            Damage = { ["Blunt"] = 1f, ["Slash"] = 0.5f },
            Thresholds = [10f, 20f, 30f],
        };
#pragma warning restore RA0039

        var blunt = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(8) } };
        var both = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(8), ["Slash"] = FixedPoint2.New(8) } };
        var unrelated = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(8), ["Heat"] = FixedPoint2.New(50) } };
        var healing = new DamageSpecifier { DamageDict = { ["Blunt"] = FixedPoint2.New(-30) } };

        Assert.Multiple(() =>
        {
            Assert.That(wound.Score(blunt), Is.EqualTo(8f));
            Assert.That(wound.TierFor(wound.Score(blunt)), Is.EqualTo(0), "8 blunt alone is below the first threshold");

            Assert.That(wound.Score(both), Is.EqualTo(12f), "8 blunt + 8 slash at half weight");
            Assert.That(wound.TierFor(wound.Score(both)), Is.EqualTo(1), "the two damage types together reach tier 1");

            Assert.That(wound.Score(unrelated), Is.EqualTo(8f), "heat isn't one of this wound's damage types");
            Assert.That(wound.Score(healing), Is.EqualTo(0f), "healing never scores");

            Assert.That(wound.TierFor(20f), Is.EqualTo(2));
            Assert.That(wound.TierFor(1000f), Is.EqualTo(3));
        });
    }

    /// <summary>
    /// Slashing and piercing hits now create real wound entries (they used to only set a display-only bleed source),
    /// and a single hit that is big enough in both creates both.
    /// </summary>
    [Test]
    public async Task SlashAndPiercingHitsCreateWounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;
        var mapData = await pair.CreateTestMap();
        var damageable = entMan.System<DamageableSystem>();

        EntityUid patient = default;
        WoundComponent comp = default!;

        await server.WaitAssertion(() =>
        {
            patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            comp = entMan.GetComponent<WoundComponent>(patient);

            // Below every threshold: no wound
            var small = new DamageSpecifier(proto.Index(DamageTypeIds.Slash), FixedPoint2.New(3));
            damageable.TryChangeDamage(patient, small, ignoreResistances: true);
            Assert.That(comp.ActiveWounds, Is.Empty, "a scratch shouldn't make a wound");

            // One hit with both a cut and a stab
            var both = new DamageSpecifier(proto.Index(DamageTypeIds.Slash), FixedPoint2.New(25));
            both.DamageDict["Piercing"] = FixedPoint2.New(25);
            damageable.TryChangeDamage(patient, both, ignoreResistances: true);

            Assert.That(comp.ActiveWounds.Select(w => w.WoundTypeId.Id), Is.EquivalentTo(new[] { "SlashLaceration", "PiercingPuncture" }));
            Assert.That(comp.BleedSourceDamageType, Is.EqualTo("Slash"), "slash outranks piercing as the bleed source");

            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }
}
