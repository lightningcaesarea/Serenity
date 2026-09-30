using Robust.Shared.GameStates;

namespace Content.Shared._Serenity.Medical.Sterility;

/// <summary>
/// Dirt and other patients' DNA picked up by a surgical tool or a pair of gloves. Added the first time the item
/// is used in surgery; wiping it with soap (<see cref="SurgeryCleansDirtComponent"/>) removes it again.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedSterilitySystem))]
public sealed partial class SurgeryDirtinessComponent : Component
{
    /// <summary>
    /// 0 is sterile; <see cref="SterilityConfigPrototype.MaxDirtiness"/> is filthy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Dirtiness;

    /// <summary>
    /// DNA of the patients this item has touched since it was last cleaned.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<string> Dnas = new();
}
