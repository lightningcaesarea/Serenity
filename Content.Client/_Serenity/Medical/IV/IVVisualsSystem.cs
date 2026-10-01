using Content.Shared._Serenity.Medical.IV;
using Content.Shared.Rounding;
using Robust.Client.GameObjects;

namespace Content.Client._Serenity.Medical.IV;

/// <summary>
/// Shows the fill level and hookup state of IV bags and stands.
/// </summary>
public sealed partial class IVVisualsSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IVStandComponent, AppearanceChangeEvent>(OnStandAppearance);
        SubscribeLocalEvent<IVBagComponent, AppearanceChangeEvent>(OnBagAppearance);
    }

    private void OnStandAppearance(Entity<IVStandComponent> stand, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (stand.Owner, args.Sprite);
        var data = args.AppearanceData;
        data.TryGetValue(IVVisuals.HasBag, out var hasBagData);
        data.TryGetValue(IVVisuals.Attached, out var attachedData);
        data.TryGetValue(IVVisuals.Fill, out var fillData);
        data.TryGetValue(IVVisuals.Color, out var colorData);

        var hasBag = hasBagData is true;
        var baseState = !hasBag
            ? stand.Comp.NoBagState
            : attachedData is true ? stand.Comp.AttachedState : stand.Comp.UnattachedState;

        if (_sprite.LayerMapTryGet(sprite, IVVisualLayers.Base, out var baseLayer, false))
            _sprite.LayerSetRsiState(sprite, baseLayer, baseState);

        if (!_sprite.LayerMapTryGet(sprite, IVVisualLayers.Reagent, out var reagentLayer, false))
            return;

        // The last state whose threshold the fill has reached.
        var percent = fillData is float fill ? (int) (fill * 100f) : 0;
        string? reagentState = null;
        for (var i = stand.Comp.ReagentStates.Count - 1; i >= 0; i--)
        {
            var (threshold, state) = stand.Comp.ReagentStates[i];
            if (threshold > percent)
                continue;

            reagentState = state;
            break;
        }

        if (!hasBag || reagentState == null)
        {
            _sprite.LayerSetVisible(sprite, reagentLayer, false);
            return;
        }

        _sprite.LayerSetVisible(sprite, reagentLayer, true);
        _sprite.LayerSetRsiState(sprite, reagentLayer, reagentState);
        _sprite.LayerSetColor(sprite, reagentLayer, colorData is Color color ? color : Color.White);
    }

    private void OnBagAppearance(Entity<IVBagComponent> bag, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null
            || !_sprite.LayerMapTryGet((bag.Owner, args.Sprite), IVVisualLayers.Reagent, out var layer, false))
            return;

        var sprite = (bag.Owner, args.Sprite);
        args.AppearanceData.TryGetValue(IVVisuals.Fill, out var fillData);
        args.AppearanceData.TryGetValue(IVVisuals.Color, out var colorData);

        var level = fillData is float fill ? ContentHelpers.RoundToLevels(fill, 1, bag.Comp.MaxFillLevels + 1) : 0;

        // An empty bag shows only its base sprite.
        _sprite.LayerSetVisible(sprite, layer, level > 0);
        if (level <= 0)
            return;

        _sprite.LayerSetRsiState(sprite, layer, $"{bag.Comp.FillBaseName}{level}");
        _sprite.LayerSetColor(sprite, layer, colorData is Color color ? color : Color.White);
    }
}
