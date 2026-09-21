// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Shared._Serenity.BountyContracts;

namespace Content.Server._Serenity.BountyContracts;

/// <summary>
/// Round-scoped store of every open bounty contract. Lives on a nullspace entity.
/// </summary>
[RegisterComponent, Access(typeof(BountyContractSystem))]
public sealed partial class BountyContractDataComponent : Component
{
    [DataField]
    public uint LastId;

    [DataField]
    public Dictionary<uint, BountyContract> Contracts = new();
}
