// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Shared._Serenity.Shipyard.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Serialization;

namespace Content.Shared._Serenity.Shipyard;

[NetSerializable, Serializable]
public enum ShipyardConsoleUiKey : byte
{
    Shipyard,
}

public abstract partial class SharedShipyardSystem : EntitySystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShipyardConsoleComponent, ComponentRemove>(OnComponentRemove);
    }

    private void OnComponentInit(EntityUid uid, ShipyardConsoleComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, ShipyardConsoleComponent.TargetIdCardSlotId, component.TargetIdSlot);
    }

    private void OnComponentRemove(EntityUid uid, ShipyardConsoleComponent component, ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(uid, component.TargetIdSlot);
    }

    /// <summary>
    /// Full display name of a deeded ship: "[name] [suffix]".
    /// </summary>
    public static string GetFullName(ShuttleDeedComponent deed)
    {
        return string.IsNullOrWhiteSpace(deed.ShuttleNameSuffix)
            ? deed.ShuttleName ?? string.Empty
            : $"{deed.ShuttleName} {deed.ShuttleNameSuffix}";
    }
}
