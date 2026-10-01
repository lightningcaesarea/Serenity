#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Server._Starlight.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules;
using Content.Shared.CCVar;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.GameRules;

/// <summary>
/// Secret used to fail intermittently in <c>StartEndGameRulesTest</c>: with no ready players ShitStation is the only
/// pickable preset, and a ShitStation roll in an earlier round on the same pooled server put it on a Dynamic
/// cooldown, so Secret deleted itself and the ticker then tried to start the deleted entity.
/// </summary>
[TestFixture]
[TestOf(typeof(SecretRuleSystem))]
public sealed class SecretRuleCooldownTest : GameTest
{
    private static readonly EntProtoId SecretRule = "Secret";
    private static readonly ProtoId<GamePresetPrototype> NoMinimumPreset = "ShitStation";

    [TestPrototypes]
    private const string Prototypes = """
    -   type: gamePreset
        id: TestSecretUnpickablePreset
        name: secret-title
        description: secret-description
        minPlayers: 1000
        rules: []

    -   type: weightedRandom
        id: TestSecretUnpickableWeights
        weights:
            TestSecretUnpickablePreset: 1
    """;

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        InLobby = true,
    };

    [Test]
    public async Task SecretIgnoresCooldownsWhenNothingElseIsPickable()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var cooldowns = server.System<DynamicRuleCooldownSystem>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        // An earlier round on this server rolled the only preset that needs no players.
        await server.WaitAssertion(() => cooldowns.ApplyPresetCooldown(prototypes.Index(NoMinimumPreset)));

        await server.WaitPost(() => ticker.RestartRound());
        await Pair.RunUntilSynced();

        await server.WaitAssertion(() =>
        {
            cooldowns.EnsureRoundInitialized(dynamicRound: false);

            Assert.Multiple(() =>
            {
                Assert.That(ticker.ReadyPlayerCount(), Is.Zero);
                Assert.That(cooldowns.TryGetPresetCooldown(NoMinimumPreset, out _), Is.True);
                Assert.That(ticker.StartGameRule(SecretRule, out var secret), Is.True);
                Assert.That(server.EntMan.EntityExists(secret), Is.True);
            });

            ticker.ClearGameRules();
        });
    }

    [Test]
    public async Task StartingSelfDeletingRuleReturnsFalse()
    {
        var server = Pair.Server;
        var ticker = server.System<GameTicker>();
        var original = server.CfgMan.GetCVar(CCVars.SecretWeightPrototype);

        // Secret logs an error when it cannot pick a preset; that is the expected path here.
        var failureLevel = Pair.ServerLogHandler.FailureLevel;
        Pair.ServerLogHandler.FailureLevel = LogLevel.Fatal;
        try
        {
            await server.WaitAssertion(() =>
            {
                server.CfgMan.SetCVar(CCVars.SecretWeightPrototype, "TestSecretUnpickableWeights");

                // Secret deletes itself while being added; starting it must not throw.
                Assert.That(ticker.StartGameRule(SecretRule, out var secret), Is.False);
                Assert.That(server.EntMan.EntityExists(secret), Is.False);

                ticker.ClearGameRules();
            });
        }
        finally
        {
            Pair.ServerLogHandler.FailureLevel = failureLevel;
            await server.WaitPost(() => server.CfgMan.SetCVar(CCVars.SecretWeightPrototype, original));
        }
    }
}
