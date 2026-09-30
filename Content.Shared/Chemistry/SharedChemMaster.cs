using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.Chemistry
{
    /// <summary>
    /// This class holds constants that are shared between client and server.
    /// </summary>
    public sealed partial class SharedChemMaster
    {
        public const uint PillTypes = 20;
        public const string BufferSolutionName = "buffer";
        public const string InputSlotName = "beakerSlot";
        public const string OutputSlotName = "outputSlot";
        public const string PillSolutionName = "food";
        public const string PatchSolutionName = "patch"; //Starlight-edit
        public const string BottleSolutionName = "drink";
        public const uint LabelMaxLength = 50;
    }

    [Serializable, NetSerializable]
    public sealed class ChemMasterSetModeMessage : BoundUserInterfaceMessage
    {
        public readonly ChemMasterMode ChemMasterMode;

        public ChemMasterSetModeMessage(ChemMasterMode mode)
        {
            ChemMasterMode = mode;
        }
    }

    [Serializable, NetSerializable]
    public sealed class ChemMasterReagentAmountButtonMessage : BoundUserInterfaceMessage
    {
        public readonly ReagentId ReagentId;
        public readonly ChemMasterReagentAmount Amount;
        public readonly bool FromBuffer;

        public ChemMasterReagentAmountButtonMessage(ReagentId reagentId, ChemMasterReagentAmount amount, bool fromBuffer)
        {
            ReagentId = reagentId;
            Amount = amount;
            FromBuffer = fromBuffer;
        }
    }

    public enum ChemMasterMode
    {
        Transfer,
        Discard,
    }

    public enum ChemMasterSortingType : byte
    {
        None = 0,
        Alphabetical = 1,
        Quantity = 2,
        Latest = 3,
    }

    [Serializable, NetSerializable]
    public sealed class ChemMasterSortingTypeCycleMessage : BoundUserInterfaceMessage;


    public enum ChemMasterReagentAmount
    {
        U1 = 1,
        U5 = 5,
        U10 = 10,
        U15 = 15,
        U20 = 20,
        U30 = 30,
        U40 = 40,
        U60 = 60,
        U120 = 120,
        All,
    }

    public static class ChemMasterReagentAmountToFixedPoint
    {
        public static FixedPoint2 GetFixedPoint(this ChemMasterReagentAmount amount)
        {
            if (amount == ChemMasterReagentAmount.All)
                return FixedPoint2.MaxValue;
            else
                return FixedPoint2.New((int)amount);
        }
    }

    /// <summary>
    /// Information about the capacity and contents of a container for display in the UI
    /// </summary>
    [Serializable, NetSerializable]
    public sealed partial class ContainerInfo
    {
        /// <summary>
        /// The container name to show to the player
        /// </summary>
        public readonly string DisplayName;

        /// <summary>
        /// The currently used volume of the container
        /// </summary>
        public readonly FixedPoint2 CurrentVolume;

        /// <summary>
        /// The maximum volume of the container
        /// </summary>
        public readonly FixedPoint2 MaxVolume;

        /// <summary>
        /// A list of the pill entities and their sizes within the container
        /// STARLIGHT: Edited to only be pills and not both pills and patches.
        /// </summary>
        public List<(string Id, FixedPoint2 Quantity)>? PillEntities { get; init; }

        /// <summary>
        /// The label of the container, if one exists at all, to allow us to change the label of the container separate from the pills/patches.
        /// STARLIGHT:  Affects both pills and patches.
        /// </summary>
        public string? ContainerLabel { get; init;  } // Starlight

        public NetEntity? Uid; // Starlight

        public List<ReagentQuantity>? Reagents { get; init; }

        public ContainerInfo(string displayName, FixedPoint2 currentVolume, FixedPoint2 maxVolume)
        {
            DisplayName = displayName;
            CurrentVolume = currentVolume;
            MaxVolume = maxVolume;
        }
    }

    [Serializable, NetSerializable]
    public sealed partial class ChemMasterBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly ContainerInfo? InputContainerInfo;

        /// <summary>Name of whatever is in the optional output slot, if anything.</summary>
        public readonly string? OutputContainerName;

        public readonly ChemMasterReagentAmount TransferAmount;

        /// <summary>
        /// A list of the reagents and their amounts within the buffer, if applicable.
        /// </summary>
        public readonly IReadOnlyList<ReagentQuantity> BufferReagents;

        public readonly ChemMasterMode Mode;

        public readonly ChemMasterSortingType SortingType;

        public readonly FixedPoint2? BufferCurrentVolume;

        /// <summary>Contents of the packaging buffer, which is independent of the main buffer.</summary>
        public readonly IReadOnlyList<ReagentQuantity> PackagingReagents;

        public readonly FixedPoint2 PackagingVolume;

        public readonly FixedPoint2 PackagingMaxVolume;

        public ChemMasterBoundUserInterfaceState(
            ChemMasterMode mode, ChemMasterSortingType sortingType, ContainerInfo? inputContainerInfo, string? outputContainerName,
            IReadOnlyList<ReagentQuantity> bufferReagents, FixedPoint2 bufferCurrentVolume,
            bool valveOpen, ChemMasterReagentAmount transferAmount,
            IReadOnlyList<ReagentQuantity> packagingReagents, FixedPoint2 packagingVolume, FixedPoint2 packagingMaxVolume) // Starlight-edit - valveOpen; Serenity - packaging buffer
        {
            InputContainerInfo = inputContainerInfo;
            OutputContainerName = outputContainerName;
            BufferReagents = bufferReagents;
            Mode = mode;
            SortingType = sortingType;
            BufferCurrentVolume = bufferCurrentVolume;
            ValveOpen = valveOpen; // Starlight-edit
            TransferAmount = transferAmount; // TRIESTE
            PackagingReagents = packagingReagents;
            PackagingVolume = packagingVolume;
            PackagingMaxVolume = packagingMaxVolume;
        }
    }

    [Serializable, NetSerializable]
    public enum ChemMasterUiKey
    {
        Key
    }
}
