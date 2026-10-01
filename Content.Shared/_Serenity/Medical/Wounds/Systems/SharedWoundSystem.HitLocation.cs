using Content.Shared._Starlight.Medical.Body.Part;
using Content.Shared.Projectiles;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Shared._Serenity.Medical.Wounds.Systems;

/// <summary>
/// Weapon hits: melee attacks, thrown items and projectiles each tell the target what is about to hit it just before
/// the damage lands, so the damage handler knows the hit came from a weapon (and gets a location even when it causes
/// no wound) and can use the weapon's <see cref="HitLocationBiasComponent"/>. Hitscan has no such event; its damage
/// carries the gun as its origin, which <see cref="OnDamageChanged"/> falls back to.
/// Melee hits can also be aimed by where the attacker clicked on the target (see <see cref="HitAimConfig"/>).
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
        SetPendingHit(ent, args.Used, AimedAt(ent, args.ClickLocation));
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

    private void SetPendingHit(Entity<WoundComponent> ent, EntityUid source, List<BodyPartType>? aim = null)
    {
        // Locations are only picked on the server
        if (!_net.IsServer)
            return;

        ent.Comp.PendingHitSource = source;
        ent.Comp.PendingHitTick = _timing.CurTick;
        ent.Comp.PendingHitAim = aim;
    }

    /// <summary>
    /// The weapon whose hit is landing right now and what it was aimed at, if any. Clears them, so one hit is only
    /// counted once.
    /// </summary>
    private (EntityUid? Source, List<BodyPartType>? Aim) TakePendingHit(WoundComponent comp)
    {
        var current = comp.PendingHitTick == _timing.CurTick;
        var result = current ? (comp.PendingHitSource, comp.PendingHitAim) : (null, null);
        comp.PendingHitSource = null;
        comp.PendingHitAim = null;
        return result;
    }

    /// <summary>
    /// The body part types a click aims at: the click's height relative to the target, as the attacker sees it.
    /// Mobs are drawn upright and the camera follows the grid, so "up" is up in the target's grid frame. Null if
    /// the mob can't be aimed at, is lying down (its sprite is on its side), or the click wasn't on it.
    /// </summary>
    public List<BodyPartType>? AimedAt(Entity<WoundComponent> target, EntityCoordinates click)
    {
        var aim = _proto.Index(target.Comp.Config).Aim;
        if (aim == null || !click.IsValid(EntityManager) || _standing.IsDown(target.Owner))
            return null;

        var xform = Transform(target);
        var local = _transform.WithEntityId(click, xform.ParentUid);
        if (!local.IsValid(EntityManager))
            return null;

        var offset = local.Position - xform.LocalPosition;
        if (offset.Length() > aim.MaxDistance)
            return null;

        return aim.ZoneAt(offset.Y);
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
