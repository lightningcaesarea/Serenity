// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Client.UserInterface.Fragments;
using Content.Shared._Serenity.BountyContracts;
using Content.Shared.CartridgeLoader;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Serenity.BountyContracts.UI;

[UsedImplicitly]
public sealed partial class BountyContractUi : UIFragment
{
    private BountyContractUiFragment? _fragment;
    private BoundUserInterface? _userInterface;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new BountyContractUiFragment();
        _userInterface = userInterface;
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (_fragment == null)
            return;

        switch (state)
        {
            case BountyContractListUiState listState:
                ShowListState(listState);
                break;
            case BountyContractCreateUiState createState:
                ShowCreateState(createState);
                break;
        }
    }

    private void ShowCreateState(BountyContractCreateUiState state)
    {
        _fragment?.RemoveAllChildren();

        var create = new BountyContractUiFragmentCreate();
        create.OnCancelPressed += () => Send(new BountyContractCloseCreateUiMsg());
        create.OnCreatePressed += contract => Send(new BountyContractTryCreateMsg(contract));
        create.SetPossibleTargets(state.Targets);
        create.SetVessels(state.Vessels);

        _fragment?.AddChild(create);
    }

    private void ShowListState(BountyContractListUiState state)
    {
        _fragment?.RemoveAllChildren();

        var list = new BountyContractUiFragmentList();
        list.OnCreateButtonPressed += () => Send(new BountyContractOpenCreateUiMsg());
        list.OnRefreshButtonPressed += () => Send(new BountyContractRefreshListUiMsg());
        list.OnRemoveButtonPressed += contract => Send(new BountyContractTryRemoveUiMsg(contract.ContractId));
        list.SetContracts(state.Contracts, state.IsAllowedRemoveBounties);
        list.SetCanCreate(state.IsAllowedCreateBounties);

        _fragment?.AddChild(list);
    }

    private void Send(CartridgeMessageEvent message)
    {
        _userInterface?.SendMessage(new CartridgeUiMessage(message));
    }
}
