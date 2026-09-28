using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Spawners.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpawnPointComponent : Component, ISpawnPoint
{
    /// <summary>
    /// The job this spawn point is valid for.
    /// Null will allow all jobs to spawn here.
    /// </summary>
    [DataField("job_id")]
    public ProtoId<JobPrototype>? Job;

    // Serenity start
    /// <summary>
    /// Further jobs that may also spawn here, so a new job can share an existing job's spawn points
    /// without placing new markers on every map.
    /// </summary>
    [DataField]
    public List<ProtoId<JobPrototype>> AdditionalJobs = new();

    /// <summary>
    /// Whether <paramref name="job"/> may spawn here. A null job, or a spawn point with no job, matches anything.
    /// </summary>
    public bool AllowsJob(ProtoId<JobPrototype>? job)
        => job == null || Job == null || Job == job || AdditionalJobs.Contains(job.Value);
    // Serenity end

    /// <summary>
    /// The type of spawn point.
    /// </summary>
    [DataField("spawn_type"), ViewVariables(VVAccess.ReadWrite)]
    public SpawnPointType SpawnType { get; set; } = SpawnPointType.Unset;

    public override string ToString()
    {
        return $"{Job} {SpawnType}";
    }
}

public enum SpawnPointType
{
    Unset = 0,
    LateJoin,
    Job,
    Observer,
}
