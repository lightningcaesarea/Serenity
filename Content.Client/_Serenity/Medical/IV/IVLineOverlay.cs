using Content.Shared._Serenity.Medical.IV;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._Serenity.Medical.IV;

/// <summary>
/// Draws the tube from every hooked-up IV bag or stand to its patient.
/// </summary>
public sealed partial class IVLineOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;

    private readonly TransformSystem _transform;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities;

    public IVLineOverlay()
    {
        IoCManager.InjectDependencies(this);
        _transform = _entity.System<TransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var lines = _entity.EntityQueryEnumerator<IVLineComponent>();
        while (lines.MoveNext(out var uid, out var line))
        {
            if (line.AttachedTo is not { Valid: true } patient)
                continue;

            var from = _transform.GetMapCoordinates(uid);
            var to = _transform.GetMapCoordinates(patient);
            if (from.MapId == MapId.Nullspace || from.MapId != to.MapId || from.MapId != args.MapId)
                continue;

            var start = from.Position + _transform.GetWorldRotation(uid).RotateVec(line.LineOriginOffset);
            args.WorldHandle.DrawLine(start, to.Position, Color.White);
        }
    }
}
