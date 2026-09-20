using Robust.Shared.Audio;

namespace Content.Shared._Serenity.Economy;

/// <summary>
/// A machine that converts physical Federal Bills (SpaceCash) into physical credits (NTCredit)
/// and back. Operates purely on carried stacks — it never touches the station budget or
/// a player's credit ledger.
/// </summary>
[RegisterComponent]
public sealed partial class CurrencyExchangeComponent : Component
{
    /// <summary>
    /// How many Federal Bills one credit is worth. Federal Bills are the weaker currency: station-scale
    /// cash finds run to thousands, while a shift's wages are tens of credits, so parity
    /// would let a single loot crate outweigh weeks of salary.
    /// The YAML key stays "spesosPerCredit" — renaming a DataField would break the prototype.
    /// </summary>
    [DataField]
    public float SpesosPerCredit = 100f;

    /// <summary>
    /// Fraction skimmed off every exchange, applied in both directions so a round trip
    /// always loses value and cannot be farmed.
    /// </summary>
    [DataField]
    public float Fee = 0.1f;

    [DataField]
    public SoundSpecifier ExchangeSound = new SoundPathSpecifier("/Audio/Machines/chime.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}
