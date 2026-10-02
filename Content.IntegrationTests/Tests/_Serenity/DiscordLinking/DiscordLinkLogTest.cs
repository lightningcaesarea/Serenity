using Content.Server._Serenity.DiscordLinking;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._Serenity.DiscordLinking;

[TestFixture]
public sealed class DiscordLinkLogTest
{
    private const string DiscordName = "cool_discord_name";
    private const ulong DiscordId = 1234567890123456789;
    private const string Player = "CoolPlayer";
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    /// <summary>
    /// Discord delivers commands, modal submissions and member events on its own threads, not the game's main thread. The
    /// static Loc looks its service up through a per-thread container, so on those threads it throws a
    /// NullReferenceException: that is what made !linkpanel (and the whole Discord side of linking) fail. This proves it
    /// still does, so the next test means something.
    /// </summary>
    [Test]
    public async Task StaticLocFailsOffTheMainThread()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            Assert.Throws<NullReferenceException>(() =>
                Task.Run(() => Loc.GetString("serenity-discord-link-log-unlinked")).GetAwaiter().GetResult());
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Every line posted to the staff log channel says who the player is on both sides, so nobody has to look them up:
    /// the Discord username and ID and the SS14 name and user ID. They are built here on a thread-pool thread, as Discord
    /// does, so they must not depend on the main thread.
    /// </summary>
    [Test]
    public async Task LogLinesNameBothAccountsAndTheirIds()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var links = pair.Server.ResolveDependency<DiscordAccountLinkManager>();
            var lines = Task.Run(() => new[]
            {
                links.LogLinked(DiscordName, DiscordId, Player, UserId),
                links.LogRemoved(DiscordName, DiscordId, Player, UserId),
            }).GetAwaiter().GetResult();

            foreach (var line in lines)
            {
                Assert.That(line, Does.Contain(DiscordName), $"Discord username in: {line}");
                Assert.That(line, Does.Contain(DiscordId.ToString()), $"Discord ID in: {line}");
                Assert.That(line, Does.Contain(Player), $"SS14 name in: {line}");
                Assert.That(line, Does.Contain(UserId.ToString()), $"SS14 user ID in: {line}");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// An unlink line carries the link summary (which names both accounts and IDs) and who removed it, with their ID.
    /// </summary>
    [Test]
    public async Task UnlinkLineNamesWhoDidIt()
    {
        await using var pair = await PoolManager.GetServerClient();

        await pair.Server.WaitAssertion(() =>
        {
            var links = pair.Server.ResolveDependency<DiscordAccountLinkManager>();
            var line = Task.Run(() => links.LogUnlinked("the link summary", "StaffMember", "99999999-8888-7777-6666-555555555555"))
                .GetAwaiter().GetResult();

            Assert.That(line, Does.Contain("the link summary"));
            Assert.That(line, Does.Contain("StaffMember"));
            Assert.That(line, Does.Contain("99999999-8888-7777-6666-555555555555"));
        });

        await pair.CleanReturnAsync();
    }
}
