// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Shared._Serenity.Shipyard.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Shipyard.Components;

/// <summary>
/// Adds specific vessels to a shipyard console's catalogue regardless of group.
/// </summary>
[RegisterComponent]
public sealed partial class ShipyardListingComponent : Component
{
    [DataField]
    public List<ProtoId<VesselPrototype>> Shuttles = new();
}
