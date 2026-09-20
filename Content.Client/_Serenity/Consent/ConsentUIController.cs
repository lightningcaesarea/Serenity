using Content.Client._Serenity.Kinks;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.MenuBar.Widgets;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Serenity.Consent;

/// <summary>
/// Top-bar button that opens the Kinks &amp; Consent window in-game. The window was previously
/// reachable only from the lobby, which meant a player could not change a consent toggle mid-round
/// without leaving their body. Mirrors Starlight's <c>AchievementUIController</c> wiring.
/// </summary>
[UsedImplicitly]
public sealed partial class ConsentUIController : UIController, IOnStateExited<GameplayState>
{
    [Dependency] private IClientPreferencesManager _preferences = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private MenuButton? ConsentButton => UIManager.GetActiveUIWidgetOrNull<GameTopMenuBar>()?.ConsentButton;

    private KinkListWindow? _window;

    public void OnStateExited(GameplayState state)
    {
        _window?.Close();
        _window = null;
    }

    public void LoadButton()
        => ConsentButton?.OnPressed += OnButtonPressed;

    public void UnloadButton()
        => ConsentButton?.OnPressed -= OnButtonPressed;

    private void OnButtonPressed(BaseButton.ButtonEventArgs obj)
    {
        if (_window is { IsOpen: true })
        {
            _window.Close();
            return;
        }

        // Built fresh on every open: the window snapshots preferences in its constructor, and
        // they may have changed since (lobby edits, server-side validation).
        _window = new KinkListWindow(_preferences, _prototypes);
        _window.OnClose += () =>
        {
            if (ConsentButton != null)
                ConsentButton.Pressed = false;
            _window = null;
        };

        if (ConsentButton != null)
            ConsentButton.Pressed = true;

        _window.OpenCentered();
    }
}
