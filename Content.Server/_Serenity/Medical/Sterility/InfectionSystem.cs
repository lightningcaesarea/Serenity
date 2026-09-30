using Content.Shared._Serenity.Medical.Sterility;
using Content.Shared._Serenity.Medical.Wounds;
using Content.Shared._Serenity.Medical.Wounds.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Serenity.Medical.Sterility;

/// <summary>
/// Infection: a wound that never heals by itself. An unsterile operation or an untreated open wound can give a
/// patient one; left alone it gets worse tier by tier and poisons them. A broad-spectrum antibiotic freezes it where
/// it is, suppresses its symptoms and blocks new infections, but doesn't cure it: surgery (draining it) does.
/// All tuning is in <see cref="SterilityConfigPrototype"/>.
/// </summary>
public sealed partial class InfectionSystem : EntitySystem
{
    /// <summary>
    /// Status effect an antibiotic applies.
    /// </summary>
    public static readonly EntProtoId AntibioticEffect = "StatusEffectAntibiotic";

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedWoundSystem _wounds = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundComponent, SurgeryStepDirtiedEvent>(OnStepDirtied);
        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(Config.InfectionTickSeconds);
    }

    private SterilityConfigPrototype Config => _proto.Index<SterilityConfigPrototype>(SterilityConfigPrototype.DefaultId);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;

        var now = _timing.CurTime;
        _nextTick = now + TimeSpan.FromSeconds(Config.InfectionTickSeconds);

        var query = EntityQueryEnumerator<WoundComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.ActiveWounds.Count == 0 || _mobState.IsDead(uid))
                continue;

            Tick(uid, comp, now);
        }
    }

    private void OnStepDirtied(Entity<WoundComponent> ent, ref SurgeryStepDirtiedEvent args)
    {
        if (HasAntibiotic(ent))
            return;

        if (_random.Prob(Config.SurgeryInfectionChance(args.TotalDirtiness)))
            TryInfect(ent, ent.Comp);
    }

    public bool HasAntibiotic(EntityUid uid)
    {
        return _statusEffects.HasStatusEffect(uid, AntibioticEffect);
    }

    /// <summary>
    /// Gives the mob a tier 1 infection, unless it already has one. Returns true if it was added.
    /// </summary>
    public bool TryInfect(EntityUid uid, WoundComponent comp)
    {
        if (GetInfection(comp) != null)
            return false;

        var config = Config;
        _wounds.AddWound(uid, comp, new WoundEntry(config.InfectionWound, 1)
        {
            NextDecayTime = _timing.CurTime + EscalationDelay(config, 1),
        });
        return true;
    }

    /// <summary>
    /// The chance per update that the mob's untreated open wounds give it a new infection. 0 if it is already
    /// infected or on an antibiotic.
    /// </summary>
    public float OpenWoundInfectionChance(EntityUid uid, WoundComponent comp)
    {
        if (GetInfection(comp) != null || HasAntibiotic(uid))
            return 0f;

        var risks = Config.OpenWounds;
        var chance = 0f;
        foreach (var wound in comp.ActiveWounds)
        {
            if (!_proto.TryIndex(wound.WoundTypeId, out var type)
                || !risks.TryGetValue(type.Category, out var risk)
                || wound.Tier < risk.MinTier)
            {
                continue;
            }

            chance += risk.ChancePerTier * wound.Tier;
        }

        return Math.Min(chance, 1f);
    }

    /// <summary>
    /// One infection update: moves an existing infection along, deals its symptoms, and rolls for a new infection
    /// from open wounds. Public so tests can drive it with their own clock.
    /// </summary>
    public void Tick(EntityUid uid, WoundComponent comp, TimeSpan now)
    {
        var config = Config;
        var antibiotic = HasAntibiotic(uid);

        if (GetInfection(comp) is { } infection)
        {
            // A broad-spectrum antibiotic freezes the infection at its current tier: its timer restarts so it can't
            // worsen, and its symptoms are suppressed. It is not cured, so it resumes when the drug wears off.
            if (antibiotic)
            {
                Freeze(infection, now, config);
                return;
            }

            Progress(uid, comp, infection, now, config);
            Symptoms(uid, infection, config);
            return;
        }

        if (_random.Prob(OpenWoundInfectionChance(uid, comp)))
            TryInfect(uid, comp);
    }

    private static void Freeze(WoundEntry infection, TimeSpan now, SterilityConfigPrototype config)
    {
        // Tier 3 never escalates, so it has no timer to hold. Only the server reads this timer, so it isn't networked.
        if (infection.Tier < WoundsConstants.MaxWoundTier)
            infection.NextDecayTime = now + EscalationDelay(config, infection.Tier);
    }

    private void Progress(EntityUid uid, WoundComponent comp, WoundEntry infection, TimeSpan now, SterilityConfigPrototype config)
    {
        if (infection.NextDecayTime > now || infection.Tier >= WoundsConstants.MaxWoundTier)
            return;

        infection.Tier += 1;
        infection.NextDecayTime = infection.Tier >= WoundsConstants.MaxWoundTier
            ? TimeSpan.MaxValue
            : now + EscalationDelay(config, infection.Tier);
        Dirty(uid, comp);
        RaiseLocalEvent(uid, new WoundsDamagedEvent());
    }

    private void Symptoms(EntityUid uid, WoundEntry infection, SterilityConfigPrototype config)
    {
        var index = infection.Tier - WoundsConstants.TierIndexToTierOffset;
        if (index < 0 || index >= config.SymptomDamage.Length || config.SymptomDamage[index] <= 0f)
            return;

        var damage = new DamageSpecifier(_proto.Index(config.SepsisDamageType), FixedPoint2.New(config.SymptomDamage[index]));
        _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false);
    }

    private WoundEntry? GetInfection(WoundComponent comp)
    {
        foreach (var wound in comp.ActiveWounds)
        {
            if (wound.WoundTypeId == Config.InfectionWound)
                return wound;
        }

        return null;
    }

    private static TimeSpan EscalationDelay(SterilityConfigPrototype config, int tier)
    {
        var index = tier - WoundsConstants.TierIndexToTierOffset;
        return TimeSpan.FromSeconds(index >= 0 && index < config.EscalationSeconds.Length ? config.EscalationSeconds[index] : 0f);
    }
}
