using Content.Shared._Serenity.Medical.IV;
using Content.Shared._Serenity.Skills;
using Content.Shared._Starlight.Medical.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Serenity.Medical.IV;

public sealed partial class IVSystem : SharedIVSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private SharedChatSystem _chat = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedSkillSystem _skills = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    /// A line needs this skill to be set up or switched between injecting and drawing.
    /// </summary>
    private const string RequiredSkill = "Medicine";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IVLineComponent, IVAttachDoAfterEvent>(OnAttachDoAfter);
        SubscribeLocalEvent<IVLineComponent, GetVerbsEvent<InteractionVerb>>(OnVerbs);

        SubscribeLocalEvent<IVStandComponent, DragDropDraggedEvent>(OnStandDragDrop);
        SubscribeLocalEvent<IVStandComponent, InteractHandEvent>(OnStandInteractHand);
        SubscribeLocalEvent<IVStandComponent, MapInitEvent>(OnStandMapInit);
        SubscribeLocalEvent<IVStandComponent, EntInsertedIntoContainerMessage>(OnStandChanged);
        SubscribeLocalEvent<IVStandComponent, EntRemovedFromContainerMessage>(OnStandChanged);

        SubscribeLocalEvent<IVBagComponent, AfterInteractEvent>(OnBagAfterInteract);
        SubscribeLocalEvent<IVBagComponent, GotUnequippedHandEvent>(OnBagUnequipped);
        SubscribeLocalEvent<IVBagComponent, MapInitEvent>(OnBagMapInit);
        SubscribeLocalEvent<IVBagComponent, SolutionChangedEvent>(OnBagSolutionChanged);
    }

    #region Attaching

    private void OnStandDragDrop(Entity<IVStandComponent> stand, ref DragDropDraggedEvent args)
    {
        // Dropped on yourself the stand folds instead (DeployFoldable).
        if (args.Handled || args.User == args.Target || !TryComp<IVLineComponent>(stand, out var line))
            return;

        args.Handled = true;

        if (line.AttachedTo != null)
            Detach((stand, line), args.User, false);
        else
            TryStartAttach((stand, line), args.User, args.Target);
    }

    private void OnStandInteractHand(Entity<IVStandComponent> stand, ref InteractHandEvent args)
    {
        if (args.Handled || !TryComp<IVLineComponent>(stand, out var line) || line.AttachedTo == null)
            return;

        args.Handled = true;
        Detach((stand, line), args.User, false);
    }

    private void OnBagAfterInteract(Entity<IVBagComponent> bag, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { } patient
            || !HasComp<BloodstreamComponent>(patient)
            || !TryComp<IVLineComponent>(bag, out var line)
            || TryGetStand(bag, out _))
            return;

        args.Handled = true;

        if (line.AttachedTo != null)
            Detach((bag, line), args.User, false);
        else if (patient == args.User)
            _popup.PopupEntity(Loc.GetString("iv-cannot-self"), bag, args.User);
        else
            TryStartAttach((bag, line), args.User, patient);
    }

    private void TryStartAttach(Entity<IVLineComponent> line, EntityUid user, EntityUid patient)
    {
        if (!HasComp<BloodstreamComponent>(patient) || IsFolded(line))
            return;

        if (!TryGetBag(line, out _))
        {
            _popup.PopupEntity(Loc.GetString("iv-no-bag", ("iv", line.Owner)), line, user);
            return;
        }

        if (!InRange(line, patient, line.Comp.Range))
            return;

        var args = new DoAfterArgs(EntityManager, user, line.Comp.AttachDelay, new IVAttachDoAfterEvent(), line, patient, line)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        var target = Identity.Entity(patient, EntityManager);
        _popup.PopupEntity(Loc.GetString("iv-attach-start-self", ("iv", line.Owner), ("target", target)), patient, user);
        _popup.PopupEntity(Loc.GetString("iv-attach-start-others", ("user", Identity.Entity(user, EntityManager)), ("iv", line.Owner), ("target", target)),
            patient, Filter.PvsExcept(user), true);
    }

    private void OnAttachDoAfter(Entity<IVLineComponent> line, ref IVAttachDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } patient)
            return;

        args.Handled = true;

        if (!HasComp<BloodstreamComponent>(patient) || !InRange(line, patient, line.Comp.Range) || !TryGetBag(line, out _))
            return;

        line.Comp.AttachedTo = patient;
        line.Comp.TransferAt = _timing.CurTime;
        Dirty(line);
        UpdateVisuals(line);

        var target = Identity.Entity(patient, EntityManager);
        var mode = line.Comp.Injecting ? "injecting" : "drawing";
        _popup.PopupEntity(Loc.GetString($"iv-attach-self-{mode}", ("iv", line.Owner), ("target", target)), patient, args.User);
        _popup.PopupEntity(Loc.GetString($"iv-attach-others-{mode}", ("user", Identity.Entity(args.User, EntityManager)), ("iv", line.Owner), ("target", target)),
            patient, Filter.PvsExcept(args.User), true);
    }

    /// <summary>
    /// Takes the line out of the patient. <paramref name="rip"/> is for when it was torn out rather than removed.
    /// </summary>
    public void Detach(Entity<IVLineComponent> line, EntityUid? user, bool rip)
    {
        if (line.Comp.AttachedTo is not { } patient)
            return;

        line.Comp.AttachedTo = null;
        Dirty(line);
        UpdateVisuals(line);

        if (Deleted(patient))
            return;

        var target = Identity.Entity(patient, EntityManager);

        if (rip)
        {
            if (line.Comp.RipDamage is { } damage)
                _damageable.TryChangeDamage(patient, damage, true);

            _chat.TryEmoteWithoutChat(patient, line.Comp.RipEmote);
            _popup.PopupEntity(Loc.GetString("iv-rip", ("target", target)), patient);
            return;
        }

        _popup.PopupEntity(Loc.GetString("iv-detach-self", ("iv", line.Owner), ("target", target)), patient, user ?? patient);

        if (user is { } remover)
        {
            _popup.PopupEntity(Loc.GetString("iv-detach-others", ("user", Identity.Entity(remover, EntityManager)), ("iv", line.Owner), ("target", target)),
                patient, Filter.PvsExcept(remover), true);
        }
    }

    private void OnBagUnequipped(Entity<IVBagComponent> bag, ref GotUnequippedHandEvent args)
    {
        // Letting go of a bag that is in someone yanks the line.
        if (TryComp<IVLineComponent>(bag, out var line) && line.AttachedTo != null && !TryGetStand(bag, out _))
            Detach((bag, line), args.User, true);
    }

    #endregion

    #region Mode

    private void OnVerbs(Entity<IVLineComponent> line, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // A bag hanging on a stand is switched through the stand.
        if (TryComp<IVBagComponent>(line, out _) && TryGetStand(line, out _))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => ToggleMode(line, user),
            Text = Loc.GetString("iv-verb-toggle"),
        });
    }

    private void ToggleMode(Entity<IVLineComponent> line, EntityUid user)
    {
        if (!_skills.HasSkill(user, RequiredSkill))
        {
            _popup.PopupEntity(Loc.GetString("skill-required-popup", ("skill", _skills.SkillName(RequiredSkill))), line, user);
            return;
        }

        line.Comp.Injecting = !line.Comp.Injecting;
        Dirty(line);

        _popup.PopupEntity(Loc.GetString(line.Comp.Injecting ? "iv-now-injecting" : "iv-now-drawing"), line, user);
    }

    #endregion

    #region Transfer

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var lines = EntityQueryEnumerator<IVLineComponent>();
        while (lines.MoveNext(out var uid, out var line))
        {
            if (line.AttachedTo is not { } patient)
                continue;

            if (Deleted(patient) || !InRange(uid, patient, line.Range))
            {
                Detach((uid, line), null, true);
                continue;
            }

            if (now < line.TransferAt)
                continue;

            line.TransferAt = now + line.TransferDelay;

            if (TryGetBag(uid, out var bag))
                Transfer((uid, line), bag.Value, patient);
        }
    }

    private void Transfer(Entity<IVLineComponent> line, Entity<IVBagComponent> bag, EntityUid patient)
    {
        if (!_solutions.TryGetSolution(bag.Owner, bag.Comp.Solution, out var bagSoln, out var bagSolution)
            || !TryComp<BloodstreamComponent>(patient, out var stream)
            || !_solutions.TryGetSolution(patient, stream.BloodSolutionName, out var bloodSoln, out var bloodSolution))
            return;

        if (line.Comp.Injecting)
        {
            var amount = FixedPoint2.Min(line.Comp.TransferAmount, bagSolution.Volume);
            if (amount <= 0 || bloodSolution.AvailableVolume < amount)
                return;

            // Blood and drugs alike go into the bloodstream; metabolism takes it from there.
            var given = _solutions.SplitSolution(bagSoln.Value, amount);
            if (!_bloodstream.TryAddToBloodstream(patient, given))
                _solutions.TryAddSolution(bagSoln.Value, given);
        }
        else
        {
            // Never draw a patient down past the floor, however long the line is left in.
            if (_bloodstream.GetBloodLevel(patient) <= line.Comp.DrawFloor)
                return;

            var amount = FixedPoint2.Min(line.Comp.TransferAmount, bagSolution.AvailableVolume);
            if (amount <= 0)
                return;

            var drawn = _solutions.SplitSolution(bloodSoln.Value, amount);
            if (!_solutions.TryAddSolution(bagSoln.Value, drawn))
                _solutions.TryAddSolution(bloodSoln.Value, drawn);
        }
    }

    #endregion

    #region Visuals

    private void OnStandMapInit(Entity<IVStandComponent> stand, ref MapInitEvent args)
    {
        UpdateVisuals(stand.Owner);
    }

    private void OnStandChanged(Entity<IVStandComponent> stand, ref EntInsertedIntoContainerMessage args)
    {
        UpdateVisuals(stand.Owner);
    }

    private void OnStandChanged(Entity<IVStandComponent> stand, ref EntRemovedFromContainerMessage args)
    {
        UpdateVisuals(stand.Owner);
    }

    private void OnBagMapInit(Entity<IVBagComponent> bag, ref MapInitEvent args)
    {
        UpdateVisuals(bag.Owner);
    }

    private void OnBagSolutionChanged(Entity<IVBagComponent> bag, ref SolutionChangedEvent args)
    {
        UpdateVisuals(bag.Owner);

        if (TryGetStand(bag, out var stand))
            UpdateVisuals(stand.Value.Owner);
    }

    /// <summary>
    /// Pushes the fill level, colour and hookup state of a bag or stand to the sprite.
    /// </summary>
    private void UpdateVisuals(EntityUid uid)
    {
        if (!HasComp<AppearanceComponent>(uid))
            return;

        var fill = 0f;
        var color = Color.White;
        var hasBag = false;

        if (TryGetBag(uid, out var bag)
            && _solutions.TryGetSolution(bag.Value.Owner, bag.Value.Comp.Solution, out _, out var solution))
        {
            hasBag = true;
            color = solution.GetColor(_prototype);
            fill = solution.MaxVolume > 0 ? (float) (solution.Volume / solution.MaxVolume) : 0f;
        }

        _appearance.SetData(uid, IVVisuals.Fill, fill);
        _appearance.SetData(uid, IVVisuals.Color, color);
        _appearance.SetData(uid, IVVisuals.HasBag, hasBag);
        _appearance.SetData(uid, IVVisuals.Attached, TryComp<IVLineComponent>(uid, out var line) && line.AttachedTo != null);
    }

    #endregion
}
