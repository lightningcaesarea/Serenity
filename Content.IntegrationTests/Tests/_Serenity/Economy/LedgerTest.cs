using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._Serenity.Economy;

namespace Content.IntegrationTests.Tests._Serenity.Economy;

[TestFixture]
public sealed class LedgerTest
{
    private const string Credits = "credits";

    /// <summary>
    /// Additive changes are applied by the database itself, so many writers adding at once can't overwrite each
    /// other, and every ledger row reports the real balance after its own change.
    /// </summary>
    [Test]
    public async Task ConcurrentAdjustmentsAreNotLost()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var player = Guid.NewGuid();

        const int writers = 40;
        var results = await Task.WhenAll(Enumerable.Range(0, writers)
            .Select(_ => db.AdjustPlayerResource(player, Credits, 5, LedgerReasons.Donate)));

        var stored = await db.GetPlayerResources(player);
        Assert.That(stored[Credits], Is.EqualTo(5 * writers), "no addition may be lost");
        Assert.That(results.Max(), Is.EqualTo(5 * writers));
        Assert.That(results.Distinct().Count(), Is.EqualTo(writers), "each adjustment should see its own resulting balance");

        var ledger = await db.GetPlayerResourceTransactions(player, 100);
        Assert.That(ledger, Has.Count.EqualTo(writers));
        Assert.That(ledger.Sum(t => t.Delta), Is.EqualTo(5 * writers));
        Assert.That(ledger.Select(t => t.BalanceAfter).Distinct().Count(), Is.EqualTo(writers));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Setting an exact value records the real delta (computed from the stored value), and a later adjustment
    /// builds on it.
    /// </summary>
    [Test]
    public async Task SetRecordsTheRealDelta()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var player = Guid.NewGuid();

        await db.SetPlayerResource(player, Credits, 100, "first");
        var afterAdjust = await db.AdjustPlayerResource(player, Credits, 50, "second");
        await db.SetPlayerResource(player, Credits, 20, "third");

        Assert.That(afterAdjust, Is.EqualTo(150));
        Assert.That((await db.GetPlayerResources(player))[Credits], Is.EqualTo(20));

        // Newest first
        var ledger = await db.GetPlayerResourceTransactions(player, 10);
        Assert.That(ledger.Select(t => (t.Reason, t.Delta, t.BalanceAfter)), Is.EqualTo(new[]
        {
            ("third", -130d, 20d),
            ("second", 50d, 150d),
            ("first", 100d, 100d),
        }));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A reason is admin-supplied free text, so it is cut to a sane length instead of growing the ledger without limit.
    /// </summary>
    [Test]
    public async Task LongReasonsAreTrimmed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var db = pair.Server.ResolveDependency<IServerDbManager>();
        var player = Guid.NewGuid();

        await db.AdjustPlayerResource(player, Credits, 1, LedgerReasons.Admin("someone", new string('x', 5000)));

        var ledger = await db.GetPlayerResourceTransactions(player, 1);
        Assert.That(ledger.Single().Reason, Has.Length.LessThanOrEqualTo(256));

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The two sides of an ATM transfer each name the other player, so they can be matched up in the ledger.
    /// </summary>
    [Test]
    public void TransferReasonsPairUp()
    {
        var sender = Guid.NewGuid();
        var recipient = Guid.NewGuid();

        Assert.That(LedgerReasons.TransferOut(recipient), Does.Contain(recipient.ToString()));
        Assert.That(LedgerReasons.TransferIn(sender), Does.Contain(sender.ToString()));
        Assert.That(LedgerReasons.TransferOut(recipient), Is.Not.EqualTo(LedgerReasons.TransferIn(sender)));
    }
}
