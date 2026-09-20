using Content.Shared._Serenity.Intimacy;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Serenity.Intimacy;

[UsedImplicitly]
public sealed class IntimacyBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private IntimacyWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<IntimacyWindow>();
        _window.OnActPressed += act => SendMessage(new IntimacyPerformActMessage(act));
        _window.OnClimaxPressed += () => SendMessage(new IntimacyClimaxMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IntimacyUiState cast)
            _window?.Update(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Close();
    }
}
