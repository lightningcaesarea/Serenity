using Robust.Client.Graphics;

namespace Content.Client._Serenity.Medical.IV;

public sealed partial class IVLineOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay.AddOverlay(new IVLineOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<IVLineOverlay>();
    }
}
