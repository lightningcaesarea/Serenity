using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Consent;

/// <summary>
/// A single yes/no consent flag a player sets for themselves.
/// </summary>
[Prototype]
public sealed partial class ConsentTogglePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; set; } = string.Empty;

    [DataField]
    public string Description { get; set; } = string.Empty;

    [DataField]
    public string Category { get; set; } = "general";

    /// <summary>
    /// Value used when the player has never touched this toggle. Anything that can happen
    /// to a player without their own input should default to false.
    /// </summary>
    [DataField]
    public bool Default;
}
