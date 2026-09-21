// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Serenity.Shipyard.Prototypes;

/// <summary>
/// A ship for sale at a shipyard console.
/// </summary>
[Prototype]
public sealed partial class VesselPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    /// <summary>
    /// Price in Sector Credits.
    /// </summary>
    [DataField(required: true)]
    public int Price;

    /// <summary>
    /// Size class shown as a filter in the console (Small, Medium, Large, ...).
    /// </summary>
    [DataField]
    public string Category = string.Empty;

    /// <summary>
    /// Which consoles list this vessel. Matched against <c>groups</c> on the console.
    /// </summary>
    [DataField]
    public string Group = "Civilian";

    /// <summary>
    /// Grid file to load, e.g. <c>/Maps/_Starlight/Shuttles/Shipyard/pts.yml</c>.
    /// </summary>
    [DataField(required: true)]
    public ResPath ShuttlePath = default!;

    /// <summary>
    /// Seconds between purchase and the ship docking at the station.
    /// </summary>
    [DataField]
    public float Delay = 10f;
}
