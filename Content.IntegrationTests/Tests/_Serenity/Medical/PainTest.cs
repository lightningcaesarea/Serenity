using Content.Server._Serenity.Medical.Pain;
using Content.Shared._Serenity.Medical.Pain;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
[TestOf(typeof(PainSystem))]
public sealed class PainTest
{
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(1);

    private static readonly string[] PainkillerEffects =
    [
        "StatusEffectPainkillerLight",
        "StatusEffectPainkillerAntiInflammatory",
        "StatusEffectPainkillerOpioid",
        "StatusEffectPainkillerTopical",
        "StatusEffectPainkillerAnesthetic",
    ];

    /// <summary>
    /// Wounds create pain, painkillers hide it without touching the wound, a topical painkiller only covers the
    /// injuries in its scope, and the pain returns when the drug is removed.
    /// </summary>
    [Test]
    public async Task PainkillersMaskWoundPainWithoutHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var wounds = entMan.System<SharedWoundSystem>();
        var pain = entMan.System<SharedPainSystem>();
        var effects = entMan.System<StatusEffectsSystem>();
        var config = server.ProtoMan.Index<PainConfigPrototype>(PainConfigPrototype.DefaultId);

        EntityUid patient = default;
        WoundComponent woundComp = default!;
        PainComponent painComp = default!;
        float before = 0f;

        await server.WaitAssertion(() =>
        {
            patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            woundComp = entMan.GetComponent<WoundComponent>(patient);
            painComp = entMan.GetComponent<PainComponent>(patient);

            Assert.That(painComp.Level, Is.EqualTo(PainLevel.None), "an uninjured patient should feel no pain");

            // A shattered bone: tier 3 fracture.
            wounds.AddWound(woundComp, new WoundEntry("BluntFracture", 3));
            pain.Recalculate((patient, painComp));
            Assert.That(painComp.RawPain, Is.EqualTo(config.WoundWeight(WoundCategory.Fracture, 3)));
            Assert.That(painComp.Level, Is.GreaterThanOrEqualTo(PainLevel.Moderate));
            Assert.That(painComp.Masked, Is.False);

            // A topical numbs skin-level injuries only, so a broken bone still hurts fully.
            before = painComp.EffectivePain;
            Assert.That(effects.TryAddStatusEffectDuration(patient, "StatusEffectPainkillerTopical", Duration));
            Assert.That(painComp.EffectivePain, Is.EqualTo(before), "a topical shouldn't dull a fracture");

            // A systemic opioid dulls it, but the wound stays.
            Assert.That(effects.TryAddStatusEffectDuration(patient, "StatusEffectPainkillerOpioid", Duration));
            Assert.That(painComp.EffectivePain, Is.LessThan(before));
            Assert.That(painComp.Masked, Is.True);
            Assert.That(woundComp.ActiveWounds, Has.Count.EqualTo(1), "painkillers must not heal the wound");

            // Removing the drug brings the pain straight back (status effects are deleted on the next tick).
            Assert.That(effects.TryRemoveStatusEffect(patient, "StatusEffectPainkillerOpioid"));
        });

        await server.WaitRunTicks(3);

        await server.WaitAssertion(() =>
        {
            Assert.That(painComp.EffectivePain, Is.EqualTo(before));

            // A burn is inside the still-active topical's scope, so the burn adds raw pain but no felt pain.
            var rawBefore = painComp.RawPain;
            wounds.AddWound(woundComp, new WoundEntry("HeatBurn", 3));
            pain.Recalculate((patient, painComp));
            Assert.That(painComp.RawPain, Is.GreaterThan(rawBefore));
            Assert.That(painComp.EffectivePain, Is.EqualTo(before), "the topical should absorb the burn's pain");
            Assert.That(painComp.Masked, Is.True);

            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every painkiller reagent must point at a status effect that carries a painkiller component, or it does nothing.
    /// </summary>
    [Test]
    public async Task PainkillerEffectsAreWired()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var factory = server.EntMan.ComponentFactory;

        await server.WaitAssertion(() =>
        {
            foreach (var id in PainkillerEffects)
            {
                var entity = proto.Index<EntityPrototype>(id);
                Assert.That(entity.TryGetComponent<PainkillerStatusEffectComponent>(out var comp, factory), $"{id} lacks a painkiller component");
                Assert.That(comp!.Strength, Is.GreaterThan(0f), $"{id} masks nothing");
            }
        });

        await pair.CleanReturnAsync();
    }
}
