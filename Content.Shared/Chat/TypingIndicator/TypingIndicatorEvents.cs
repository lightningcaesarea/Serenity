using Robust.Shared.Serialization;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Exceptions;

namespace Content.Shared.Chat.TypingIndicator;

/// <summary>
///     Networked event from client.
///     Send to server when client started/stopped typing in chat input field.
/// </summary>
[Serializable, NetSerializable]
public sealed class TypingChangedEvent : EntityEventArgs
{
    public readonly TypingIndicatorState State;

    /// <summary>
    ///     Serenity: bubble to show instead of the species default while typing in a special channel
    ///     (whisper, emote, OOC, radio). Must be one listed by a <see cref="Content.Shared._Serenity.Chat.TypingIndicator.TypingChannelIndicatorPrototype"/>,
    ///     otherwise the server ignores it.
    /// </summary>
    public readonly ProtoId<TypingIndicatorPrototype>? ChannelIndicator;

    public TypingChangedEvent(TypingIndicatorState state, ProtoId<TypingIndicatorPrototype>? channelIndicator = null)
    {
        State = state;
        ChannelIndicator = channelIndicator;
    }
}

/// <summary>
///     This event will be broadcast right before displaying an entities typing indicator.
///     If _overrideIndicator is not null after the event is finished it will be used.
/// </summary>
[Serializable, NetSerializable]
public sealed class BeforeShowTypingIndicatorEvent : IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;

    private ProtoId<TypingIndicatorPrototype>? _overrideIndicator = null;
    private TimeSpan? _latestEquipTime = null;
    public BeforeShowTypingIndicatorEvent()
    {
        _overrideIndicator = null;
        _latestEquipTime = null;
    }
    /// <summary>
    ///     Will only update the time and indicator if the given time is more recent than
    ///     the stored time or if the stored time is null.
    /// </summary>
    ///  <returns>
    ///     True if the given time is more recent than the stored time, and false otherwise.
    ///  </returns>
    public bool TryUpdateTimeAndIndicator(ProtoId<TypingIndicatorPrototype>? indicator, TimeSpan? equipTime)
    {
        if (equipTime != null && (_latestEquipTime == null || _latestEquipTime < equipTime))
        {
            _latestEquipTime = equipTime;
            _overrideIndicator = indicator;
            return true;
        }
        return false;
    }
    public ProtoId<TypingIndicatorPrototype>? GetMostRecentIndicator()
    {
        return _overrideIndicator;
    }
}
