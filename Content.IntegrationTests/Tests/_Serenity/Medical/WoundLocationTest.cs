using System.Collections.Generic;
using System.Linq;
using Content.Shared._Serenity.Medical.Damage;
using Content.Shared._Serenity.Medical.Pain;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weapons.Melee.Events;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
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
    private static readonly WoundLocation Head = new(BodyPartType.Head, BodyPartSymmetry.None);

    private static readonly EntProtoId Woozy = "StatusEffectWoozy";

    [TestPrototypes]
    private const string Prototypes = @"
- { type: entity, id: TestSerenityHeadClub, components: [ { type: HitLocationBias, multipliers: { Torso: 0, Arm: 0, Hand: 0, Leg: 0, Foot: 0 } } ] }
";

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
    /// A weapon's bias steers where its hits land: a club that only ever hits heads puts its wounds in the head,
    /// and a bias that rules out everything falls back to the normal spread.
    /// </summary>
    [Test]
    public async Task WeaponBiasSteersHits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var proto = server.ProtoMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();
        var damageable = entMan.System<DamageableSystem>();
        var config = proto.Index(DefaultConfig);

        await server.WaitAssertion(() =>
        {
            var club = entMan.SpawnEntity("TestSerenityHeadClub", mapData.GridCoords);
            for (var i = 0; i < 10; i++)
            {
                var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
                var comp = entMan.GetComponent<WoundComponent>(patient);

                // The melee system tells the target what is hitting it just before the damage lands
                entMan.EventBus.RaiseLocalEvent(patient, new AttackedEvent(club, club, mapData.GridCoords));
                var hit = new DamageSpecifier(proto.Index(DamageTypeIds.Blunt), FixedPoint2.New(40));
                damageable.TryChangeDamage(patient, hit, ignoreResistances: true);

                Assert.That(comp.ActiveWounds, Is.Not.Empty, "a 40 blunt hit should wound");
                Assert.That(comp.ActiveWounds[0].Location, Is.EqualTo(Head), "the club only hits heads");

                entMan.DeleteEntity(patient);
            }

            var other = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var nothing = new HitLocationBiasComponent();
            foreach (var type in Enum.GetValues<BodyPartType>())
            {
                nothing.Multipliers[type] = 0f;
            }

            Assert.That(wounds.PickLocation(other, config, nothing), Is.Not.Null, "an impossible bias falls back");

            entMan.DeleteEntity(other);
            entMan.DeleteEntity(club);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Clicking high on a target aims at the head, low at the legs, and a click well off the target isn't aimed.
    /// Aiming shifts the odds: over many aimed hits most land in the aimed zone, but the aim never forces it.
    /// </summary>
    [Test]
    public async Task ClickHeightAimsHits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();
        var config = server.ProtoMan.Index(DefaultConfig);

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var target = (patient, entMan.GetComponent<WoundComponent>(patient));
            var at = entMan.GetComponent<TransformComponent>(patient).Coordinates;

            Assert.That(wounds.AimedAt(target, at.Offset(new Vector2(0f, 0.35f))), Does.Contain(BodyPartType.Head));
            Assert.That(wounds.AimedAt(target, at.Offset(new Vector2(0.1f, -0.35f))), Does.Contain(BodyPartType.Leg));
            Assert.That(wounds.AimedAt(target, at), Does.Contain(BodyPartType.Torso));
            Assert.That(wounds.AimedAt(target, at.Offset(new Vector2(2f, 0f))), Is.Null, "a click off the target isn't aimed");

            var head = wounds.AimedAt(target, at.Offset(new Vector2(0f, 0.35f)));
            var heads = 0;
            const int tries = 400;
            for (var i = 0; i < tries; i++)
            {
                if (wounds.PickLocation(patient, config, aimed: head)?.Type == BodyPartType.Head)
                    heads++;
            }

            // Head is 8 of 100 by default; aimed x3 it is 24 of 116, about a fifth
            Assert.That(heads, Is.InRange(tries / 10, tries / 2), "aiming high should land on the head more often, not always");

            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// What a wound does depends on where it is: a broken leg slows you and a broken arm doesn't, a cracked skull
    /// makes you woozy, and a head wound hurts more than the same wound in a leg.
    /// </summary>
    [Test]
    public async Task WoundEffectsDependOnLocation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();
        var pain = entMan.System<SharedPainSystem>();
        var status = entMan.System<StatusEffectsSystem>();

        await server.WaitAssertion(() =>
        {
            var legPatient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var armPatient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var headPatient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);

            Assert.That(wounds.TryApplyWound(legPatient, entMan.GetComponent<WoundComponent>(legPatient), "BluntFracture", 2, RightLeg));
            Assert.That(wounds.TryApplyWound(armPatient, entMan.GetComponent<WoundComponent>(armPatient), "BluntFracture", 2, LeftArm));
            Assert.That(wounds.TryApplyWound(headPatient, entMan.GetComponent<WoundComponent>(headPatient), "BluntFracture", 2, Head));

            // Same wound, same pain everywhere but the head, so the only speed difference is the limp
            var legSpeed = entMan.GetComponent<MovementSpeedModifierComponent>(legPatient).SprintSpeedModifier;
            var armSpeed = entMan.GetComponent<MovementSpeedModifierComponent>(armPatient).SprintSpeedModifier;
            Assert.That(legSpeed, Is.LessThan(armSpeed), "a broken leg should slow more than a broken arm");

            Assert.That(status.HasStatusEffect(headPatient, Woozy), "a cracked skull should concuss");
            Assert.That(status.HasStatusEffect(legPatient, Woozy), Is.False);

            var legPain = entMan.GetComponent<PainComponent>(legPatient);
            var headPain = entMan.GetComponent<PainComponent>(headPatient);
            pain.Recalculate((legPatient, legPain));
            pain.Recalculate((headPatient, headPain));
            Assert.That(headPain.RawPain, Is.GreaterThan(legPain.RawPain), "a head wound should hurt more");

            entMan.DeleteEntity(legPatient);
            entMan.DeleteEntity(armPatient);
            entMan.DeleteEntity(headPatient);
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
