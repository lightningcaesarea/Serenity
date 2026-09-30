using System.Diagnostics.CodeAnalysis;
using Content.Server.Chemistry.Components;
using Content.Shared._Serenity.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Chemistry.EntitySystems
{
    /// <summary>
    /// Serenity: data-driven packaging (pills, patches, bottles...) fed from a packaging buffer that is
    /// separate from the ChemMaster's main buffer.
    /// </summary>
    public sealed partial class ChemMasterSystem
    {
        [Dependency] private ChemMasterPackagingSystem _packaging = default!;
        [Dependency] private IPrototypeManager _prototype = default!;

        private void InitializePackaging()
        {
            SubscribeLocalEvent<ChemMasterPackagingComponent, ChemMasterPackagingTransferMessage>(OnPackagingTransfer);
            SubscribeLocalEvent<ChemMasterPackagingComponent, ChemMasterPackagingToggleDiscardMessage>(OnPackagingToggleDiscard);
            SubscribeLocalEvent<ChemMasterPackagingComponent, ChemMasterPackagingSelectMessage>(OnPackagingSelect);
            SubscribeLocalEvent<ChemMasterPackagingComponent, ChemMasterPackagingUseContainerMessage>(OnPackagingUseContainer);
            SubscribeLocalEvent<ChemMasterPackagingComponent, ChemMasterPackagingSetAmountMessage>(OnPackagingSetAmount);
            SubscribeLocalEvent<ChemMasterPackagingComponent, ChemMasterPackagingPrintMessage>(OnPackagingPrint);
        }

        private bool TryGetPackagingBuffer(
            Entity<ChemMasterPackagingComponent> ent,
            [NotNullWhen(true)] out Entity<SolutionComponent>? soln,
            [NotNullWhen(true)] out Solution? solution)
        {
            return _solutionContainerSystem.TryGetSolution(ent.Owner, ent.Comp.BufferSolution, out soln, out solution);
        }

        private void OnPackagingTransfer(Entity<ChemMasterPackagingComponent> ent, ref ChemMasterPackagingTransferMessage msg)
        {
            if (!TryComp(ent, out ChemMasterComponent? chemMaster) ||
                msg.Amount <= FixedPoint2.Zero ||
                msg.Amount != FixedPoint2.MaxValue && Array.IndexOf(ent.Comp.TransferAmounts, msg.Amount) < 0 ||
                !TryGetPackagingBuffer(ent, out var bufferSoln, out var buffer))
            {
                return;
            }

            var beakerEnt = _itemSlotsSystem.GetItemOrNull(ent, SharedChemMaster.InputSlotName);
            Entity<SolutionComponent>? beakerSoln = null;
            Solution? beaker = null;
            var hasBeaker = beakerEnt != null &&
                            _solutionContainerSystem.TryGetFitsInDispenser(beakerEnt.Value, out beakerSoln, out beaker);

            if (msg.FromBeaker)
            {
                if (!hasBeaker)
                    return;

                var amount = FixedPoint2.Min(msg.Amount, beaker!.GetReagentQuantity(msg.Reagent), buffer.AvailableVolume);
                if (amount <= FixedPoint2.Zero)
                    return;

                amount = _solutionContainerSystem.RemoveReagent(beakerSoln!.Value, msg.Reagent, amount);
                _solutionContainerSystem.TryAddReagent(bufferSoln.Value, msg.Reagent, amount, out _);
            }
            else if (ent.Comp.Discarding)
            {
                _solutionContainerSystem.RemoveReagent(bufferSoln.Value, msg.Reagent, FixedPoint2.Min(msg.Amount, buffer.GetReagentQuantity(msg.Reagent)));
            }
            else
            {
                if (!hasBeaker)
                    return;

                var amount = FixedPoint2.Min(msg.Amount, buffer.GetReagentQuantity(msg.Reagent), beaker!.AvailableVolume);
                if (amount <= FixedPoint2.Zero)
                    return;

                amount = _solutionContainerSystem.RemoveReagent(bufferSoln.Value, msg.Reagent, amount);
                _solutionContainerSystem.TryAddReagent(beakerSoln!.Value, msg.Reagent, amount, out _);
            }

            UpdateUiState((ent.Owner, chemMaster));
            ClickSound((ent.Owner, chemMaster));
        }

        private void OnPackagingToggleDiscard(Entity<ChemMasterPackagingComponent> ent, ref ChemMasterPackagingToggleDiscardMessage msg)
        {
            ent.Comp.Discarding = !ent.Comp.Discarding;
            PackagingChanged(ent);
        }

        private void OnPackagingSelect(Entity<ChemMasterPackagingComponent> ent, ref ChemMasterPackagingSelectMessage msg)
        {
            if (!ent.Comp.Sections.TryGetValue(msg.Index, out var section) ||
                !section.Packaging.Contains(msg.Packaging))
            {
                return;
            }

            ent.Comp.SelectedPackaging = (msg.Index, msg.Packaging);
            PackagingChanged(ent);
        }

        private void OnPackagingUseContainer(Entity<ChemMasterPackagingComponent> ent, ref ChemMasterPackagingUseContainerMessage msg)
        {
            if (!ent.Comp.Sections.TryGetValue(msg.Index, out var section))
                return;

            ent.Comp.Sections[msg.Index] = section with { ContainerPressed = msg.Use };
            PackagingChanged(ent);
        }

        private void OnPackagingSetAmount(Entity<ChemMasterPackagingComponent> ent, ref ChemMasterPackagingSetAmountMessage msg)
        {
            ent.Comp.PackagingAmount = Math.Clamp(msg.Amount, 1, ent.Comp.PackagingMaxAmount);
            PackagingChanged(ent);
        }

        private void PackagingChanged(Entity<ChemMasterPackagingComponent> ent)
        {
            Dirty(ent);
            if (TryComp(ent, out ChemMasterComponent? chemMaster))
            {
                UpdateUiState((ent.Owner, chemMaster));
                ClickSound((ent.Owner, chemMaster));
            }
        }

        private void OnPackagingPrint(Entity<ChemMasterPackagingComponent> ent, ref ChemMasterPackagingPrintMessage msg)
        {
            var user = msg.Actor;
            if (!TryComp(ent, out ChemMasterComponent? chemMaster) ||
                ent.Comp.SelectedPackaging is not { Index: var index, Packaging: var packagingId } ||
                !ent.Comp.Sections.TryGetValue(index, out var section) ||
                !section.Packaging.Contains(packagingId) ||
                !_packaging.TryGetPackagingMaxVolume(packagingId, out var maxVolume) ||
                !TryGetPackagingBuffer(ent, out var bufferSoln, out var buffer))
            {
                return;
            }

            if (buffer.Volume <= FixedPoint2.Zero)
            {
                _popupSystem.PopupCursor(Loc.GetString("chem-master-packaging-buffer-empty"), user);
                return;
            }

            // Split the whole buffer evenly across the requested number of packages, never past a package's capacity.
            // Anything that does not fit stays in the buffer.
            var count = Math.Clamp(ent.Comp.PackagingAmount, 1, ent.Comp.PackagingMaxAmount);
            var perPackage = FixedPoint2.Min(buffer.Volume / FixedPoint2.New(count), maxVolume.Value);
            if (perPackage <= FixedPoint2.Zero)
            {
                count = 1;
                perPackage = FixedPoint2.Min(buffer.Volume, maxVolume.Value);
            }

            string? label = null;
            var typedName = msg.Name.Trim();
            if (typedName.Length > 0)
            {
                label = FormattedMessage.EscapeText(typedName[..Math.Min(typedName.Length, ent.Comp.NameMaxLength)]);
            }
            else if (ent.Comp.AutoName && buffer.Contents.Count > 0)
            {
                var biggest = buffer.Contents[0];
                foreach (var reagent in buffer.Contents)
                {
                    if (reagent.Quantity > biggest.Quantity)
                        biggest = reagent;
                }

                var name = _prototype.TryIndex(biggest.Reagent.Prototype, out ReagentPrototype? reagentProto)
                    ? reagentProto.LocalizedName
                    : biggest.Reagent.Prototype.Id;
                label = $"{name} ({perPackage.Int()}u)";
            }

            // Prefer a storage item sitting in the optional output slot, then the section's optional
            // container, then the floor.
            var coords = Transform(ent).Coordinates;
            EntityUid? storageTarget = null;
            var slotItem = _itemSlotsSystem.GetItemOrNull(ent, ent.Comp.OutputSlot);
            if (slotItem != null && HasComp<StorageComponent>(slotItem))
            {
                storageTarget = slotItem;
            }
            else if (section is { ContainerPressed: true, Container: { } containerId })
            {
                storageTarget = Spawn(containerId, coords);
            }

            if (storageTarget != null && label != null)
                _labelSystem.Label(storageTarget.Value, label);

            for (var i = 0; i < count && buffer.Volume > FixedPoint2.Zero; i++)
            {
                // The last package also takes whatever the even split left over, up to its capacity.
                var amount = i == count - 1
                    ? FixedPoint2.Min(buffer.Volume, maxVolume.Value)
                    : FixedPoint2.Min(perPackage, buffer.Volume);

                var package = Spawn(packagingId, coords);
                if (!TryGetFirstSolution(package, out var packageSoln))
                {
                    Del(package);
                    continue;
                }

                _solutionContainerSystem.RemoveAllSolution(packageSoln.Value);
                var split = _solutionContainerSystem.SplitSolution(bufferSoln.Value, amount);
                if (!_solutionContainerSystem.TryAddSolution(packageSoln.Value, split))
                    _solutionContainerSystem.ForceAddSolution(bufferSoln.Value, split);

                if (label != null)
                    _labelSystem.Label(package, label);

                if (storageTarget != null)
                    _storageSystem.Insert(storageTarget.Value, package, out _, user: user);

                _adminLogger.Add(LogType.Action, LogImpact.Low,
                    $"{ToPrettyString(user):user} printed {ToPrettyString(package):package} {SharedSolutionContainerSystem.ToPrettyString(packageSoln.Value.Comp.Solution)}");
            }

            UpdateUiState((ent.Owner, chemMaster));
            ClickSound((ent.Owner, chemMaster));
        }

        private bool TryGetFirstSolution(EntityUid uid, [NotNullWhen(true)] out Entity<SolutionComponent>? solution)
        {
            foreach (var (_, soln) in _solutionContainerSystem.EnumerateSolutions(uid))
            {
                solution = soln;
                return true;
            }

            solution = null;
            return false;
        }
    }
}
