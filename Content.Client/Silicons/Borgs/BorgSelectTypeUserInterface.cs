// Starlight
using Content.Shared._Serenity.Silicons.Borgs; // Starlight
using Content.Shared.Silicons.Borgs.Components; // Starlight
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Silicons.Borgs;

/// <summary>
/// User interface used by borgs to select their type.
/// </summary>
/// <seealso cref="BorgSelectTypeMenu"/>
/// <seealso cref="BorgSwitchableTypeComponent"/>
/// <seealso cref="BorgSwitchableTypeUiKey"/>
[UsedImplicitly]
public sealed class BorgSelectTypeUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BorgSelectTypeMenu? _menu;

    public BorgSelectTypeUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<BorgSelectTypeMenu>();
        _menu.ConfirmBorgSubtype += subtypePrototype => SendMessage(new BorgSelectSubtypeMessage(subtypePrototype?.ID)); // Starlight - borg subtypes - Starlight
        _menu.ConfirmedBorgType += prototype => SendMessage(new BorgSelectTypeMessage(prototype)); // Starlight
        _menu.SetupMenu(Owner); // Starlight-edit
    }
}
