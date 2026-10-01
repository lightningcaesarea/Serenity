using Content.Shared._Starlight.Medical.Body.Part;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Where on the body a wound is: a kind of body part and which side it is on. A wound with no location affects the
/// whole body (infection, or a mob without a body).
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class WoundLocation : IEquatable<WoundLocation>
{
    [DataField(required: true)]
    public BodyPartType Type;

    [DataField]
    public BodyPartSymmetry Symmetry;

    public WoundLocation()
    {
    }

    public WoundLocation(BodyPartType type, BodyPartSymmetry symmetry)
    {
        Type = type;
        Symmetry = symmetry;
    }

    /// <summary>
    /// Locale key for the location's name, e.g. <c>wound-location-left-arm</c> or <c>wound-location-head</c>.
    /// </summary>
    public string LocKey => Symmetry == BodyPartSymmetry.None
        ? $"wound-location-{Type.ToString().ToLowerInvariant()}"
        : $"wound-location-{Symmetry.ToString().ToLowerInvariant()}-{Type.ToString().ToLowerInvariant()}";

    public bool Equals(WoundLocation? other)
    {
        return other != null && other.Type == Type && other.Symmetry == Symmetry;
    }

    public override bool Equals(object? obj) => Equals(obj as WoundLocation);

    public override int GetHashCode() => HashCode.Combine(Type, Symmetry);
}
