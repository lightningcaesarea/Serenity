using Content.Server.Chemistry.Components;
using Content.Server.Popups;
using Content.Server.Storage.EntitySystems;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Administration.Logs;
using Content.Shared._Serenity.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Storage;
using JetBrains.Annotations;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Content.Server.Chemistry.EntitySystems
{

    /// <summary>
    /// Contains all the server-side logic for ChemMasters.
    /// <seealso cref="ChemMasterComponent"/>
    /// </summary>
    [UsedImplicitly]
    public sealed partial class ChemMasterSystem : EntitySystem
    {
        [Dependency] private PopupSystem _popupSystem = default!;
        [Dependency] private AudioSystem _audioSystem = default!;
        [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;
        [Dependency] private ItemSlotsSystem _itemSlotsSystem = default!;
        [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!;
        [Dependency] private StorageSystem _storageSystem = default!;
        [Dependency] private LabelSystem _labelSystem = default!;
        [Dependency] private ISharedAdminLogManager _adminLogger = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<ChemMasterComponent, ComponentStartup>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ChemMasterComponent, SolutionChangedEvent>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ChemMasterComponent, EntInsertedIntoContainerMessage>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ChemMasterComponent, EntRemovedFromContainerMessage>(SubscribeUpdateUiState);
            // Subscribing to DragDropTargetEvent is a quick fix to ensure the UI updates when fluids are dragged and dropped into the ChemMaster, since Shared.Fluids.EntitySystems.SolutionDumpingSystem.cs bypasses UpdateChemicals().
            // TODO: Remove when proper support for infinite volume solutions is added.
            SubscribeLocalEvent<ChemMasterComponent, DragDropTargetEvent>(SubscribeUpdateUiState);
            SubscribeLocalEvent<ChemMasterComponent, BoundUIOpenedEvent>(SubscribeUpdateUiState);

            SubscribeLocalEvent<ChemMasterComponent, ChemMasterSetModeMessage>(OnSetModeMessage);
            SubscribeLocalEvent<ChemMasterComponent, ChemMasterSortingTypeCycleMessage>(OnCycleSortingTypeMessage);
            SubscribeLocalEvent<ChemMasterComponent, ChemMasterReagentAmountButtonMessage>(OnReagentButtonMessage);
            SubscribeLocalEvent<ChemMasterComponent, ChemMasterReagentCustomAmountButtonMessage>(OnCustomReagentButtonMessage); // Starlight
            SubscribeLocalEvent<ChemMasterComponent, ChemMasterToggleValveMessage>(OnToggleValveMessage); // Starlight-edit: Plumbing valve
            SubscribeLocalEvent<ChemMasterComponent, ChemMasterSetTransferAmountMessage>(OnSetTransferAmountMessage); // TRIESTE
            SubscribeLocalEvent<ChemMasterComponent, DragDropTargetEvent>(SubscribeUpdateUiState); // TRIESTE

            InitializePackaging(); // Serenity
        }

        private void SubscribeUpdateUiState<T>(Entity<ChemMasterComponent> ent, ref T ev)
        {
            UpdateUiState(ent);
        }

        private void UpdateUiState(Entity<ChemMasterComponent> ent)
        {
            var (owner, chemMaster) = ent;
            if (!_solutionContainerSystem.TryGetSolution(owner, SharedChemMaster.BufferSolutionName, out _, out var bufferSolution))
                return;
            var inputContainer = _itemSlotsSystem.GetItemOrNull(owner, SharedChemMaster.InputSlotName);
            var outputContainer = _itemSlotsSystem.GetItemOrNull(owner, SharedChemMaster.OutputSlotName);

            var bufferReagents = bufferSolution.Contents;
            var bufferCurrentVolume = bufferSolution.Volume;

            // Serenity: the packaging buffer is a separate solution, so the packaging tab never touches the main buffer.
            IReadOnlyList<ReagentQuantity> packagingReagents = [];
            var packagingVolume = FixedPoint2.Zero;
            var packagingMax = FixedPoint2.Zero;
            if (TryComp<ChemMasterPackagingComponent>(owner, out var packaging) &&
                _solutionContainerSystem.TryGetSolution(owner, packaging.BufferSolution, out _, out var packagingSolution))
            {
                packagingReagents = packagingSolution.Contents;
                packagingVolume = packagingSolution.Volume;
                packagingMax = packagingSolution.MaxVolume;
            }

            var valveOpen = TryComp<PlumbingOutletComponent>(owner, out var plumbingOutlet) && plumbingOutlet.Enabled; // Starlight-edit: Plumbing valve
            var state = new ChemMasterBoundUserInterfaceState(
                chemMaster.Mode, chemMaster.SortingType, BuildInputContainerInfo(inputContainer), outputContainer is { } output ? Name(output) : null,
                bufferReagents, bufferCurrentVolume, valveOpen, chemMaster.TransferAmount,
                packagingReagents, packagingVolume, packagingMax); // Starlight-edit - valveOpen; Serenity - packaging buffer

            _userInterfaceSystem.SetUiState(owner, ChemMasterUiKey.Key, state);
        }

        private void OnSetModeMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterSetModeMessage message)
        {
            // Ensure the mode is valid, either Transfer or Discard.
            if (!Enum.IsDefined(typeof(ChemMasterMode), message.ChemMasterMode))
                return;

            chemMaster.Comp.Mode = message.ChemMasterMode;
            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }

        private void OnCycleSortingTypeMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterSortingTypeCycleMessage message)
        {
            chemMaster.Comp.SortingType++;
            if (chemMaster.Comp.SortingType > ChemMasterSortingType.Latest)
                chemMaster.Comp.SortingType = ChemMasterSortingType.None;
            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }

        private void OnReagentButtonMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterReagentAmountButtonMessage message)
        {
            // Ensure the amount corresponds to one of the reagent amount buttons.
            if (!Enum.IsDefined(typeof(ChemMasterReagentAmount), message.Amount))
                return;

            switch (chemMaster.Comp.Mode)
            {
                case ChemMasterMode.Transfer:
                    TransferReagents(chemMaster, message.ReagentId, message.Amount.GetFixedPoint(), message.FromBuffer);
                    break;
                case ChemMasterMode.Discard:
                    DiscardReagents(chemMaster, message.ReagentId, message.Amount.GetFixedPoint(), message.FromBuffer);
                    break;
                default:
                    // Invalid mode.
                    return;
            }

            ClickSound(chemMaster);
        }

        private void TransferReagents(Entity<ChemMasterComponent> chemMaster, ReagentId id, FixedPoint2 amount, bool fromBuffer)
        {
            var container = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.InputSlotName);
            if (container is null ||
                !_solutionContainerSystem.TryGetFitsInDispenser(container.Value, out var containerSoln, out var containerSolution) ||
                !_solutionContainerSystem.TryGetSolution(chemMaster.Owner, SharedChemMaster.BufferSolutionName, out var bufferSoln, out var bufferSolution)) //Starlight-edit
            {
                return;
            }

            if (fromBuffer) // Buffer to container
            {
                amount = FixedPoint2.Min(amount, containerSolution.AvailableVolume);
                amount = bufferSolution.RemoveReagent(id, amount, preserveOrder: true);
                _solutionContainerSystem.TryAddReagent(containerSoln.Value, id, amount, out var _);
            }
            else // Container to buffer
            {
                // Starlight-Start: No longer force transfer as the buffer container no longer is infinitely big.
                amount = FixedPoint2.Min(amount, containerSolution.GetReagentQuantity(id), bufferSolution.AvailableVolume);
                _solutionContainerSystem.RemoveReagent(containerSoln.Value, id, amount);
                _solutionContainerSystem.TryAddReagent(bufferSoln.Value, id, amount, out _);
                // Starlight-End
            }

            UpdateUiState(chemMaster);
        }

        private void DiscardReagents(Entity<ChemMasterComponent> chemMaster, ReagentId id, FixedPoint2 amount, bool fromBuffer)
        {
            if (fromBuffer)
            {
                if (_solutionContainerSystem.TryGetSolution(chemMaster.Owner, SharedChemMaster.BufferSolutionName, out _, out var bufferSolution))
                    bufferSolution.RemoveReagent(id, amount, preserveOrder: true);
                else
                    return;
            }
            else
            {
                var container = _itemSlotsSystem.GetItemOrNull(chemMaster, SharedChemMaster.InputSlotName);
                if (container is not null &&
                    _solutionContainerSystem.TryGetFitsInDispenser(container.Value, out var containerSolution, out _))
                {
                    _solutionContainerSystem.RemoveReagent(containerSolution.Value, id, amount);
                }
                else
                    return;
            }

            UpdateUiState(chemMaster);
        }

        private void ClickSound(Entity<ChemMasterComponent> chemMaster)
        {
            _audioSystem.PlayPvs(chemMaster.Comp.ClickSound, chemMaster, AudioParams.Default.WithVolume(-2f));
        }

        private ContainerInfo? BuildInputContainerInfo(EntityUid? container)
        {
            if (container is not { Valid: true })
                return null;

            if (!TryComp(container, out FitsInDispenserComponent? fits)
                || !_solutionContainerSystem.TryGetSolution(container.Value, fits.Solution, out _, out var solution))
            {
                return null;
            }

            return BuildContainerInfo(Name(container.Value), solution);
        }

        private static ContainerInfo BuildContainerInfo(string name, Solution solution)
        {
            return new ContainerInfo(name, solution.Volume, solution.MaxVolume)
            {
                Reagents = solution.Contents
            };
        }
    }
}
