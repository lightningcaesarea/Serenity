using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// Ties a character-editor marking category to the feature tags the intimacy engine checks. A character
/// "has" an anatomy part when they wear at least one visible marking in <see cref="Category"/>; it is
/// "accessible" when nothing is worn in any of <see cref="CoveringSlots"/>.
/// </summary>
/// <remarks>
/// A character with a Penis marking exposes the feature tags <c>Penis</c> and, while uncovered,
/// <c>PenisAccessible</c>. Acts list the tags they need in <c>actorNeeds</c> / <c>targetNeeds</c>.
/// </remarks>
[Prototype]
public sealed partial class IntimacyAnatomyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The marking category that provides this anatomy.
    /// </summary>
    [DataField(required: true)]
    public MarkingCategories Category;

    /// <summary>
    /// Base feature tag. The tag with <c>Accessible</c> appended is added while uncovered.
    /// </summary>
    [DataField(required: true)]
    public string Feature = string.Empty;

    /// <summary>
    /// Inventory slot names (for example <c>jumpsuit</c>) that hide this anatomy while occupied.
    /// </summary>
    [DataField]
    public List<string> CoveringSlots = new();

    /// <summary>
    /// The tag for "present and uncovered".
    /// </summary>
    public string AccessibleFeature => Feature + AccessibleSuffix;

    public const string AccessibleSuffix = "Accessible";
}
