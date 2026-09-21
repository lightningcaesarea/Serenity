// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Events;
using Content.Shared._Serenity.BountyContracts;
using Content.Shared.Database;
using Robust.Shared.Map;

namespace Content.Server._Serenity.BountyContracts;

/// <summary>
/// Player-posted bounty contracts, browsed and posted from a PDA cartridge.
/// </summary>
public sealed partial class BountyContractSystem : SharedBountyContractSystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        InitializeUi();
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        // Drop any store left over from a previous round or placed on a map.
        var query = EntityQueryEnumerator<BountyContractDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RemCompDeferred<BountyContractDataComponent>(uid);
        }

        var store = Spawn(null, MapCoordinates.Nullspace);
        EnsureComp<BountyContractDataComponent>(store);
    }

    private BountyContractDataComponent? GetContracts()
    {
        var query = EntityQueryEnumerator<BountyContractDataComponent>();
        while (query.MoveNext(out _, out var data))
        {
            return data;
        }

        return null;
    }

    /// <summary>
    /// Posts a new contract. Returns null when there is no store this round.
    /// </summary>
    public BountyContract? CreateBountyContract(BountyContractCategory category,
        string name,
        int reward,
        string? description = null,
        string? vessel = null,
        string? dna = null,
        string? author = null,
        bool postToRadio = true)
    {
        var data = GetContracts();
        if (data == null)
            return null;

        var contractId = data.LastId++;
        var contract = new BountyContract(contractId, category, name, reward, dna, vessel, description, author);

        if (!data.Contracts.TryAdd(contractId, contract))
        {
            Log.Error($"Failed to store bounty contract {contractId}; LastId is {data.LastId}.");
            return null;
        }

        if (postToRadio)
        {
            var sender = Loc.GetString("bounty-contracts-radio-name");
            var target = !string.IsNullOrEmpty(contract.Vessel)
                ? $"{contract.Name} ({contract.Vessel})"
                : contract.Name;
            var msg = Loc.GetString("bounty-contracts-radio-create", ("target", target), ("reward", contract.Reward));
            _chat.DispatchGlobalAnnouncement(msg, sender, playSound: false, colorOverride: Color.FromHex("#D7D7BE"));
        }

        return contract;
    }

    public bool TryGetContract(uint contractId, [NotNullWhen(true)] out BountyContract? contract)
    {
        contract = null;
        return GetContracts()?.Contracts.TryGetValue(contractId, out contract) ?? false;
    }

    public IEnumerable<BountyContract> GetAllContracts()
    {
        return GetContracts()?.Contracts.Values ?? Enumerable.Empty<BountyContract>();
    }

    public bool RemoveBountyContract(uint contractId)
    {
        var data = GetContracts();
        if (data == null)
            return false;

        if (!data.Contracts.Remove(contractId))
        {
            Log.Warning($"Failed to remove bounty contract {contractId}: not found.");
            return false;
        }

        return true;
    }
}
