// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

namespace Content.Server._Serenity.PublicTransit;

/// <summary>
/// Marks a grid as a bus stop. Give it a docking airlock with the DockTransit priority tag.
/// </summary>
[RegisterComponent, Access(typeof(PublicTransitSystem))]
public sealed partial class StationTransitComponent : Component;
