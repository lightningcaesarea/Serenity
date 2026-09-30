using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Chemistry;

/// <summary>
/// Data-driven packaging for the ChemMaster. Owns a packaging buffer that is independent of the
/// machine's main (plumbing) buffer, and a list of packaging sections (pills, patches, bottles...)
/// defined entirely in YAML.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ChemMasterPackagingComponent : Component
{
    /// <summary>Solution id of the packaging buffer. Separate from the main <c>buffer</c>.</summary>
    [DataField, AutoNetworkedField]
    public string BufferSolution = "packagingBuffer";

    [DataField(required: true), AutoNetworkedField]
    public List<PackagingSection> Sections = new();

    [DataField, AutoNetworkedField]
    public (int Index, EntProtoId Packaging)? SelectedPackaging;

    /// <summary>Amounts offered on each reagent row. <see cref="FixedPoint2.MaxValue"/> (the "All" button) is always allowed.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2[] TransferAmounts = { 1, 5, 10, 25, 50 };

    /// <summary>When true, reagents clicked in the packaging buffer are deleted instead of moved back to the beaker.</summary>
    [DataField, AutoNetworkedField]
    public bool Discarding;

    /// <summary>How many packages the next print splits the buffer across.</summary>
    [DataField, AutoNetworkedField]
    public int PackagingAmount = 1;

    [DataField, AutoNetworkedField]
    public int PackagingMaxAmount = 10;

    [DataField, AutoNetworkedField]
    public int NameMaxLength = 42;

    /// <summary>Name packages after their largest reagent when the player leaves the name blank.</summary>
    [DataField, AutoNetworkedField]
    public bool AutoName = true;

    /// <summary>Output slot (of the ChemMaster's ItemSlots) whose storage, if any, receives packages before they fall on the floor.</summary>
    [DataField]
    public string OutputSlot = "outputSlot";

    [DataRecord]
    [Serializable, NetSerializable]
    public readonly partial record struct PackagingSection(
        string Name,
        EntProtoId? Container,
        bool ContainerPressed,
        List<EntProtoId> Packaging
    );
}
