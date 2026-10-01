using Content.Server.Popups;
using Content.Shared._Serenity.Species.RadiationStore;
using Content.Shared.Actions;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Serenity.Species.RadiationStore;

public sealed partial class RadiationStoreSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadiationStoreComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RadiationStoreComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<RadiationStoreComponent, PurgeRadiationStoreActionEvent>(OnPurge);
    }

    private void OnMapInit(Entity<RadiationStoreComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextHeal = _timing.CurTime + ent.Comp.Interval;
        _actions.AddAction(ent, ref ent.Comp.PurgeActionEntity, ent.Comp.PurgeAction);
        _actions.SetEnabled(ent.Comp.PurgeActionEntity, IsFull(ent.Comp));
    }

    private void OnShutdown(Entity<RadiationStoreComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.PurgeActionEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RadiationStoreComponent>();
        while (query.MoveNext(out var uid, out var store))
        {
            if (now < store.NextHeal)
                continue;

            store.NextHeal = now + store.Interval;

            if (IsFull(store) || _mobState.IsDead(uid))
                continue;

            // Resistances are ignored so the store counts exactly what was taken out, not a scaled amount.
            if (!_damageable.TryChangeDamage(uid, store.Healing, out var healed, ignoreResistances: true, interruptsDoAfters: false))
                continue;

            store.Stored -= healed.GetTotal();

            if (!IsFull(store))
                continue;

            _actions.SetEnabled(store.PurgeActionEntity, true);
            _popup.PopupEntity(Loc.GetString(store.FullPopup), uid, uid, PopupType.MediumCaution);
        }
    }

    private void OnPurge(Entity<RadiationStoreComponent> ent, ref PurgeRadiationStoreActionEvent args)
    {
        if (args.Handled || !IsFull(ent.Comp))
            return;

        args.Handled = true;

        var item = Spawn(ent.Comp.PurgeItem, Transform(ent).Coordinates);
        _hands.TryPickupAnyHand(ent, item);

        _audio.PlayPvs(ent.Comp.PurgeSound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.PurgePopup, ("user", ent.Owner)), ent, PopupType.Medium);

        ent.Comp.Stored = 0;
        _actions.SetEnabled(ent.Comp.PurgeActionEntity, false);
    }

    private static bool IsFull(RadiationStoreComponent store)
    {
        return store.Stored >= store.Capacity;
    }
}
