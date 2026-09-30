using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Serenity.Chat.TypingIndicator;

/// <summary>
/// Maps a chat channel to the typing bubble shown above someone typing in it, in place of their species bubble.
/// Only bubbles listed here can be requested by a client.
/// </summary>
[Prototype]
public sealed partial class TypingChannelIndicatorPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The channel this bubble is for.
    /// </summary>
    [DataField(required: true)]
    public ChatSelectChannel Channel;

    [DataField(required: true)]
    public ProtoId<TypingIndicatorPrototype> Indicator;
}
