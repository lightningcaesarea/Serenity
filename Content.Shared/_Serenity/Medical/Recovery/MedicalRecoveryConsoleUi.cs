using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Medical.Recovery;

[Serializable, NetSerializable]
public enum MedicalRecoveryConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum MedicalRecoveryPhase : byte
{
    /// <summary>
    /// The beacon is being deployed onto the body. Shown as a progress bar.
    /// </summary>
    Deploying,

    /// <summary>
    /// The fulton balloon is up and the body is on its way to the pad. Shown as a countdown.
    /// </summary>
    InTransit,
}

/// <summary>
/// A carrier of an unused recovery implant that the console can currently see.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecoveryTargetEntry(NetEntity entity, string name, bool dead, float distance)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly bool Dead = dead;
    public readonly float Distance = distance;
}

/// <summary>
/// A recovery this console started that hasn't landed yet. The client derives the progress bar
/// and countdown from <see cref="PhaseStart"/>/<see cref="PhaseEnd"/> against its own clock.
/// </summary>
[Serializable, NetSerializable]
public sealed class MedicalRecoveryActiveEntry(NetEntity entity, string name, MedicalRecoveryPhase phase, TimeSpan phaseStart, TimeSpan phaseEnd)
{
    public readonly NetEntity Entity = entity;
    public readonly string Name = name;
    public readonly MedicalRecoveryPhase Phase = phase;
    public readonly TimeSpan PhaseStart = phaseStart;
    public readonly TimeSpan PhaseEnd = phaseEnd;
}

[Serializable, NetSerializable]
public sealed class MedicalRecoveryConsoleState(
    List<MedicalRecoveryTargetEntry> targets,
    List<MedicalRecoveryActiveEntry> active,
    bool padLinked,
    float range) : BoundUserInterfaceState
{
    public readonly List<MedicalRecoveryTargetEntry> Targets = targets;
    public readonly List<MedicalRecoveryActiveEntry> Active = active;
    public readonly bool PadLinked = padLinked;
    public readonly float Range = range;
}

[Serializable, NetSerializable]
public sealed class MedicalRecoveryConsoleRecoverMessage(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class MedicalRecoveryConsoleScanMessage : BoundUserInterfaceMessage;
