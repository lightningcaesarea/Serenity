using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Kinks;

[Serializable]
public enum KinkPreferenceLevel : byte
{
    Favorite = 0,
    Yes = 1,
    Maybe = 2,
    No = 3,
}

public sealed class MsgUpdateKinkPreferences : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public Dictionary<string, KinkPreferenceLevel> KinkPreferences = default!;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream();
        buffer.ReadAlignedMemory(stream, length);
        serializer.DeserializeDirect(stream, out KinkPreferences);
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        using var stream = new MemoryStream();
        serializer.SerializeDirect(stream, KinkPreferences);
        buffer.WriteVariableInt32((int)stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
