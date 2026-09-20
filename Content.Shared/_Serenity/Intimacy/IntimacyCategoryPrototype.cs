using Content.Shared._Serenity.Consent;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// A tab in the intimacy window. Every act in the category inherits its consent requirements.
/// </summary>
[Prototype]
public sealed partial class IntimacyCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public Color Color = Color.White;

    [DataField]
    public int Order;

    /// <summary>
    /// Consent toggles that <b>both</b> participants must allow before any act in this category
    /// is offered. The global lewd-interactions toggle is always required on top of these.
    /// </summary>
    [DataField]
    public List<ProtoId<ConsentTogglePrototype>> Consent = new();
}
