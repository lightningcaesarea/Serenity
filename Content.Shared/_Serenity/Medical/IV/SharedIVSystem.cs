using System.Diagnostics.CodeAnalysis;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Robust.Shared.Containers;

namespace Content.Shared._Serenity.Medical.IV;

/// <summary>
/// What both sides of the IV stand need: who can be dragged onto, what the examine text says, and which bag a line is running.
/// The moving of fluid and the attaching itself are server-only (<c>IVSystem</c>).
/// </summary>
public abstract partial class SharedIVSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IVStandComponent, CanDragEvent>(OnStandCanDrag);
        SubscribeLocalEvent<IVStandComponent, CanDropDraggedEvent>(OnStandCanDrop);
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

    private void OnStandCanDrag(Entity<IVStandComponent> stand, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnStandCanDrop(Entity<IVStandComponent> stand, ref CanDropDraggedEvent args)
    {
        if (!TryComp<IVLineComponent>(stand, out var line)
            || !HasComp<BloodstreamComponent>(args.Target)
            || !InRange(stand, args.Target, line.Range))
            return;

        args.Handled = true;
        args.CanDrop = true;
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
