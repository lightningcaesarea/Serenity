using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Consent;

public sealed class MsgUpdateConsentToggles : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public Dictionary<string, bool> ConsentToggles = default!;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream();
        buffer.ReadAlignedMemory(stream, length);
        serializer.DeserializeDirect(stream, out ConsentToggles);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        using var stream = new MemoryStream();
        serializer.SerializeDirect(stream, ConsentToggles);
        buffer.WriteVariableInt32((int)stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
