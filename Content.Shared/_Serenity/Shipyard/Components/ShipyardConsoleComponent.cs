// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity.

using Content.Shared.Access;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Shipyard.Components;

/// <summary>
/// A console that sells vessels to players and buys them back. The buyer's ID card goes in the
/// slot and receives a <see cref="ShuttleDeedComponent"/> on purchase.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedShipyardSystem))]
public sealed partial class ShipyardConsoleComponent : Component
{
    public const string TargetIdCardSlotId = "ShipyardConsole-targetId";

    [DataField("targetIdSlot")]
    public ItemSlot TargetIdSlot = new();

    [DataField("soundError")]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField("soundConfirm")]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    /// <summary>
    /// Vessel groups this console offers (matched against <c>group</c> on vessel prototypes).
    /// Vessels named explicitly by a <see cref="ShipyardListingComponent"/> are offered too.
    /// </summary>
    [DataField]
    public List<string> Groups = new() { "Civilian" };

    /// <summary>
    /// Radio channel every purchase and sale is announced on.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> ShipyardChannel = "Command";

    /// <summary>
    /// Optional second channel that gets a redacted announcement. Meant for black-market
    /// consoles so security hears that *something* arrived without learning what.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? SecretShipyardChannel;

    /// <summary>
    /// If set, the buyer's ID card job title becomes this on purchase.
    /// </summary>
    [DataField]
    public string? NewJobTitle;

    /// <summary>
    /// Access levels added to the buyer's ID card on purchase.
    /// </summary>
    [DataField]
    public List<ProtoId<AccessLevelPrototype>> NewAccessLevels = new();

    /// <summary>
    /// Fraction of a ship's appraised value kept by the station when it is sold back.
    /// 0.3 means the seller receives 70%.
    /// </summary>
    [DataField]
    public float SalesTax;
}
