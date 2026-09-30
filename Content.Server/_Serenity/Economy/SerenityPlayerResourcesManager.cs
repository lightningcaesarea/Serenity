using System.Threading.Channels;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._NullLink;
using Content.Shared._Serenity.Economy;
using Content.Shared._Starlight;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Serenity.Economy;

/// <summary>
/// Database-backed replacement for <c>NullLinkPlayerResourcesManager</c>.
/// </summary>
/// <remarks>
/// Starlight stores player resources (Sector Credits, key <c>"credits"</c>) in NullLink, an external
/// Orleans cluster Serenity has no access to. With NullLink disabled the stock manager keeps balances
/// in memory only and every write silently succeeds, so money evaporates on disconnect.
///
/// This manager keeps the same in-memory <see cref="PlayerData.Resources"/> cache the rest of the
/// economy reads from, but:
/// <list type="bullet">
/// <item>loads the player's row set from the DB when they connect,</item>
/// <item>writes every change through to the DB via a single ordered queue,</item>
/// <item>records every change in an append-only transaction table for admin disputes.</item>
/// </list>
///
/// Changes made between connect and the DB load completing are accumulated in memory and merged on
/// top of the loaded values, and only start being persisted once the load has landed — otherwise a
/// salary paid during that window would be counted twice.
/// </remarks>
public sealed partial class SerenityPlayerResourcesManager : SharedNullLinkPlayerResourcesManager, ISerenityPlayerResourcesManager, IPostInjectInit
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ISharedPlayersRoleManager _playersRole = default!;


    private bool _initialized;

    // The server entry point never calls Initialize() on this interface (only the client does),
    // so hook IoC's post-injection callback to guarantee we start.
    void IPostInjectInit.PostInject() => Initialize();

    /// <summary>
    /// A queued DB write. Additive changes are sent as deltas so the database adds them itself and can't
    /// overwrite another writer (an admin adjusting an offline or just-connecting player); only an explicit
    /// "set" sends an absolute value.
    /// </summary>
    private readonly record struct ResourceWrite(Guid User, string Resource, bool Absolute, double Amount, string Reason);

    private readonly Channel<ResourceWrite> _writes = Channel.CreateUnbounded<ResourceWrite>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    /// <summary>Players whose DB state has been loaded and merged; only these have writes persisted.</summary>
    private readonly HashSet<Guid> _loaded = new();

    private Task? _writer;

    public override void Initialize()
    {
        if (_initialized)
            return;
        _initialized = true;

        base.Initialize();
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        // Started from the main thread so continuations resume there and DB calls originate there.
        _writer = ProcessWritesAsync();
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        switch (e.NewStatus)
        {
            case SessionStatus.Connected:
                _ = LoadAsync(e.Session);
                break;
            case SessionStatus.Disconnected:
                _loaded.Remove(e.Session.UserId);
                break;
        }
    }

    private async Task LoadAsync(ICommonSession session)
    {
        Dictionary<string, double> stored;
        try
        {
            stored = await _db.GetPlayerResources(session.UserId.UserId);
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to load resources for {session.Name} ({session.UserId}): {ex}");
            return;
        }

        // Starlight's PlayerRolesManager creates the in-memory PlayerData asynchronously after the
        // same Connected event, so it may not exist yet when our query returns. Wait for it instead of
        // giving up: giving up here silently disables persistence for the whole session.
        PlayerData? data = null;
        for (var attempt = 0; attempt < 600; attempt++)
        {
            if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie)
                return;

            if (_playersRole.GetPlayerData(session) is { } found)
            {
                data = found;
                break;
            }

            await Task.Delay(100);
        }

        if (data == null)
        {
            _sawmill.Error($"Player data for {session.Name} ({session.UserId}) never appeared; resources will not persist this session.");
            return;
        }

        // Anything already in memory accrued since connect (salary, purchases) and has not been
        // persisted yet; merge it on top of the stored value rather than discarding either side.
        foreach (var (resource, value) in stored)
        {
            data.Resources.TryGetValue(resource, out var accrued);
            data.Resources[resource] = value + accrued;
        }

        _loaded.Add(session.UserId);

        // Persist the accrued-during-load deltas exactly once. They go out as deltas, so if an admin changed the
        // stored balance after we read it, that change is kept instead of overwritten.
        foreach (var (resource, value) in data.Resources)
        {
            stored.TryGetValue(resource, out var before);
            if (value != before)
                EnqueueAdjust(session.UserId, resource, value - before, LedgerReasons.LoadMerge);
        }
    }

    #region Starlight interface (no reason available)

    public override bool TryUpdateResource(ICommonSession session, string id, double value, bool skipNullLink = false)
        => UpdateCore(session, id, value, LedgerReasons.Unspecified);

    public override bool TrySetResource(ICommonSession session, string id, double value, bool skipNullLink = false)
        => SetCore(session, id, value, LedgerReasons.Unspecified);

    #endregion

    #region Serenity interface (reason recorded in the ledger)

    public bool TryUpdateResource(ICommonSession session, string id, double delta, string reason)
        => UpdateCore(session, id, delta, reason);

    public bool TryUpdateResource(EntityUid uid, string id, double delta, string reason)
        => _players.TryGetSessionByEntity(uid, out var session) && UpdateCore(session, id, delta, reason);

    public bool TrySetResource(ICommonSession session, string id, double value, string reason)
        => SetCore(session, id, value, reason);

    #endregion

    private bool UpdateCore(ICommonSession session, string id, double delta, string reason)
    {
        // Match the stock manager: a zero delta is not a change and must not spam the ledger.
        if (delta == 0 || !base.TryUpdateResource(session, id, delta))
            return false;

        if (_loaded.Contains(session.UserId))
            EnqueueAdjust(session.UserId, id, delta, reason);

        return true;
    }

    private bool SetCore(ICommonSession session, string id, double value, string reason)
    {
        if (_playersRole.GetPlayerData(session) == null)
            return false;

        if (!base.TrySetResource(session, id, value))
            return false;

        if (_loaded.Contains(session.UserId))
            Enqueue(new ResourceWrite(session.UserId.UserId, id, true, value, reason));

        return true;
    }

    private void EnqueueAdjust(NetUserId user, string resource, double delta, string reason)
        => Enqueue(new ResourceWrite(user.UserId, resource, false, delta, reason));

    private void Enqueue(ResourceWrite write)
    {
        if (!_writes.Writer.TryWrite(write))
            _sawmill.Error($"Dropped resource write for {write.User}: {write.Resource} {(write.Absolute ? "=" : "+=")} {write.Amount}");
    }

    private async Task ProcessWritesAsync()
    {
        await foreach (var write in _writes.Reader.ReadAllAsync())
        {
            try
            {
                if (write.Absolute)
                    await _db.SetPlayerResource(write.User, write.Resource, write.Amount, write.Reason);
                else
                    await _db.AdjustPlayerResource(write.User, write.Resource, write.Amount, write.Reason);
            }
            catch (Exception ex)
            {
                // Never let one bad row stall the queue for every other player.
                _sawmill.Error($"Failed to persist resource {write.Resource} {(write.Absolute ? "=" : "+=")} {write.Amount} for {write.User}: {ex}");
            }
        }
    }
}
