using Content.Client.Chat.TypingIndicator;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chat.TypingIndicator;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Chat;

[TestFixture]
[TestOf(typeof(SharedTypingIndicatorSystem))]
public sealed class TypingBubbleTest : InteractionTest
{
    protected override string PlayerPrototype => "MobFelionoid";

    private static readonly ProtoId<TypingIndicatorPrototype> Whisper = "whisper";
    private static readonly ProtoId<TypingIndicatorPrototype> NotAChannelBubble = "CosmicTyping";

    /// <summary>
    /// A client typing in a special channel gets that channel's bubble on the server, a bubble no channel
    /// mapping lists is rejected, and the bubble clears when typing stops.
    /// </summary>
    [Test]
    public async Task ChannelBubbleIsSyncedValidatedAndCleared()
    {
        var typing = CEntMan.System<TypingIndicatorSystem>();

        await Client.WaitPost(() => typing.ClientChangedChatFocus(true));
        await Client.WaitPost(() => typing.ClientChangedChatText(Whisper));
        await RunTicks(5);

        var comp = SEntMan.GetComponent<TypingIndicatorComponent>(SPlayer);
        Assert.That(comp.TypingIndicatorPrototype, Is.EqualTo((ProtoId<TypingIndicatorPrototype>) "felinid"), "Felionoids should use the felinid bubble");
        Assert.That(comp.ChannelIndicator, Is.EqualTo((ProtoId<TypingIndicatorPrototype>?) Whisper));

        await Client.WaitPost(() => typing.ClientChangedChatText(NotAChannelBubble));
        await RunTicks(5);
        Assert.That(comp.ChannelIndicator, Is.Null, "a client must not be able to pick an arbitrary bubble");

        await Client.WaitPost(() => typing.ClientChangedChatText(Whisper));
        await RunTicks(5);
        Assert.That(comp.ChannelIndicator, Is.EqualTo((ProtoId<TypingIndicatorPrototype>?) Whisper));

        await Client.WaitPost(() => typing.ClientChangedChatFocus(false));
        await RunTicks(5);
        Assert.That(comp.ChannelIndicator, Is.Null, "the bubble should clear when typing stops");
    }
}
