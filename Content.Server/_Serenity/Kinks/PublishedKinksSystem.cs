using System.Linq;
using Content.Server.Preferences.Managers;
using Content.Shared._Serenity.Consent;
using Content.Shared._Serenity.Kinks;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Serenity.Kinks;

/// <summary>
/// Keeps <see cref="PublishedKinksComponent"/> on player mobs in step with the player's saved
/// kink preferences and their "Publish my kink list" consent toggle. Same shape as
/// <c>ConsentSystem</c>: rebuild on spawn/attach, strip on detach, and react to preference edits.
/// </summary>
public sealed partial class PublishedKinksSystem : EntitySystem
{
    [Dependency] private IServerPreferencesManager _prefs = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ISharedPlayerManager _players = default!;

    /// <summary>The consent toggle that gates publication. Fails closed if the prototype is ever removed.</summary>
    private const string PublishToggle = "ShowKinkList";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnDetached);

        _prefs.ConsentTogglesChanged += OnPreferencesChanged;
        _prefs.KinkPreferencesChanged += OnPreferencesChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _prefs.ConsentTogglesChanged -= OnPreferencesChanged;
        _prefs.KinkPreferencesChanged -= OnPreferencesChanged;
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev) => Rebuild(ev.Mob, ev.Player.UserId);

    private void OnAttached(PlayerAttachedEvent ev) => Rebuild(ev.Entity, ev.Player.UserId);

    private void OnDetached(PlayerDetachedEvent ev)
    {
        // A body nobody is driving publishes nothing.
        RemComp<PublishedKinksComponent>(ev.Entity);
    }

    private void OnPreferencesChanged(NetUserId userId)
    {
        if (!_players.TryGetSessionById(userId, out var session) || session.AttachedEntity is not { } mob)
            return;

        Rebuild(mob, userId);
    }

    private void Rebuild(EntityUid mob, NetUserId userId)
    {
        if (TerminatingOrDeleted(mob))
            return;

        var prefs = _prefs.GetPreferencesOrNull(userId);

        if (prefs == null || !ConsentCheck.HasConsent(prefs.ConsentToggles, PublishToggle, _proto))
        {
            RemComp<PublishedKinksComponent>(mob);
            return;
        }

        // Only publish ids that still resolve to a prototype, so a removed kink never leaks as a raw id.
        var published = new Dictionary<string, KinkPreferenceLevel>();
        foreach (var (id, level) in prefs.KinkPreferences)
        {
            if (_proto.HasIndex<KinkPrototype>(id))
                published[id] = level;
        }

        var comp = EnsureComp<PublishedKinksComponent>(mob);
        if (comp.Kinks.Count == published.Count && !comp.Kinks.Except(published).Any())
            return;

        comp.Kinks = published;
        Dirty(mob, comp);
    }
}
