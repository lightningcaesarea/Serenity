using Content.Shared.Chemistry.Events;

namespace Content.Shared._Serenity.Species.InjectionProof;

public sealed partial class InjectionProofSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InjectionProofComponent, TargetBeforeInjectEvent>(OnBeforeInject);
    }

    private void OnBeforeInject(EntityUid uid, InjectionProofComponent comp, TargetBeforeInjectEvent args)
    {
        args.Cancel();
    }
}
