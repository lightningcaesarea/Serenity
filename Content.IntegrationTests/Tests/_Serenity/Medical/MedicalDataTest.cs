using System.Collections.Generic;
using System.Linq;
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
/// arrays that don't match the tier count, pain bands out of order, damage types or categories that don't exist,
/// and locale keys that are built by string interpolation at runtime.
/// </summary>
[TestFixture]
public sealed class MedicalDataTest
{
    private const int TierCount = WoundsConstants.MaxWoundTier;

    private static readonly ProtoId<AlertPrototype> PainAlert = "Pain";

    [Test]
    public async Task CategoriesAndWoundTypesAreConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var proto = server.ProtoMan;
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                // The categories code refers to directly must exist
                foreach (var id in new[]
                         {
                             WoundCategoryIds.Bleeding, WoundCategoryIds.Fracture, WoundCategoryIds.Burn,
                             WoundCategoryIds.Laceration, WoundCategoryIds.Puncture,
                         })
                {
                    Assert.That(proto.HasIndex(id), $"wound category {id} is missing");
                }

                foreach (var category in proto.EnumeratePrototypes<WoundCategoryPrototype>())
                {
                    if (category.Alert is { } alert)
                    {
                        Assert.That(proto.TryIndex<AlertPrototype>(alert, out var alertProto), $"{category.ID} uses unknown alert {alert}");
                        if (alertProto != null)
                            Assert.That(alertProto.Icons, Has.Count.EqualTo(TierCount), $"alert {alert} needs {TierCount} icons, one per tier");
                    }

                    if (category.ExamineLoc is { } prefix)
                    {
                        for (var tier = 1; tier <= TierCount; tier++)
                        {
                            RequireLoc(loc, $"{prefix}-{tier}");
                        }
                    }
                }

                foreach (var wound in proto.EnumeratePrototypes<WoundTypePrototype>())
                {
                    Assert.That(proto.TryIndex(wound.Category, out var category), $"{wound.ID} uses unknown category {wound.Category}");
                    if (category != null)
                        Assert.That(category.Derived, Is.False, $"{wound.ID} is in derived category {category.ID}, which can't hold wound entries");

                    Assert.That(wound.Damage, Is.Not.Empty, $"{wound.ID} is caused by no damage type");
                    foreach (var (type, weight) in wound.Damage)
                    {
                        Assert.That(proto.HasIndex<DamageTypePrototype>(type), $"{wound.ID} uses unknown damage type {type}");
                        Assert.That(weight, Is.GreaterThan(0f), $"{wound.ID} weight for {type} must be positive");
                    }

                    Assert.That(wound.Thresholds, Has.Length.EqualTo(TierCount), $"{wound.ID} needs {TierCount} thresholds");
                    Assert.That(wound.Thresholds, Is.Ordered.Ascending, $"{wound.ID} thresholds must increase with tier");

                    for (var tier = 1; tier <= TierCount; tier++)
                    {
                        RequireLoc(loc, $"wound-{wound.ID.ToLowerInvariant()}-{tier}");
                    }
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

            var categories = proto.EnumeratePrototypes<WoundCategoryPrototype>().Select(c => c.ID).ToHashSet();

            Assert.Multiple(() =>
            {
                foreach (var config in proto.EnumeratePrototypes<WoundConfigPrototype>())
                {
                    Assert.That(config.TierDecaySeconds, Has.Length.EqualTo(TierCount), $"{config.ID} needs {TierCount} decay times");
                    Assert.That(config.TierDecaySeconds, Has.All.GreaterThan(0f), $"{config.ID} decay times must be positive");
                    Assert.That(config.RegenTickSeconds, Is.GreaterThan(0f), $"{config.ID} regen tick must be positive");
                    Assert.That(config.MaxStackedWoundsPerType, Is.GreaterThanOrEqualTo(1), $"{config.ID} stack cap");
                    Assert.That(config.BleedSources, Is.Not.Empty, $"{config.ID} lists no bleed sources");

                    foreach (var source in config.BleedSources)
                    {
                        Assert.That(proto.HasIndex<DamageTypePrototype>(source), $"{config.ID} bleed source {source} is not a damage type");

                        // The health analyzer builds wound-bleed-<type>-<tier> from the recorded source
                        for (var tier = 1; tier <= TierCount; tier++)
                        {
                            RequireLoc(loc, $"wound-bleed-{source.Id.ToLowerInvariant()}-{tier}");
                        }
                    }

                    foreach (var (category, effects) in config.Categories)
                    {
                        Assert.That(categories, Does.Contain(category.Id), $"{config.ID} configures unknown category {category}");
                        Assert.That(effects.SlowTier, Is.InRange(0, TierCount), $"{config.ID} {category} slow tier");
                        Assert.That(effects.DropTier, Is.InRange(0, TierCount), $"{config.ID} {category} drop tier");
                        Assert.That(effects.SlowMultiplier, Is.InRange(0.01f, 1f), $"{config.ID} {category} slow multiplier");
                        Assert.That(effects.DropChance, Is.InRange(0f, 1f), $"{config.ID} {category} drop chance");
                    }
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

                    // Every category, including derived ones like bleeding, must say how much pain it causes
                    foreach (var category in categories)
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

                    foreach (var category in config.WoundWeights.Keys)
                    {
                        Assert.That(categories, Does.Contain(category.Id), $"{config.ID} has weights for unknown category {category}");
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
            var categories = proto.EnumeratePrototypes<WoundCategoryPrototype>().Select(c => c.ID).ToHashSet();

            Assert.Multiple(() =>
            {
                // Pain has one alert severity per band above None
                Assert.That(proto.TryIndex(PainAlert, out var pain), "the Pain alert is missing");
                if (pain != null)
                    Assert.That(pain.Icons, Has.Count.EqualTo((int) PainLevel.Agonizing), "the Pain alert needs one icon per pain band");

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
                        foreach (var category in scope)
                        {
                            Assert.That(categories, Does.Contain(category.Id), $"{entity.ID} scope names unknown category {category}");
                        }
                    }
                }

                Assert.That(painkillers, Is.GreaterThan(0), "no painkiller status effects found");

                RequireLoc(loc, "alerts-pain-name");
                RequireLoc(loc, "alerts-pain-desc");
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void RequireLoc(ILocalizationManager loc, string key)
    {
        Assert.That(loc.HasString(key), $"missing locale key {key}");
    }
}
