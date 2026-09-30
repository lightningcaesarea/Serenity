using System.Linq;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Serenity.Medical.Sterility;

/// <summary>
/// Surgical dirt on tools and gloves: the API to read and change it, how it looks when examined, and wiping it
/// off with soap. What dirty surgery does to the patient lives in the server half.
/// </summary>
public abstract partial class SharedSterilitySystem : EntitySystem
{
    [Dependency] protected IPrototypeManager _proto = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurgeryDirtinessComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<SurgeryDirtinessComponent, GetVerbsEvent<UtilityVerb>>(OnGetVerbs);
        SubscribeLocalEvent<SurgeryCleansDirtComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SurgeryCleansDirtComponent, SurgeryCleanDoAfterEvent>(OnCleaned);
    }

    protected SterilityConfigPrototype Config => _proto.Index<SterilityConfigPrototype>(SterilityConfigPrototype.DefaultId);

    #region API

    /// <summary>
    /// How dirty an item is, 0 if it has never been used in surgery.
    /// </summary>
    public float GetDirtiness(EntityUid uid)
    {
        return TryComp<SurgeryDirtinessComponent>(uid, out var comp) ? comp.Dirtiness : 0f;
    }

    /// <summary>
    /// The DNA of other patients on an item, or an empty set.
    /// </summary>
    public IReadOnlySet<string> GetDnas(EntityUid uid)
    {
        return TryComp<SurgeryDirtinessComponent>(uid, out var comp) ? comp.Dnas : new HashSet<string>();
    }

    public bool RequiresCleaning(EntityUid uid)
    {
        return TryComp<SurgeryDirtinessComponent>(uid, out var comp) && (comp.Dirtiness > 0f || comp.Dnas.Count > 0);
    }

    public void AddDirt(EntityUid uid, float amount)
    {
        var comp = EnsureComp<SurgeryDirtinessComponent>(uid);
        comp.Dirtiness = Math.Clamp(comp.Dirtiness + amount, 0f, Config.MaxDirtiness);
        Dirty(uid, comp);
    }

    public void AddDna(EntityUid uid, string? dna)
    {
        if (dna == null)
            return;

        var comp = EnsureComp<SurgeryDirtinessComponent>(uid);
        if (comp.Dnas.Add(dna))
            Dirty(uid, comp);
    }

    /// <summary>
    /// Removes up to <paramref name="dirt"/> dirtiness and <paramref name="dnaCount"/> patients' DNA from an item.
    /// </summary>
    public void Clean(EntityUid uid, float dirt, int dnaCount)
    {
        if (!TryComp<SurgeryDirtinessComponent>(uid, out var comp))
            return;

        comp.Dirtiness = Math.Max(0f, comp.Dirtiness - dirt);
        foreach (var dna in comp.Dnas.Take(dnaCount).ToList())
        {
            comp.Dnas.Remove(dna);
        }

        Dirty(uid, comp);
    }

    #endregion

    private void OnExamined(Entity<SurgeryDirtinessComponent> ent, ref ExaminedEvent args)
    {
        // 0 (sterile) to 5 (filthy)
        var stage = (int) Math.Ceiling(ent.Comp.Dirtiness / Config.MaxDirtiness * 5f);
        args.PushMarkup(Loc.GetString($"surgery-cleanliness-{Math.Clamp(stage, 0, 5)}"));

        if (ent.Comp.Dnas.Count > 0)
            args.PushMarkup(Loc.GetString("surgery-contaminated"));
    }

    #region Cleaning

    private void OnGetVerbs(Entity<SurgeryDirtinessComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !RequiresCleaning(ent))
            return;

        var cleaner = _hands.EnumerateHeld(args.User).FirstOrDefault(held => HasComp<SurgeryCleansDirtComponent>(held));
        if (cleaner == default || !TryComp<SurgeryCleansDirtComponent>(cleaner, out var cleans))
            return;

        var user = args.User;
        var target = ent.Owner;
        args.Verbs.Add(new UtilityVerb
        {
            Act = () => TryStartCleaning((cleaner, cleans), user, target),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
            Text = Loc.GetString("surgery-clean-verb"),
            Message = Loc.GetString("surgery-clean-verb-message"),
            DoContactInteraction = true,
        });
    }

    private void OnAfterInteract(Entity<SurgeryCleansDirtComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !RequiresCleaning(target))
            return;

        args.Handled = TryStartCleaning(ent, args.User, target);
    }

    private bool TryStartCleaning(Entity<SurgeryCleansDirtComponent> cleaner, EntityUid user, EntityUid target)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, cleaner.Comp.CleanDelay, new SurgeryCleanDoAfterEvent(), cleaner, target: target, used: cleaner)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            DistanceThreshold = 1f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupClient(Loc.GetString("surgery-cleaning", ("target", target)), user, user);
        return true;
    }

    private void OnCleaned(Entity<SurgeryCleansDirtComponent> ent, ref SurgeryCleanDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not { } target)
            return;

        Clean(target, ent.Comp.DirtAmount, ent.Comp.DnaAmount);

        // Keep wiping until it's clean
        args.Repeat = RequiresCleaning(target);
        args.Handled = true;
    }

    #endregion
}
