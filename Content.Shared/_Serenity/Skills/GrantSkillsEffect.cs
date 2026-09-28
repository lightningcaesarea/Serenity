using Content.Shared._Starlight.Traits.Effects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Skills;

/// <summary>
/// Trait effect that teaches skills. Prerequisites aren't checked, so only use it for skills that have none
/// or list the prerequisites too.
/// </summary>
public sealed partial class GrantSkillsEffect : BaseTraitEffect
{
    [DataField(required: true)]
    public List<ProtoId<SkillPrototype>> Skills = new();

    public override void Apply(TraitEffectContext ctx)
    {
        var skills = ctx.EntMan.System<SharedSkillSystem>();
        foreach (var skill in Skills)
        {
            skills.GrantSkill(ctx.Player, skill);
        }
    }
}
