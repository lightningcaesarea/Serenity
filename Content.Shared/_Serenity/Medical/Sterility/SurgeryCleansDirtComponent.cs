using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Sterility;

/// <summary>
/// An item (soap) that can wipe surgical dirt off tools and gloves.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryCleansDirtComponent : Component
{
    /// <summary>
    /// Seconds per wipe. Wiping repeats until the item is clean.
    /// </summary>
    [DataField]
    public float CleanDelay = 3f;

    /// <summary>
    /// Dirtiness removed per wipe.
    /// </summary>
    [DataField]
    public float DirtAmount = 25f;

    /// <summary>
    /// Patients' DNA removed per wipe.
    /// </summary>
    [DataField]
    public int DnaAmount = 1;
}
