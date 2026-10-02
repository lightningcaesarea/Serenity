using Content.Shared._Serenity.Medical.IV;

namespace Content.Client._Serenity.Medical.IV;

/// <summary>
/// SharedIVSystem is abstract and carries the stand's drag, drop and examine handlers. Without a concrete client system
/// nothing on the client answers CanDragEvent, so the stand can't be click-dragged onto a patient.
/// </summary>
public sealed partial class IVSystem : SharedIVSystem;
