using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry
{
    // Plumbing valve toggle
    [Serializable, NetSerializable]
    public sealed class ChemMasterToggleValveMessage : BoundUserInterfaceMessage
    {
    }

    // Custom unit selection handling
    [Serializable, NetSerializable]
    public sealed class ChemMasterReagentCustomAmountButtonMessage : BoundUserInterfaceMessage
    {
        public readonly ReagentId ReagentId;
        public readonly FixedPoint2 Amount;
        public readonly bool FromBuffer;

        public ChemMasterReagentCustomAmountButtonMessage(ReagentId reagentId, FixedPoint2 amount, bool fromBuffer)
        {
            ReagentId = reagentId;
            Amount = amount;
            FromBuffer = fromBuffer;
        }
    }

    /// <summary>
    /// Information about the capacity and contents of a container for display in the UI
    /// </summary>
    public sealed partial class ContainerInfo
    {

        /// <summary>
        /// A list of the patch entities and their sizes within the container
        /// STARLIGHT: Added specifically for patches
        /// </summary>
        public List<(string Id, FixedPoint2 Quantity)>? PatchEntities { get; init; }
    }

    public sealed partial class ChemMasterBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly bool ValveOpen;
    }

    /// TRIESTE SPECIFIC
    [Serializable, NetSerializable]
    public sealed class ChemMasterSetTransferAmountMessage : BoundUserInterfaceMessage
    {
        public ChemMasterReagentAmount Amount;

        public ChemMasterSetTransferAmountMessage(ChemMasterReagentAmount amount)
        {
            Amount = amount;
        }
    }
}
