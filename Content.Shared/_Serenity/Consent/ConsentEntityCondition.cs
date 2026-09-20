using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Consent;

/// <summary>
/// YAML-usable condition: passes when the target mob's player allows the given consent toggle.
/// Mobs without a consent snapshot never pass, so reagent and effect authors can gate adult
/// outcomes without special-casing NPCs.
/// </summary>
public sealed partial class ConsentEntityConditionSystem : EntityConditionSystem<PlayerConsentComponent, ConsentCondition>
{
    protected override void Condition(Entity<PlayerConsentComponent> entity, ref EntityConditionEvent<ConsentCondition> args)
    {
        args.Result = entity.Comp.Allowed.Contains(args.Condition.Toggle);
    }
}

/// <inheritdoc cref="EntityCondition"/>
public sealed partial class ConsentCondition : EntityConditionBase<ConsentCondition>
{
    [DataField(required: true)]
    public ProtoId<ConsentTogglePrototype> Toggle;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype)
    {
        var name = prototype.TryIndex(Toggle, out var proto) ? proto.Name : Toggle.Id;
        return Loc.GetString("entity-condition-guidebook-consent", ("toggle", name));
    }
}
