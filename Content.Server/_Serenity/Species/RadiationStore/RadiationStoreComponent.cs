using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Serenity.Species.RadiationStore;

/// <summary>
/// Lets a living creature soak up radiation damage into an internal store. While the store has room the
/// creature passively heals radiation; once it fills, healing stops and a purge action unlocks that ejects
/// the store as an item and empties it again.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(RadiationStoreSystem))]
public sealed partial class RadiationStoreComponent : Component
{
    /// <summary>
    /// Radiation absorbed since the last purge.
    /// </summary>
    [DataField]
    public FixedPoint2 Stored;

    /// <summary>
    /// How much radiation the store holds before healing stops and the purge action unlocks.
    /// </summary>
    [DataField]
    public FixedPoint2 Capacity = 30;

    /// <summary>
    /// Damage applied every <see cref="Interval"/> while the store has room. Should be negative (healing).
    /// </summary>
    [DataField]
    public DamageSpecifier Healing = new()
    {
        DamageDict = { ["Radiation"] = -0.5 },
    };

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextHeal;

    /// <summary>
    /// Action granted on map init, kept disabled until the store is full.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId PurgeAction;

    [DataField]
    public EntityUid? PurgeActionEntity;

    /// <summary>
    /// Item spawned when the store is purged.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId PurgeItem;

    [DataField]
    public SoundSpecifier? PurgeSound;

    [DataField]
    public LocId FullPopup = "radiation-store-full";

    [DataField]
    public LocId PurgePopup = "radiation-store-purge";
}
