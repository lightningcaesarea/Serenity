// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Serenity.PublicTransit;

/// <summary>
/// A grid that runs the bus route. Added to the loaded bus grid by <see cref="PublicTransitSystem"/>;
/// mappers may also put it on a shuttle to force it onto the route.
/// </summary>
[RegisterComponent, Access(typeof(PublicTransitSystem))]
public sealed partial class TransitShuttleComponent : Component
{
    [DataField]
    public EntityUid NextStation;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTransfer;
}
