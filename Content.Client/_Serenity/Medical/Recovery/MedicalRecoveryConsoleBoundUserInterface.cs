using Content.Shared._Serenity.Medical.Recovery;
using Robust.Client.UserInterface;

namespace Content.Client._Serenity.Medical.Recovery;

public sealed class MedicalRecoveryConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MedicalRecoveryConsoleWindow? _window;

    public MedicalRecoveryConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MedicalRecoveryConsoleWindow>();
        _window.ScanPressed += () => SendMessage(new MedicalRecoveryConsoleScanMessage());
        _window.RecoverPressed += target => SendMessage(new MedicalRecoveryConsoleRecoverMessage(target));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MedicalRecoveryConsoleState recoveryState)
            return;

        _window?.UpdateState(recoveryState);
    }
}
