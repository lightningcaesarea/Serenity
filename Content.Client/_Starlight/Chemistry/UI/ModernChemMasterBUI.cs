using Content.Shared._Serenity.Chemistry;
using Content.Shared.Chemistry;
using Content.Shared.Containers.ItemSlots;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Starlight.Chemistry.UI;

/// <summary>
///     Created by Cookie for multiple servers.
///     Initializes a <see cref="ModernChemMasterWindow"/> and updates it when new server messages are received.
/// </summary>
[UsedImplicitly]
public sealed class ModernChemMasterBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ModernChemMasterWindow? _window;

    /// <summary>
    ///     Called each time a chem master UI instance is opened.
    ///     Generates the window and fills it with relevant info. Sets the actions for static buttons.
    /// </summary>
    protected override void Open()
    {
        base.Open();

        // Set-up the window layout/elements
        _window = this.CreateWindow<ModernChemMasterWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.SetChemMasterEntity(EntMan.GetNetEntity(Owner));

        // Set-up the static button actions.
        _window.InputEjectButton.OnPressed += _ => SendMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));
        _window.InputEjectButtonClassic.OnPressed += _ => SendMessage(
            new ItemSlotButtonPressedEvent(SharedChemMaster.InputSlotName));
        _window.BufferTransferButton.OnPressed += _ => SendMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Transfer));
        _window.BufferTransferButtonClassic.OnPressed += _ => SendMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Transfer));
        _window.BufferDiscardButton.OnPressed += _ => SendMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Discard));
        _window.BufferDiscardButtonClassic.OnPressed += _ => SendMessage(
            new ChemMasterSetModeMessage(ChemMasterMode.Discard));
        _window.BufferSortButton.OnPressed += _ => SendMessage(
            new ChemMasterSortingTypeCycleMessage());
        _window.BufferSortButtonClassic.OnPressed += _ => SendMessage(
            new ChemMasterSortingTypeCycleMessage());
        // Serenity: packaging tab (one panel per layout, both wired identically).
        foreach (var panel in new[] { _window.PackagingPanel, _window.PackagingPanelClassic })
        {
            panel.OnTransfer += (reagent, amount, fromBeaker) =>
                SendMessage(new ChemMasterPackagingTransferMessage(reagent, amount, fromBeaker));
            panel.OnToggleDiscard += () => SendMessage(new ChemMasterPackagingToggleDiscardMessage());
            panel.OnSelect += (index, packaging) => SendMessage(new ChemMasterPackagingSelectMessage(index, packaging));
            panel.OnUseContainer += (index, use) => SendMessage(new ChemMasterPackagingUseContainerMessage(index, use));
            panel.OnSetAmount += amount => SendMessage(new ChemMasterPackagingSetAmountMessage(amount));
            panel.OnPrint += name => SendMessage(new ChemMasterPackagingPrintMessage(name));
            panel.OnEjectOutput += () => SendMessage(new ItemSlotButtonPressedEvent(SharedChemMaster.OutputSlotName));
        }

        _window.OnReagentButtonPressed += (_, button) => SendMessage(new ChemMasterReagentAmountButtonMessage(button.Id, button.Amount, button.IsBuffer));
        _window.OnCustomReagentButtonPressed += (_, id, amount, isBuffer) => SendMessage(new ChemMasterReagentCustomAmountButtonMessage(id, amount, isBuffer));

        _window.OnAmountSelected += amount => SendMessage(new ChemMasterSetTransferAmountMessage(amount));

        _window.OnToggleValveButtonPressed += () =>
            SendMessage(new ChemMasterToggleValveMessage());
    }

    /// <summary>
    /// Update the ui each time new state data is sent from the server.
    /// </summary>
    /// <param name="state">
    /// Data of the <see cref="SharedReagentDispenser"/> that this ui represents.
    /// Sent from the server.
    /// </param>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var castState = (ChemMasterBoundUserInterfaceState) state;

        _window?.SetSelectedAmount(castState.TransferAmount);
        _window?.UpdateState(castState); // Update window state
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null && EntMan.GetNetEntity(Owner) is {} id)
            ModernChemMasterWindow.ClearCustomForEntity(id);
        base.Dispose(disposing);
    }
}
