using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

/// <summary>
/// Weapon hits: melee attacks, thrown items and projectiles each tell the target what is about to hit it just before
/// the damage lands, so the damage handler knows the hit came from a weapon (and gets a location even when it causes
/// no wound) and can use the weapon's <see cref="HitLocationBiasComponent"/>. Hitscan has no such event; its damage
/// carries the gun as its origin, which <see cref="OnDamageChanged"/> falls back to.
/// </summary>
public abstract partial class SharedWoundSystem
{
    private void InitializeHitLocation()
    {
        SubscribeLocalEvent<WoundComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<WoundComponent, ThrowHitByEvent>(OnThrowHitBy);
        SubscribeLocalEvent<WoundComponent, ProjectileReflectAttemptEvent>(OnProjectileHitAttempt);
    }

    private void OnAttacked(Entity<WoundComponent> ent, ref AttackedEvent args)
    {
        // Used is the user itself for an unarmed attack
        SetPendingHit(ent, args.Used);
    }

    private void OnThrowHitBy(Entity<WoundComponent> ent, ref ThrowHitByEvent args)
    {
        SetPendingHit(ent, args.Thrown);
    }

    private void OnProjectileHitAttempt(Entity<WoundComponent> ent, ref ProjectileReflectAttemptEvent args)
    {
        // A bullet's own bias wins; otherwise the gun that fired it
        var source = args.ProjUid;
        if (!HasComp<HitLocationBiasComponent>(source) && args.Component.Weapon is { } gun && HasComp<HitLocationBiasComponent>(gun))
            source = gun;

        SetPendingHit(ent, source);
    }

    private void SetPendingHit(Entity<WoundComponent> ent, EntityUid source)
    {
        // Locations are only picked on the server
        if (!_net.IsServer)
            return;

        ent.Comp.PendingHitSource = source;
        ent.Comp.PendingHitTick = _timing.CurTick;
    }

    /// <summary>
    /// The weapon whose hit is landing right now, if any. Clears it, so one hit is only counted once.
    /// </summary>
    private EntityUid? TakePendingHit(WoundComponent comp)
    {
        var source = comp.PendingHitTick == _timing.CurTick ? comp.PendingHitSource : null;
        comp.PendingHitSource = null;
        return source;
    }
}

/// <summary>
/// Raised on a mob when a weapon hit lands in a body part, after the hit's wounds (if any) are applied. Server only.
/// </summary>
/// <param name="Location">Where the hit landed.</param>
/// <param name="Source">The weapon, projectile or thrown item, or the attacker for an unarmed hit.</param>
/// <param name="Origin">Who dealt the damage, if known.</param>
[ByRefEvent]
public readonly record struct BodyPartHitEvent(WoundLocation Location, EntityUid Source, EntityUid? Origin);
