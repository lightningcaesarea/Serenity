// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

namespace Content.Shared._Serenity.Shipyard.Components;

/// <summary>
/// Put on an ID card mapped inside a ship. On map init the card copies the deed of the grid
/// it is sitting on, so pre-built ships can ship with a spare deed aboard.
/// </summary>
[RegisterComponent]
public sealed partial class StationDeedSpawnerComponent : Component;
