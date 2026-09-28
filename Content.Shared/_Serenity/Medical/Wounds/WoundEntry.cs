using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Medical.Wounds;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class WoundEntry
{
    [DataField]
    public ProtoId<WoundTypePrototype> WoundTypeId;

    [DataField]
    public int Tier;

    /// <summary>
    /// Game time at which this wound's next natural-regen tier drop is due.
    /// Set by <see cref="Systems.SharedWoundSystem"/> on application or tier
    /// upgrade and decremented by the server-side regen tick. Replicated so
    /// the client never lags the actual tier value.
    /// </summary>
    [DataField]
    public TimeSpan NextDecayTime;

    public WoundEntry(ProtoId<WoundTypePrototype> woundTypeId, int tier)
    {
        WoundTypeId = woundTypeId;
        Tier = tier;
    }
}
