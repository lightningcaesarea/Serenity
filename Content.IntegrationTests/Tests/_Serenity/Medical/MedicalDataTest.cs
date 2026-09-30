using System.Collections.Generic;
using Content.Shared._Serenity.Medical.Pain;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared.Alert;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

/// <summary>
/// Catches YAML and locale mistakes in the wound and pain data that would otherwise fail silently in game:
/// arrays that don't match the tier count, pain bands out of order, damage types that don't exist,
/// and locale keys that are built by string interpolation at runtime.
/// </summary>
[TestFixture]
public sealed class MedicalDataTest
{
    private const int TierCount = WoundsConstants.MaxWoundTier;

    [Test]
    public async Task WoundTypesAreConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var wound in proto.EnumeratePrototypes<WoundTypePrototype>())
                {
                    Assert.That(wound.Thresholds, Has.Length.EqualTo(TierCount), $"{wound.ID} needs {TierCount} thresholds");
                    Assert.That(wound.Thresholds, Is.Ordered.Ascending, $"{wound.ID} thresholds must increase with tier");
                    Assert.That(proto.HasIndex<DamageTypePrototype>(wound.DamageType), $"{wound.ID} uses unknown damage type {wound.DamageType}");

                    for (var tier = 1; tier <= TierCount; tier++)
                    {
                        Assert.That(wound.Names.ContainsKey(tier), $"{wound.ID} has no name for tier {tier}");
                        RequireLoc(loc, $"wound-{wound.ID.ToLowerInvariant()}-{tier}");
                    }
                }

                // Keys the wound display and examine code builds at runtime
                for (var tier = 1; tier <= TierCount; tier++)
                {
                    RequireLoc(loc, $"wound-bleed-slash-{tier}");
                    RequireLoc(loc, $"wound-bleed-piercing-{tier}");
                    RequireLoc(loc, $"wound-examine-fracture-{tier}");
                    RequireLoc(loc, $"wound-examine-burn-{tier}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConfigsAreConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.That(proto.HasIndex<WoundConfigPrototype>(WoundConfigPrototype.DefaultId), "the default wound config is missing");
            Assert.That(proto.HasIndex<PainConfigPrototype>(PainConfigPrototype.DefaultId), "the default pain config is missing");

            var woundCategories = new HashSet<WoundCategory> { WoundCategory.Bleeding };
            foreach (var wound in proto.EnumeratePrototypes<WoundTypePrototype>())
            {
                woundCategories.Add(wound.Category);
            }

            Assert.Multiple(() =>
            {
                foreach (var config in proto.EnumeratePrototypes<WoundConfigPrototype>())
                {
                    Assert.That(config.TierDecaySeconds, Has.Length.EqualTo(TierCount), $"{config.ID} needs {TierCount} decay times");
                    Assert.That(config.TierDecaySeconds, Has.All.GreaterThan(0f), $"{config.ID} decay times must be positive");
                    Assert.That(config.RegenTickSeconds, Is.GreaterThan(0f), $"{config.ID} regen tick must be positive");
                    Assert.That(config.MovementSlowTier, Is.InRange(1, TierCount), $"{config.ID} movement slow tier");
                    Assert.That(config.FractureDropTier, Is.InRange(1, TierCount), $"{config.ID} fracture drop tier");
                    Assert.That(config.MaxStackedWoundsPerType, Is.GreaterThanOrEqualTo(1), $"{config.ID} stack cap");
                    Assert.That(config.FractureDropChance, Is.InRange(0f, 1f), $"{config.ID} fracture drop chance");
                }

                foreach (var config in proto.EnumeratePrototypes<PainConfigPrototype>())
                {
                    Assert.That(config.MaxPain, Is.GreaterThan(0f), $"{config.ID} max pain");
                    Assert.That(config.TickSeconds, Is.GreaterThan(0f), $"{config.ID} tick");
                    Assert.That(config.SecondaryPainkillerFactor, Is.InRange(0f, 1f), $"{config.ID} secondary painkiller factor");

                    // Every band above None needs a threshold, and they must rise with severity.
                    var previous = 0f;
                    for (var level = PainLevel.Mild; level <= PainLevel.Agonizing; level++)
                    {
                        if (!config.Levels.TryGetValue(level, out var band))
                        {
                            Assert.Fail($"{config.ID} has no {level} band");
                            continue;
                        }

                        Assert.That(band.Threshold, Is.GreaterThan(previous), $"{config.ID} {level} threshold must be above the previous band");
                        Assert.That(band.SpeedMultiplier, Is.InRange(0.01f, 1f), $"{config.ID} {level} speed multiplier");
                        Assert.That(band.DropChance, Is.InRange(0f, 1f), $"{config.ID} {level} drop chance");
                        previous = band.Threshold;
                    }

                    Assert.That(config.Levels.Keys, Has.None.EqualTo(PainLevel.None), $"{config.ID} must not configure the None band");

                    foreach (var category in woundCategories)
                    {
                        if (!config.WoundWeights.TryGetValue(category, out var weights))
                        {
                            Assert.Fail($"{config.ID} has no wound weights for {category}");
                            continue;
                        }

                        Assert.That(weights, Has.Length.EqualTo(TierCount), $"{config.ID} {category} needs {TierCount} weights");
                        Assert.That(weights, Is.Ordered.Ascending, $"{config.ID} {category} weights");
                        Assert.That(weights, Has.All.GreaterThanOrEqualTo(0f), $"{config.ID} {category} weights must not be negative");
                    }
                }

                // Keys the pain code builds at runtime
                for (var level = PainLevel.Mild; level <= PainLevel.Agonizing; level++)
                {
                    RequireLoc(loc, $"pain-rise-{level.ToString().ToLowerInvariant()}");

                    if (level >= PainLevel.Moderate)
                        RequireLoc(loc, $"pain-examine-{level.ToString().ToLowerInvariant()}");
                }

                foreach (var key in new[] { "pain-examine-masked", "pain-easing", "pain-gone", "pain-numbed" })
                {
                    RequireLoc(loc, key);
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AlertsAndPainkillersAreWired()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var factory = server.EntMan.ComponentFactory;
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                // Fracture and burn alerts take a severity per tier; pain has one per band above None.
                AssertIcons(proto, "Fracture", TierCount);
                AssertIcons(proto, "Burn", TierCount);
                AssertIcons(proto, "Pain", (int) PainLevel.Agonizing);

                var painkillers = 0;
                foreach (var entity in proto.EnumeratePrototypes<EntityPrototype>())
                {
                    if (!entity.TryGetComponent<PainkillerStatusEffectComponent>(out var painkiller, factory))
                        continue;

                    painkillers++;
                    Assert.That(painkiller.Strength, Is.GreaterThan(0f), $"{entity.ID} masks nothing");

                    if (painkiller.Scope is { } scope)
                    {
                        Assert.That(scope, Is.Not.Empty, $"{entity.ID} has an empty scope, so it masks nothing");
                    }
                }

                Assert.That(painkillers, Is.GreaterThan(0), "no painkiller status effects found");

                RequireLoc(loc, "alerts-pain-name");
                RequireLoc(loc, "alerts-pain-desc");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertIcons(IPrototypeManager proto, string alertId, int expected)
    {
        if (!proto.TryIndex<AlertPrototype>(alertId, out var alert))
        {
            Assert.Fail($"alert {alertId} is missing");
            return;
        }

        Assert.That(alert.Icons, Has.Count.EqualTo(expected), $"alert {alertId} needs {expected} icons, one per severity");
    }

    private static void RequireLoc(ILocalizationManager loc, string key)
    {
        Assert.That(loc.HasString(key), $"missing locale key {key}");
    }
}
