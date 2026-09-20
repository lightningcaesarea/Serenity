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

    private const string ReasonUnspecified = "unspecified";
    private const string ReasonLoadMerge = "load-merge";

    private bool _initialized;

    // The server entry point never calls Initialize() on this interface (only the client does),
    // so hook IoC's post-injection callback to guarantee we start.
    void IPostInjectInit.PostInject() => Initialize();

    private readonly record struct ResourceWrite(Guid User, string Resource, double Value, double Delta, string Reason);

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

        // The player may have left while the query was in flight.
        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie
            || _playersRole.GetPlayerData(session) is not { } data)
            return;

        // Anything already in memory accrued since connect (salary, purchases) and has not been
        // persisted yet; merge it on top of the stored value rather than discarding either side.
        foreach (var (resource, value) in stored)
        {
            data.Resources.TryGetValue(resource, out var accrued);
            data.Resources[resource] = value + accrued;
        }

        _loaded.Add(session.UserId);

        // Persist the merged state so the accrued-during-load deltas reach the DB exactly once.
        foreach (var (resource, value) in data.Resources)
        {
            stored.TryGetValue(resource, out var before);
            if (value != before)
                Enqueue(session.UserId, resource, value, value - before, ReasonLoadMerge);
        }
    }

    #region Starlight interface (no reason available)

    public override bool TryUpdateResource(ICommonSession session, string id, double value, bool skipNullLink = false)
        => UpdateCore(session, id, value, ReasonUnspecified);

    public override bool TrySetResource(ICommonSession session, string id, double value, bool skipNullLink = false)
        => SetCore(session, id, value, ReasonUnspecified);

    #endregion

    #region Serenity interface (reason recorded in the ledger)

    public bool TryUpdateResource(ICommonSession session, string id, double delta, string reason)
        => UpdateCore(session, id, delta, reason);

    public bool TrySetResource(ICommonSession session, string id, double value, string reason)
        => SetCore(session, id, value, reason);

    #endregion

    private bool UpdateCore(ICommonSession session, string id, double delta, string reason)
    {
        // Match the stock manager: a zero delta is not a change and must not spam the ledger.
        if (delta == 0 || !base.TryUpdateResource(session, id, delta))
            return false;

        if (_loaded.Contains(session.UserId) && _playersRole.GetPlayerData(session) is { } data)
            Enqueue(session.UserId, id, data.Resources[id], delta, reason);

        return true;
    }

    private bool SetCore(ICommonSession session, string id, double value, string reason)
    {
        if (_playersRole.GetPlayerData(session) is not { } data)
            return false;

        data.Resources.TryGetValue(id, out var before);

        if (!base.TrySetResource(session, id, value))
            return false;

        if (_loaded.Contains(session.UserId))
            Enqueue(session.UserId, id, value, value - before, reason);

        return true;
    }

    private void Enqueue(NetUserId user, string resource, double value, double delta, string reason)
    {
        if (!_writes.Writer.TryWrite(new ResourceWrite(user.UserId, resource, value, delta, reason)))
            _sawmill.Error($"Dropped resource write for {user}: {resource} -> {value}");
    }

    private async Task ProcessWritesAsync()
    {
        await foreach (var write in _writes.Reader.ReadAllAsync())
        {
            try
            {
                await _db.SetPlayerResource(write.User, write.Resource, write.Value, write.Delta, write.Reason);
            }
            catch (Exception ex)
            {
                // Never let one bad row stall the queue for every other player.
                _sawmill.Error($"Failed to persist resource {write.Resource}={write.Value} for {write.User}: {ex}");
            }
        }
    }
}
