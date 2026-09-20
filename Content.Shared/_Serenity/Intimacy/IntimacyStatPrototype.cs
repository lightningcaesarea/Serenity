using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// A tracked value on an intimacy participant, such as arousal. Stats are data-driven so that
/// content can add new ones (sensitivity, exhaustion...) without touching C#.
/// </summary>
[Prototype]
public sealed partial class IntimacyStatPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Colour used for this stat's bar in the interaction window.
    /// </summary>
    [DataField]
    public Color Color = Color.White;

    [DataField]
    public float Max = 100f;

    /// <summary>
    /// How much the stat falls per second on its own when nothing is happening.
    /// </summary>
    [DataField]
    public float DecayPerSecond = 0.5f;

    /// <summary>
    /// Display order; lower first.
    /// </summary>
    [DataField]
    public int Order;
}
