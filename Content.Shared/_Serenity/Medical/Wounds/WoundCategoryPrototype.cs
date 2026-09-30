using Content.Shared.Alert;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// A kind of injury (fracture, burn, laceration…). Wound types belong to a category, and everything that
/// treats injuries by kind (pain, painkiller scope, surgery steps, examine text, HUD alerts, movement and
/// item-drop effects) is keyed on the category, so adding one is YAML only.
/// </summary>
[Prototype]
public sealed partial class WoundCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// True for categories that aren't stored as wound entries because they are computed from something else
    /// (bleeding comes from the bloodstream). They still take part in pain, but nothing can clear them here.
    /// </summary>
    [DataField]
    public bool Derived;

    /// <summary>
    /// False for wounds that don't heal by themselves (infection): their own system decides how they progress.
    /// </summary>
    [DataField]
    public bool Decays = true;

    /// <summary>
    /// False for categories that damage never causes; their wounds are only added directly (infection from dirty
    /// surgery or an untreated open wound). Their wound types list no damage types.
    /// </summary>
    [DataField]
    public bool DamageTriggered = true;

    /// <summary>
    /// HUD alert shown at the mob's worst tier in this category, one icon per tier. No alert if unset.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype>? Alert;

    /// <summary>
    /// Locale key prefix of the examine line for the worst tier: <c>{prefix}-{tier}</c>. No line if unset.
    /// </summary>
    [DataField]
    public string? ExamineLoc;
}
