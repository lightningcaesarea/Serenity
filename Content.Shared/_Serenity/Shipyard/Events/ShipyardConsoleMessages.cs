// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Shipyard.Events;

/// <summary>
/// Buy the vessel with this prototype ID.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsolePurchaseMessage : BoundUserInterfaceMessage
{
    public string Vessel { get; }

    public ShipyardConsolePurchaseMessage(string vessel)
    {
        Vessel = vessel;
    }
}

/// <summary>
/// Sell the ship deeded to the inserted ID card. Everything is validated server-side.
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsoleSellMessage : BoundUserInterfaceMessage;
