using Content.Server.Chemistry.Components;
using Content.Shared._Starlight.Plumbing.Components;
using Content.Shared.Chemistry;
using Content.Shared.FixedPoint;
using Content.Shared.Storage;

namespace Content.Server.Chemistry.EntitySystems
{
    /// <summary>
    /// Contains all the server-side logic for ChemMasters.
    /// <seealso cref="ChemMasterComponent"/>
    /// </summary>
    public sealed partial class ChemMasterSystem : EntitySystem
    {
        private void OnCustomReagentButtonMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterReagentCustomAmountButtonMessage message)
        {
            if (message.Amount <= FixedPoint2.Zero || message.Amount > FixedPoint2.New(1000))
                return;

            switch (chemMaster.Comp.Mode)
            {
                case ChemMasterMode.Transfer:
                    TransferReagents(chemMaster, message.ReagentId, message.Amount, message.FromBuffer);
                    break;
                case ChemMasterMode.Discard:
                    DiscardReagents(chemMaster, message.ReagentId, message.Amount, message.FromBuffer);
                    break;
                default:
                    return;
            }

            ClickSound(chemMaster);
        }

        private void OnToggleValveMessage(Entity<ChemMasterComponent> chemMaster, ref ChemMasterToggleValveMessage message)
        {
            if (!TryComp<PlumbingOutletComponent>(chemMaster.Owner, out var plumbingOutlet))
                return;

            plumbingOutlet.Enabled = !plumbingOutlet.Enabled;
            Dirty(chemMaster.Owner, plumbingOutlet);
            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }

        // TRIESTE SPECIFIC
        private void OnSetTransferAmountMessage(
            Entity<ChemMasterComponent> chemMaster,
            ref ChemMasterSetTransferAmountMessage message)
        {
            if (!Enum.IsDefined(typeof(ChemMasterReagentAmount), message.Amount))
                return;

            chemMaster.Comp.TransferAmount = message.Amount;
            UpdateUiState(chemMaster);
            ClickSound(chemMaster);
        }
    }
}
