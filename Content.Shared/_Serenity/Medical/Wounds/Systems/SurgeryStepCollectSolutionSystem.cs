using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

/// <summary>
/// Makes a step with <see cref="SurgeryStepCollectSolutionEffectComponent"/> need a container in the surgeon's hands,
/// and pours the step's reagent into it when the step completes. The container is any held item that accepts
/// reagents (a beaker, a bottle, a jug...) and has room left.
/// </summary>
public sealed partial class SurgeryStepCollectSolutionSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryStepCollectSolutionEffectComponent, SurgeryCanPerformStepEvent>(OnCanPerform);
    }

    private void OnCanPerform(Entity<SurgeryStepCollectSolutionEffectComponent> ent, ref SurgeryCanPerformStepEvent args)
    {
        // Another rule already rejected the step, and its reason is the one to show
        if (args.Invalid != Content.Shared._Starlight.Medical.Surgery.StepInvalidReason.None)
            return;

        var holdsContainer = false;
        foreach (var held in args.Tools)
        {
            if (!_solutions.TryGetRefillableSolution(held, out _, out var solution))
                continue;

            holdsContainer = true;
            if (solution.AvailableVolume > FixedPoint2.Zero)
                return;
        }

        args.Invalid = Content.Shared._Starlight.Medical.Surgery.StepInvalidReason.MissingTool;
        args.Popup = holdsContainer
            ? "The container you are holding is full! Empty it or hold another."
            : "You need to hold a container, like a beaker, to drain it into!";
    }

    /// <summary>
    /// Pours the step's reagent into the first held container with room. Returns false if there is none.
    /// </summary>
    public bool TryCollect(Entity<SurgeryStepCollectSolutionEffectComponent> ent, List<EntityUid> tools)
    {
        foreach (var held in tools)
        {
            if (!_solutions.TryGetRefillableSolution(held, out var soln, out var solution)
                || solution.AvailableVolume <= FixedPoint2.Zero)
            {
                continue;
            }

            return _solutions.TryAddReagent(soln.Value, ent.Comp.Reagent, ent.Comp.Quantity, out _);
        }

        return false;
    }
}
