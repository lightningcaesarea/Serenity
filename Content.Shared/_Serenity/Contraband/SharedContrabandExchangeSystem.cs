// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity: prices come from upstream's
// ContrabandComponent severity instead of a per-item tag, and the payout is Federal Bills.

using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Contraband;

[NetSerializable, Serializable]
public enum ContrabandExchangeUiKey : byte
{
    Key,
}

[NetSerializable, Serializable]
public sealed class ContrabandExchangeInterfaceState : BoundUserInterfaceState
{
    /// <summary>Total payout for everything currently on the pallets.</summary>
    public readonly int Appraisal;

    /// <summary>How many sellable items are on the pallets.</summary>
    public readonly int Count;

    /// <summary>Whether the console is on a grid and can trade.</summary>
    public readonly bool Enabled;

    public ContrabandExchangeInterfaceState(int appraisal, int count, bool enabled)
    {
        Appraisal = appraisal;
        Count = count;
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class ContrabandExchangeAppraiseMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ContrabandExchangeSellMessage : BoundUserInterfaceMessage;

public abstract partial class SharedContrabandExchangeSystem : EntitySystem;
