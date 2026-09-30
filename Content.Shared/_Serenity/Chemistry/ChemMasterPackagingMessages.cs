using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Chemistry;

/// <summary>Move a reagent between the beaker and the packaging buffer (or delete it from the buffer while discarding).</summary>
[Serializable, NetSerializable]
public sealed class ChemMasterPackagingTransferMessage(ReagentId reagent, FixedPoint2 amount, bool fromBeaker) : BoundUserInterfaceMessage
{
    public readonly ReagentId Reagent = reagent;
    public readonly FixedPoint2 Amount = amount;
    public readonly bool FromBeaker = fromBeaker;
}

[Serializable, NetSerializable]
public sealed class ChemMasterPackagingToggleDiscardMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class ChemMasterPackagingSelectMessage(int index, EntProtoId packaging) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly EntProtoId Packaging = packaging;
}

[Serializable, NetSerializable]
public sealed class ChemMasterPackagingUseContainerMessage(int index, bool use) : BoundUserInterfaceMessage
{
    public readonly int Index = index;
    public readonly bool Use = use;
}

[Serializable, NetSerializable]
public sealed class ChemMasterPackagingSetAmountMessage(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class ChemMasterPackagingPrintMessage(string name) : BoundUserInterfaceMessage
{
    public readonly string Name = name;
}
