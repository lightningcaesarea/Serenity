// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Serenity.Smuggling;

/// <summary>
/// An object that, when searched, summons a smuggler drop pod somewhere in deep space and hands the
/// searcher a note with its coordinates. Security is tipped off that a drop happened.
/// </summary>
[RegisterComponent, Access(typeof(DeadDropSystem))]
public sealed partial class DeadDropComponent : Component
{
    /// <summary>When the next drop becomes available. Set on startup and after each use.</summary>
    [DataField]
    public TimeSpan? NextDrop;

    /// <summary>Shortest wait between drops, in seconds.</summary>
    [DataField]
    public int MinimumCoolDown = 900;

    /// <summary>Longest wait between drops, in seconds.</summary>
    [DataField]
    public int MaximumCoolDown = 5400;

    /// <summary>Closest the pod can land to the map origin, in tiles.</summary>
    [DataField]
    public int MinimumDistance = 3000;

    /// <summary>Farthest the pod can land from the map origin, in tiles.</summary>
    [DataField]
    public int MaximumDistance = 6000;

    /// <summary>Paper handed to the searcher with the coordinates on it.</summary>
    [DataField]
    public EntProtoId HintPaper = "Paper";

    /// <summary>Grid file loaded as the drop pod.</summary>
    [DataField]
    public ResPath DropGrid = new("/Maps/_Serenity/Shuttles/deaddrop.yml");

    /// <summary>Radar colour of the pod.</summary>
    [DataField]
    public Color Color = new(225, 15, 155);

    /// <summary>Channel that hears about the drop.</summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> ReportChannel = "Security";

    /// <summary>Seconds the pod spends in FTL before arriving.</summary>
    [DataField]
    public float TravelTime = 35f;
}
