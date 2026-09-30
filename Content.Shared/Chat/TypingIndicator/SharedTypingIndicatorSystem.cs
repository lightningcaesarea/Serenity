using Content.Shared.ActionBlocker;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Chat.TypingIndicator;

/// <summary>
///     Supports typing indicators on entities.
/// </summary>
public abstract partial class SharedTypingIndicatorSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!; // Serenity

    /// <summary>
    ///     Default ID of <see cref="TypingIndicatorPrototype"/>
    /// </summary>
    public static readonly ProtoId<TypingIndicatorPrototype> InitialIndicatorId = "default";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<TypingIndicatorComponent, PlayerDetachedEvent>(OnPlayerDetached);

        SubscribeLocalEvent<TypingIndicatorClothingComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<TypingIndicatorClothingComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<TypingIndicatorClothingComponent, InventoryRelayedEvent<BeforeShowTypingIndicatorEvent>>(BeforeShow);

        SubscribeAllEvent<TypingChangedEvent>(OnTypingChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded); // Serenity
        CacheChannelIndicators(); // Serenity
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        // when player poses entity we want to make sure that there is typing indicator
        EnsureComp<TypingIndicatorComponent>(ev.Entity);
        // we also need appearance component to sync visual state
        EnsureComp<AppearanceComponent>(ev.Entity);
    }

    private void OnPlayerDetached(EntityUid uid, TypingIndicatorComponent component, PlayerDetachedEvent args)
    {
        // player left entity body - hide typing indicator
        SetTypingIndicatorState(uid, TypingIndicatorState.None);
        SetChannelIndicator(uid, TypingIndicatorState.None, null); // Serenity
    }

    private void OnGotEquipped(Entity<TypingIndicatorClothingComponent> entity, ref ClothingGotEquippedEvent args)
    {
        entity.Comp.GotEquippedTime = _timing.CurTime;
    }

    private void OnGotUnequipped(Entity<TypingIndicatorClothingComponent> entity, ref ClothingGotUnequippedEvent args)
    {
        entity.Comp.GotEquippedTime = null;
    }

    private void BeforeShow(Entity<TypingIndicatorClothingComponent> entity, ref InventoryRelayedEvent<BeforeShowTypingIndicatorEvent> args)
    {
        args.Args.TryUpdateTimeAndIndicator(entity.Comp.TypingIndicatorPrototype, entity.Comp.GotEquippedTime);
    }

    private void OnTypingChanged(TypingChangedEvent ev, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;
        if (!Exists(uid))
        {
            Log.Warning($"Client {args.SenderSession} sent TypingChangedEvent without an attached entity.");
            return;
        }

        // check if this entity can speak or emote
        if (!_actionBlocker.CanEmote(uid.Value) && !_actionBlocker.CanSpeak(uid.Value))
        {
            // nah, make sure that typing indicator is disabled
            SetTypingIndicatorState(uid.Value, TypingIndicatorState.None);
            return;
        }

        SetTypingIndicatorState(uid.Value, ev.State);
        SetChannelIndicator(uid.Value, ev.State, ev.ChannelIndicator); // Serenity
    }

    // Serenity: per-channel typing bubbles

    private readonly HashSet<ProtoId<TypingIndicatorPrototype>> _channelIndicators = new();

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<Content.Shared._Serenity.Chat.TypingIndicator.TypingChannelIndicatorPrototype>())
            CacheChannelIndicators();
    }

    private void CacheChannelIndicators()
    {
        _channelIndicators.Clear();
        foreach (var proto in _proto.EnumeratePrototypes<Content.Shared._Serenity.Chat.TypingIndicator.TypingChannelIndicatorPrototype>())
        {
            _channelIndicators.Add(proto.Indicator);
        }
    }

    /// <summary>
    ///     Stores the channel bubble the client asked for, but only if a channel mapping lists it, so a client
    ///     can't pick an arbitrary indicator (e.g. an antagonist one) for itself. Cleared when typing stops.
    /// </summary>
    private void SetChannelIndicator(EntityUid uid, TypingIndicatorState state, ProtoId<TypingIndicatorPrototype>? requested)
    {
        if (!TryComp<TypingIndicatorComponent>(uid, out var comp))
            return;

        var indicator = state != TypingIndicatorState.None && requested is { } id && _channelIndicators.Contains(id)
            ? requested
            : null;

        if (comp.ChannelIndicator == indicator)
            return;

        comp.ChannelIndicator = indicator;
        Dirty(uid, comp);
    }

    private void SetTypingIndicatorState(EntityUid uid, TypingIndicatorState state, AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref appearance, false))
            return;

        _appearance.SetData(uid, TypingIndicatorVisuals.State, state, appearance);
    }
}
