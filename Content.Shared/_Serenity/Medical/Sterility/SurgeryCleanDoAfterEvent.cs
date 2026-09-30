using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Medical.Sterility;

/// <summary>
/// One wipe of a dirty surgical item with soap.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SurgeryCleanDoAfterEvent : SimpleDoAfterEvent;
