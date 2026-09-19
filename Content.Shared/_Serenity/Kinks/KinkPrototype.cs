using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Kinks;

[Prototype]
public sealed partial class KinkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name { get; set; } = string.Empty;

    [DataField]
    public string Description { get; set; } = string.Empty;

    [DataField]
    public string Category { get; set; } = "general";
}
