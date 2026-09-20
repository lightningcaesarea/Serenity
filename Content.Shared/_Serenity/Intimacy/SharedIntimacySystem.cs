using System.Diagnostics.CodeAnalysis;
using Content.Shared._Serenity.Consent;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Serenity.Intimacy;

/// <summary>
/// Stat access and act validation shared by server and client. All consent checks for the
/// intimacy loop funnel through here.
/// </summary>
public abstract partial class SharedIntimacySystem : EntitySystem
{
    /// <summary>Consent toggle that switches the whole intimacy loop on for a player.</summary>
    public const string MasterToggle = "LewdInteractions";

    /// <summary>Consent toggle for hearing intimacy sounds.</summary>
    public const string SoundsToggle = "LewdSounds";

    /// <summary>Consent toggle for climaxing automatically at peak pleasure.</summary>
    public const string AutoClimaxToggle = "AutoClimax";

    [Dependency] protected IPrototypeManager Proto = default!;
    [Dependency] protected SharedConsentSystem Consent = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>
    /// Whether this mob takes part in intimacy at all. When false every stat reads as zero and
    /// nothing can modify it, so downstream systems cannot leak effects onto a non-consenting player.
    /// </summary>
    public bool IsEnabled(EntityUid mob)
    {
        return HasComp<IntimacyParticipantComponent>(mob) && Consent.Allows(mob, MasterToggle);
    }

    public float GetStat(Entity<IntimacyParticipantComponent?> mob, string stat)
    {
        if (!Resolve(mob, ref mob.Comp, false) || !IsEnabled(mob))
            return 0f;

        return mob.Comp.Stats.GetValueOrDefault(stat);
    }

    /// <summary>
    /// Adds <paramref name="delta"/> to a stat, clamped to the stat's range. Fails silently when
    /// the mob is not an enabled participant or the stat is unknown.
    /// </summary>
    public bool TryAdjustStat(Entity<IntimacyParticipantComponent?> mob, string stat, float delta)
    {
        return TrySetStat(mob, stat, GetStat(mob, stat) + delta);
    }

    public bool TrySetStat(Entity<IntimacyParticipantComponent?> mob, string stat, float value)
    {
        if (!Resolve(mob, ref mob.Comp, false) || !IsEnabled(mob))
            return false;

        if (!Proto.TryIndex<IntimacyStatPrototype>(stat, out var proto))
            return false;

        var old = mob.Comp.Stats.GetValueOrDefault(stat);
        var clamped = Math.Clamp(value, 0f, proto.Max);
        if (MathHelper.CloseTo(old, clamped))
            return true;

        mob.Comp.Stats[stat] = clamped;
        Dirty(mob);

        var ev = new IntimacyStatChangedEvent(mob, stat, old, clamped);
        RaiseLocalEvent(mob, ref ev);
        return true;
    }

    /// <summary>
    /// Full eligibility check for one act. <paramref name="reason"/> is a loc id describing the
    /// first failure, for tooltips and popups.
    /// </summary>
    public bool CanPerform(EntityUid actor, EntityUid target, IntimacyActPrototype act, [NotNullWhen(false)] out string? reason)
    {
        reason = null;

        if (!TryComp<IntimacyParticipantComponent>(actor, out var actorComp) || !IsEnabled(actor))
        {
            reason = "intimacy-fail-actor-disabled";
            return false;
        }

        if (!TryComp<IntimacyParticipantComponent>(target, out var targetComp) || !IsEnabled(target))
        {
            reason = "intimacy-fail-target-disabled";
            return false;
        }

        if (_mobState.IsIncapacitated(actor))
        {
            reason = "intimacy-fail-incapacitated";
            return false;
        }

        var self = actor == target;
        if (self && !act.AllowSelf && !act.SelfOnly)
        {
            reason = "intimacy-fail-not-self";
            return false;
        }

        if (!self && act.SelfOnly)
        {
            reason = "intimacy-fail-self-only";
            return false;
        }

        if (!Proto.TryIndex(act.Category, out var category))
        {
            reason = "intimacy-fail-unknown";
            return false;
        }

        if (!Consent.Mutual(actor, target, category.Consent) || !Consent.Mutual(actor, target, act.Consent))
        {
            reason = "intimacy-fail-consent";
            return false;
        }

        if (!actorComp.Features.IsSupersetOf(act.ActorNeeds) || !targetComp.Features.IsSupersetOf(act.TargetNeeds))
        {
            reason = "intimacy-fail-anatomy";
            return false;
        }

        if (!self && !_interaction.InRangeUnobstructed(actor, target, act.Range))
        {
            reason = "intimacy-fail-range";
            return false;
        }

        if (actorComp.ReadyAt.TryGetValue(act.ID, out var readyAt) && Timing.CurTime < readyAt)
        {
            reason = "intimacy-fail-cooldown";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Every act the actor could perform on the target right now.
    /// </summary>
    public IEnumerable<IntimacyActPrototype> AvailableActs(EntityUid actor, EntityUid target)
    {
        foreach (var act in Proto.EnumeratePrototypes<IntimacyActPrototype>())
        {
            if (CanPerform(actor, target, act, out _))
                yield return act;
        }
    }
}
