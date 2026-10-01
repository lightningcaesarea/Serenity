using System.Collections.Generic;
using System.Linq;
using Content.Shared._Serenity.Medical.Damage;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
[TestOf(typeof(SharedWoundSystem))]
public sealed class WoundLocationTest
{
    private static readonly ProtoId<WoundConfigPrototype> DefaultConfig = WoundConfigPrototype.DefaultId;

    private static readonly WoundLocation LeftArm = new(BodyPartType.Arm, BodyPartSymmetry.Left);
    private static readonly WoundLocation RightLeg = new(BodyPartType.Leg, BodyPartSymmetry.Right);

    /// <summary>
    /// A hit puts every wound it causes in one place the patient actually has, and over many hits the wounds land
    /// in more than one place.
    /// </summary>
    [Test]
    public async Task HitsLandInRealPartsAndVary()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();
        var body = entMan.System<SharedBodySystem>();
        var damageable = entMan.System<DamageableSystem>();
        var config = proto.Index(DefaultConfig);

        await server.WaitAssertion(() =>
        {
            var seen = new HashSet<WoundLocation>();
            for (var i = 0; i < 60; i++)
            {
                var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
                var comp = entMan.GetComponent<WoundComponent>(patient);

                // A hit big enough to cut and stab at once
                var hit = new DamageSpecifier(proto.Index(DamageTypeIds.Slash), FixedPoint2.New(30));
                hit.DamageDict["Piercing"] = FixedPoint2.New(30);
                damageable.TryChangeDamage(patient, hit, ignoreResistances: true);

                Assert.That(comp.ActiveWounds, Has.Count.EqualTo(2), "a cut and a stab");
                var location = comp.ActiveWounds[0].Location;
                Assert.That(location, Is.Not.Null, "wounds on a body have a location");
                Assert.That(comp.ActiveWounds[1].Location, Is.EqualTo(location), "one hit, one place");

                Assert.That(body.GetBodyChildrenOfType(patient, location!.Type).Any(p => p.Component.Symmetry == location.Symmetry),
                    $"the patient has no {location.LocKey}");
                seen.Add(location);

                entMan.DeleteEntity(patient);
            }

            Assert.That(seen.Count, Is.GreaterThan(2), "hits should land in different places");
            Assert.That(config.HitLocations, Is.Not.Empty);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A worse wound of the same type in the same place upgrades it; the same type somewhere else is a separate wound;
    /// a milder hit in a place that is already worse changes nothing.
    /// </summary>
    [Test]
    public async Task WoundsStackByLocation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var comp = entMan.GetComponent<WoundComponent>(patient);

            Assert.That(wounds.TryApplyWound(patient, comp, "BluntFracture", 1, LeftArm), Is.True);
            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(1));

            Assert.That(wounds.TryApplyWound(patient, comp, "BluntFracture", 3, LeftArm), Is.True, "worse in the same place");
            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(1));
            Assert.That(comp.ActiveWounds[0].Tier, Is.EqualTo(3));

            Assert.That(wounds.TryApplyWound(patient, comp, "BluntFracture", 1, LeftArm), Is.False, "milder in the same place");
            Assert.That(wounds.TryApplyWound(patient, comp, "BluntFracture", 3, LeftArm), Is.False, "no more stacking in one place");

            Assert.That(wounds.TryApplyWound(patient, comp, "BluntFracture", 2, RightLeg), Is.True, "a different place is another wound");
            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(2));

            // A whole-body wound (no location) is its own thing too
            Assert.That(wounds.TryApplyWound(patient, comp, "BluntFracture", 1, null), Is.True);
            Assert.That(comp.ActiveWounds, Has.Count.EqualTo(3));

            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The hit locations in the config use real body part types, have positive weights and no repeats, and every
    /// location has a locale name.
    /// </summary>
    [Test]
    public async Task HitLocationConfigIsConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var loc = server.ResolveDependency<ILocalizationManager>();

        Assert.Multiple(() =>
        {
            foreach (var config in proto.EnumeratePrototypes<WoundConfigPrototype>())
            {
                var seen = new HashSet<WoundLocation>();
                foreach (var hit in config.HitLocations)
                {
                    Assert.That(hit.Weight, Is.GreaterThan(0f), $"{config.ID} {hit.Type} weight");

                    var location = new WoundLocation(hit.Type, hit.Symmetry);
                    Assert.That(seen.Add(location), $"{config.ID} lists {location.LocKey} twice");
                    Assert.That(loc.HasString(location.LocKey), $"missing locale key {location.LocKey}");
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
