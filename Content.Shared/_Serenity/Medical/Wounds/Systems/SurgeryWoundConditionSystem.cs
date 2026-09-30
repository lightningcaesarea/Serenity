using Content.Shared._Starlight.Medical.Surgery.Events;
using Robust.Shared.GameObjects;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

/// <summary>
/// Evaluates <see cref="SurgeryWoundConditionComponent"/> when the surgery UI checks whether a procedure applies.
/// </summary>
public sealed partial class SurgeryWoundConditionSystem : EntitySystem
{
    [Dependency] private SharedWoundSystem _wounds = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryWoundConditionComponent, SurgeryValidEvent>(OnValid);
    }

    private void OnValid(Entity<SurgeryWoundConditionComponent> ent, ref SurgeryValidEvent args)
    {
        if (!TryComp<WoundComponent>(args.Body, out var wounds)
            || _wounds.GetWorstTier(wounds, ent.Comp.Category) < ent.Comp.MinTier)
        {
            args.Cancelled = true;
        }
    }
}
