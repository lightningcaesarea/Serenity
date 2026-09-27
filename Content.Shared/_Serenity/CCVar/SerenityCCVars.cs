using Robust.Shared.Configuration;

namespace Content.Shared._Serenity.CCVar;

/// <summary>
/// CVars owned by Serenity. Keep them under the <c>serenity.</c> prefix.
/// </summary>
[CVarDefs]
public sealed partial class SerenityCCVars
{
    /// <summary>
    /// Whether an automated bus runs between every grid carrying a StationTransit component.
    /// Needs at least two stops to be useful.
    /// </summary>
    public static readonly CVarDef<bool> PublicTransit =
        CVarDef.Create("serenity.publictransit.enabled", false, CVar.SERVERONLY);

    /// <summary>
    /// Grid file loaded as the bus.
    /// </summary>
    public static readonly CVarDef<string> PublicTransitBusMap =
        CVarDef.Create("serenity.publictransit.bus_map", "/Maps/_Serenity/Shuttles/publicts.yml", CVar.SERVERONLY);

    /// <summary>
    /// Seconds the bus waits at each stop.
    /// </summary>
    public static readonly CVarDef<float> PublicTransitWaitTime =
        CVarDef.Create("serenity.publictransit.wait_time", 150f, CVar.SERVERONLY);

    /// <summary>
    /// Seconds the bus spends in FTL between stops.
    /// </summary>
    public static readonly CVarDef<float> PublicTransitFlyTime =
        CVarDef.Create("serenity.publictransit.fly_time", 145f, CVar.SERVERONLY);

    /// <summary>
    /// Whether points of interest are loaded onto the default map at round start.
    /// </summary>
    public static readonly CVarDef<bool> PoiSpawnerEnabled =
        CVarDef.Create("serenity.poi.enabled", true, CVar.SERVERONLY);
}
