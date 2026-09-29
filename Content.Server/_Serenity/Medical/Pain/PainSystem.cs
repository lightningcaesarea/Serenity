using Content.Shared._Serenity.Medical.Pain;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Serenity.Medical.Pain;

/// <summary>
/// Server half of pain: periodically recomputes pain (bleeding and wound decay raise no events of their own)
/// and tells the player when their pain changes band.
/// </summary>
public sealed partial class PainSystem : SharedPainSystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PainComponent, PainLevelChangedEvent>(OnLevelChanged);
        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(PainConstants.TickSeconds);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextTick)
            return;

        _nextTick = _timing.CurTime + TimeSpan.FromSeconds(PainConstants.TickSeconds);

        var query = EntityQueryEnumerator<PainComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            Recalculate((uid, comp));
        }
    }

    private void OnLevelChanged(Entity<PainComponent> ent, ref PainLevelChangedEvent args)
    {
        if (args.NewLevel > args.OldLevel)
        {
            var type = args.NewLevel >= PainLevel.Severe ? PopupType.LargeCaution : PopupType.SmallCaution;
            _popup.PopupEntity(Loc.GetString($"pain-rise-{args.NewLevel.ToString().ToLowerInvariant()}"), ent, ent, type);
        }
        else if (args.NewLevel == PainLevel.None)
        {
            _popup.PopupEntity(Loc.GetString(args.Masked ? "pain-numbed" : "pain-gone"), ent, ent);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("pain-easing"), ent, ent);
        }
    }
}
