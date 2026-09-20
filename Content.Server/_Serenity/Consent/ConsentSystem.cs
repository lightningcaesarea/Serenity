using Content.Server.Preferences.Managers;
using Content.Shared._Serenity.Consent;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Serenity.Consent;

/// <summary>
/// Keeps <see cref="PlayerConsentComponent"/> on every player-controlled mob in step with the
/// player's saved preferences.
/// </summary>
public sealed partial class ConsentSystem : SharedConsentSystem
{
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnDetached);

        _prefs.ConsentTogglesChanged += OnTogglesChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _prefs.ConsentTogglesChanged -= OnTogglesChanged;
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        Rebuild(ev.Mob, ev.Player.UserId);
    }

    private void OnAttached(PlayerAttachedEvent ev)
    {
        Rebuild(ev.Entity, ev.Player.UserId);
    }

    private void OnDetached(PlayerDetachedEvent ev)
    {
        // A body nobody is driving carries no consent.
        if (RemComp<PlayerConsentComponent>(ev.Entity))
        {
            var changed = new ConsentChangedEvent(ev.Entity);
            RaiseLocalEvent(ev.Entity, ref changed);
        }
    }

    private void OnTogglesChanged(NetUserId userId)
    {
        if (!_players.TryGetSessionById(userId, out var session) || session.AttachedEntity is not { } mob)
            return;

        Rebuild(mob, userId);
    }

    /// <summary>
    /// Recomputes the allowed set for a mob from the owning player's preferences and prototype defaults.
    /// </summary>
    private void Rebuild(EntityUid mob, NetUserId userId)
    {
        if (TerminatingOrDeleted(mob))
            return;

        var saved = _prefs.GetPreferencesOrNull(userId)?.ConsentToggles;
        var allowed = new HashSet<string>();

        foreach (var toggle in _proto.EnumeratePrototypes<ConsentTogglePrototype>())
        {
            if (ConsentCheck.HasConsent(saved, toggle.ID, _proto))
                allowed.Add(toggle.ID);
        }

        var comp = EnsureComp<PlayerConsentComponent>(mob);
        if (comp.Allowed.SetEquals(allowed))
            return;

        comp.Allowed = allowed;
        Dirty(mob, comp);

        var changed = new ConsentChangedEvent(mob);
        RaiseLocalEvent(mob, ref changed);
    }
}
