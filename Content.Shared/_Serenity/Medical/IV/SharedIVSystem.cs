using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Foldable;
using Content.Shared.Item;
using Content.Shared.Popups;
using Robust.Shared.Containers;

namespace Content.Shared._Serenity.Medical.IV;

/// <summary>
/// What both sides of the IV stand need: who can be dragged onto, what the examine text says, and which bag a line is running.
/// The moving of fluid and the attaching itself are server-only (<c>IVSystem</c>).
/// </summary>
public abstract partial class SharedIVSystem : EntitySystem
{
    [Dependency] private FoldableSystem _foldable = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IVStandComponent, CanDropDraggedEvent>(OnStandCanDrop);
        SubscribeLocalEvent<IVStandComponent, FoldAttemptEvent>(OnStandFoldAttempt);
        SubscribeLocalEvent<IVStandComponent, GettingPickedUpAttemptEvent>(OnStandPickupAttempt);
        SubscribeLocalEvent<IVStandComponent, ContainerIsInsertingAttemptEvent>(OnStandInsertAttempt);
        SubscribeLocalEvent<IVLineComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// The bag a line is working from: the entity itself if it is a bag, or whatever hangs on it if it is a stand.
    /// </summary>
    public bool TryGetBag(EntityUid line, [NotNullWhen(true)] out Entity<IVBagComponent>? bag)
    {
        bag = null;

        if (TryComp<IVBagComponent>(line, out var own))
        {
            bag = (line, own);
            return true;
        }

        if (TryComp<IVStandComponent>(line, out var stand)
            && _itemSlots.GetItemOrNull(line, stand.Slot) is { } hung
            && TryComp<IVBagComponent>(hung, out var hungBag))
        {
            bag = (hung, hungBag);
            return true;
        }

        return false;
    }

    /// <summary>
    /// The stand a bag is hanging on, if any.
    /// </summary>
    public bool TryGetStand(EntityUid bag, [NotNullWhen(true)] out Entity<IVStandComponent>? stand)
    {
        stand = null;

        if (_containers.TryGetContainingContainer(bag, out var container)
            && TryComp<IVStandComponent>(container.Owner, out var comp))
        {
            stand = (container.Owner, comp);
            return true;
        }

        return false;
    }

    public bool InRange(EntityUid a, EntityUid b, float range)
    {
        var first = _transform.GetMapCoordinates(a);
        var second = _transform.GetMapCoordinates(b);
        return first.MapId == second.MapId && first.InRange(second, range);
    }

    public bool IsFolded(EntityUid stand)
    {
        return _foldable.IsFolded(stand);
    }

    // Dragging a stand onto yourself folds it (DeployFoldable); onto someone else sets the line.
    private void OnStandCanDrop(Entity<IVStandComponent> stand, ref CanDropDraggedEvent args)
    {
        if (args.User == args.Target
            || IsFolded(stand)
            || !TryComp<IVLineComponent>(stand, out var line)
            || !HasComp<BloodstreamComponent>(args.Target)
            || !InRange(stand, args.Target, line.Range))
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    /// <summary>
    /// A stand with a bag hung on it or a line in someone has to be cleared before it folds.
    /// </summary>
    private void OnStandFoldAttempt(Entity<IVStandComponent> stand, ref FoldAttemptEvent args)
    {
        if (args.Comp.IsFolded)
            return;

        if ((TryComp<IVLineComponent>(stand, out var line) && line.AttachedTo != null) || TryGetBag(stand, out _))
            args.Cancelled = true;
    }

    /// <summary>
    /// An unfolded stand stays where it is; fold it to carry it.
    /// </summary>
    private void OnStandPickupAttempt(Entity<IVStandComponent> stand, ref GettingPickedUpAttemptEvent args)
    {
        if (args.Cancelled || IsFolded(stand))
            return;

        args.Cancel();

        if (args.ShowPopup)
            _popup.PopupPredictedCursor(Loc.GetString("iv-fold-first", ("iv", stand.Owner)), args.User);
    }

    private void OnStandInsertAttempt(Entity<IVStandComponent> stand, ref ContainerIsInsertingAttemptEvent args)
    {
        if (IsFolded(stand))
            args.Cancel();
    }

    private void OnExamined(Entity<IVLineComponent> line, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(IVLineComponent)))
        {
            args.PushMarkup(Loc.GetString(line.Comp.Injecting ? "iv-examine-injecting" : "iv-examine-drawing"));

            if (HasComp<IVStandComponent>(line))
            {
                args.PushMarkup(TryGetBag(line, out var hung)
                    ? Loc.GetString("iv-examine-stand-bag", ("bag", hung.Value.Owner), ("units", Contents(hung.Value)))
                    : Loc.GetString("iv-examine-stand-empty"));
            }
            else if (TryGetBag(line, out var bag))
            {
                args.PushMarkup(Loc.GetString("iv-examine-bag", ("units", Contents(bag.Value))));
            }

            args.PushMarkup(line.Comp.AttachedTo is { } patient
                ? Loc.GetString("iv-examine-attached", ("patient", patient))
                : Loc.GetString("iv-examine-unattached"));
        }
    }

    private int Contents(Entity<IVBagComponent> bag)
    {
        return _solutions.TryGetSolution(bag.Owner, bag.Comp.Solution, out _, out var solution)
            ? solution.Volume.Int()
            : 0;
    }
}
