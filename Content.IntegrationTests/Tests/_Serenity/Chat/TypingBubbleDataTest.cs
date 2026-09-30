using System.Collections.Generic;
using Content.Shared._Serenity.Chat.TypingIndicator;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Robust.Client.ResourceManagement;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Chat;

/// <summary>
/// Catches mistakes in the typing bubble data: a channel mapped twice (only the first would ever apply)
/// and bubbles whose sprite or states don't exist, which would render as a missing texture in game.
/// </summary>
[TestFixture]
public sealed class TypingBubbleDataTest
{
    [Test]
    public async Task ChannelMappingsAreUniqueAndBubblesHaveSprites()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var proto = client.ProtoMan;
        var resources = client.ResolveDependency<IResourceCache>();

        await client.WaitAssertion(() =>
        {
            var indicators = new HashSet<ProtoId<TypingIndicatorPrototype>>();
            var channels = new Dictionary<ChatSelectChannel, string>();

            foreach (var mapping in proto.EnumeratePrototypes<TypingChannelIndicatorPrototype>())
            {
                Assert.That(channels.TryAdd(mapping.Channel, mapping.ID), Is.True,
                    $"{mapping.ID} maps {mapping.Channel}, which {channels.GetValueOrDefault(mapping.Channel)} already maps");
                indicators.Add(mapping.Indicator);
            }

            // The felinid bubble isn't channel-mapped but ships with them, so check it too.
            indicators.Add("felinid");

            Assert.Multiple(() =>
            {
                foreach (var id in indicators)
                {
                    var indicator = proto.Index(id);
                    if (!resources.TryGetResource<RSIResource>(indicator.SpritePath, out var rsi))
                    {
                        Assert.Fail($"{id}: missing sprite {indicator.SpritePath}");
                        continue;
                    }

                    Assert.That(rsi.RSI.TryGetState(indicator.TypingState, out _), $"{id}: missing typing state {indicator.TypingState}");
                    Assert.That(rsi.RSI.TryGetState(indicator.IdleState, out _), $"{id}: missing idle state {indicator.IdleState}");
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
