using Robust.Shared.GameObjects;

namespace Content.Shared._Serenity.Medical.Wounds;

/// <summary>
/// Raised on an entity after wound processing from a damage hit.
/// Used by WoundEffectsSystem for item-drop logic without
/// conflicting with SharedWoundSystem's DamageChangedEvent subscription.
/// </summary>
public sealed class WoundsDamagedEvent : EntityEventArgs;

/// <summary>
/// Raised on an entity after wounds are cleared by category (e.g. surgery).
/// Used by WoundEffectsSystem to refresh alerts and movement speed.
/// </summary>
public sealed class WoundsClearedEvent : EntityEventArgs;
