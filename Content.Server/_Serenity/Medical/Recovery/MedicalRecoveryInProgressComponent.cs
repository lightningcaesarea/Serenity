using Content.Shared._Serenity.Medical.Recovery;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Serenity.Medical.Recovery;

/// <summary>
/// Added to a carrier while their recovery is running. Driven by <see cref="MedicalRecoverySystem"/>'s
/// update loop: <see cref="MedicalRecoveryPhase.Deploying"/> until <see cref="PhaseEnd"/>, then a
/// vanilla FultonedComponent carries them to <see cref="Pad"/> during
/// <see cref="MedicalRecoveryPhase.InTransit"/>. Removed once they land or the fulton is lost.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class MedicalRecoveryInProgressComponent : Component
{
    [DataField]
    public EntityUid Implant;

    [DataField]
    public EntityUid Pad;

    /// <summary>
    /// The console that started this, if any, so its UI can show progress and be refreshed.
    /// </summary>
    [DataField]
    public EntityUid? Console;

    [DataField]
    public MedicalRecoveryPhase Phase = MedicalRecoveryPhase.Deploying;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan PhaseStart;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan PhaseEnd;
}
