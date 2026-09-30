using System.Diagnostics.CodeAnalysis;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Chemistry;

/// <summary>
/// Shared helpers for <see cref="ChemMasterPackagingComponent"/>. Packaging sizes are read from the
/// packaging prototype's own solution so they are never duplicated in the machine's YAML.
/// </summary>
public sealed partial class ChemMasterPackagingSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    /// <summary>Capacity of the first solution a packaging prototype spawns with.</summary>
    public bool TryGetPackagingMaxVolume(EntProtoId packaging, [NotNullWhen(true)] out FixedPoint2? maxVolume)
    {
        maxVolume = null;
        if (!_prototype.TryIndex(packaging, out var proto))
            return false;

        // Solutions declared directly on the entity (pills, patches)...
        if (proto.TryGetComponent<SolutionComponent>(out var own, _compFactory))
        {
            maxVolume = own.Solution.MaxVolume;
            return true;
        }

        // ...or spawned through a solution manager (bottles).
        foreach (var (_, solution) in _solution.EnumerateSolutions(proto))
        {
            maxVolume = solution.MaxVolume;
            return true;
        }

        return false;
    }
}
