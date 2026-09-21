// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Shared._Serenity.Contraband;
using Robust.Client.UserInterface;

namespace Content.Client._Serenity.Contraband;

public sealed class ContrabandExchangeBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ContrabandExchangeMenu? _menu;

    public ContrabandExchangeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ContrabandExchangeMenu>();
        _menu.AppraiseRequested += () => SendMessage(new ContrabandExchangeAppraiseMessage());
        _menu.SellRequested += () => SendMessage(new ContrabandExchangeSellMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ContrabandExchangeInterfaceState cState)
            return;

        _menu?.UpdateState(cState);
    }
}
