// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity: cartridge messages now derive from
// CartridgeMessageEvent so the loader relays them to the cartridge entity.

using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.BountyContracts;

[Serializable, NetSerializable]
public enum BountyContractCategory : byte
{
    Criminal,
    Vacancy,
    Construction,
    Service,
    Other,
}

[Serializable, NetSerializable]
public struct BountyContractCategoryMeta
{
    public string Name;
    public Color UiColor;
}

/// <summary>
/// A person a contract can be posted against. Two entries are the same person when their DNA matches.
/// </summary>
[NetSerializable, Serializable]
public struct BountyContractTargetInfo : IEquatable<BountyContractTargetInfo>
{
    public string Name;
    public string? DNA;

    public bool Equals(BountyContractTargetInfo other) => DNA == other.DNA;
    public override bool Equals(object? obj) => obj is BountyContractTargetInfo other && Equals(other);
    public override int GetHashCode() => DNA?.GetHashCode() ?? 0;
}

[NetSerializable, Serializable]
public struct BountyContractRequest
{
    public BountyContractCategory Category;
    public string Name;
    public string? DNA;
    public string Vessel;
    public int Reward;
    public string Description;
}

[NetSerializable, Serializable]
public sealed class BountyContract
{
    public readonly uint ContractId;
    public readonly BountyContractCategory Category;
    public readonly string Name;
    public readonly int Reward;
    public readonly string? DNA;
    public readonly string? Vessel;
    public readonly string? Description;
    public readonly string? Author;

    public BountyContract(uint contractId, BountyContractCategory category, string name,
        int reward, string? dna, string? vessel, string? description, string? author)
    {
        ContractId = contractId;
        Category = category;
        Name = name;
        Reward = reward;
        DNA = dna;
        Vessel = vessel;
        Description = description;
        Author = author;
    }
}

[NetSerializable, Serializable]
public sealed class BountyContractCreateUiState : BoundUserInterfaceState
{
    public readonly List<BountyContractTargetInfo> Targets;
    public readonly List<string> Vessels;

    public BountyContractCreateUiState(List<BountyContractTargetInfo> targets, List<string> vessels)
    {
        Targets = targets;
        Vessels = vessels;
    }
}

[NetSerializable, Serializable]
public sealed class BountyContractListUiState : BoundUserInterfaceState
{
    public readonly List<BountyContract> Contracts;
    public readonly bool IsAllowedCreateBounties;
    public readonly bool IsAllowedRemoveBounties;

    public BountyContractListUiState(List<BountyContract> contracts, bool isAllowedCreateBounties, bool isAllowedRemoveBounties)
    {
        Contracts = contracts;
        IsAllowedCreateBounties = isAllowedCreateBounties;
        IsAllowedRemoveBounties = isAllowedRemoveBounties;
    }
}

[NetSerializable, Serializable]
public sealed class BountyContractOpenCreateUiMsg : CartridgeMessageEvent;

[NetSerializable, Serializable]
public sealed class BountyContractRefreshListUiMsg : CartridgeMessageEvent;

[NetSerializable, Serializable]
public sealed class BountyContractCloseCreateUiMsg : CartridgeMessageEvent;

[NetSerializable, Serializable]
public sealed class BountyContractTryRemoveUiMsg : CartridgeMessageEvent
{
    public readonly uint ContractId;

    public BountyContractTryRemoveUiMsg(uint contractId)
    {
        ContractId = contractId;
    }
}

[NetSerializable, Serializable]
public sealed class BountyContractTryCreateMsg : CartridgeMessageEvent
{
    public readonly BountyContractRequest Contract;

    public BountyContractTryCreateMsg(BountyContractRequest contract)
    {
        Contract = contract;
    }
}

public abstract partial class SharedBountyContractSystem : EntitySystem
{
    public const int DefaultReward = 500;
    public const int MaxNameLength = 64;
    public const int MaxVesselLength = 64;
    public const int MaxDescriptionLength = 512;

    public static readonly Dictionary<BountyContractCategory, BountyContractCategoryMeta> CategoriesMeta = new()
    {
        [BountyContractCategory.Criminal] = new BountyContractCategoryMeta
        {
            Name = "bounty-contracts-category-criminal",
            UiColor = Color.FromHex("#520c0c"),
        },
        [BountyContractCategory.Vacancy] = new BountyContractCategoryMeta
        {
            Name = "bounty-contracts-category-vacancy",
            UiColor = Color.FromHex("#003866"),
        },
        [BountyContractCategory.Construction] = new BountyContractCategoryMeta
        {
            Name = "bounty-contracts-category-construction",
            UiColor = Color.FromHex("#664a06"),
        },
        [BountyContractCategory.Service] = new BountyContractCategoryMeta
        {
            Name = "bounty-contracts-category-service",
            UiColor = Color.FromHex("#01551e"),
        },
        [BountyContractCategory.Other] = new BountyContractCategoryMeta
        {
            Name = "bounty-contracts-category-other",
            UiColor = Color.FromHex("#474747"),
        },
    };
}
