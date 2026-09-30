using System.Numerics;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Serenity.Medical.IV;

/// <summary>
/// A line that can run between an IV bag and a patient's bloodstream. Sits on the bag itself (for a bag held
/// in hand) and on the stand (for a bag hung on it), so one system drives both.
/// Attaching needs the Medicine skill; see <c>skills.yml</c>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class IVLineComponent : Component
{
    /// <summary>
    /// The patient the line is currently in.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AttachedTo;

    /// <summary>
    /// True pushes the bag's contents into the patient, false draws their blood into the bag.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Injecting = true;

    /// <summary>
    /// How far the patient can get from the bag or stand before the line is torn out.
    /// </summary>
    [DataField]
    public float Range = 2f;

    [DataField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(1);

    [DataField]
    public TimeSpan TransferDelay = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TransferAt;

    /// <summary>
    /// How long it takes to find a vein and set the line.
    /// </summary>
    [DataField]
    public TimeSpan AttachDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Damage dealt to the patient when the line is torn out.
    /// </summary>
    [DataField]
    public DamageSpecifier? RipDamage;

    [DataField]
    public ProtoId<EmotePrototype> RipEmote = "Scream";

    /// <summary>
    /// Drawing stops once the patient's blood level (1 is normal) would drop to this.
    /// </summary>
    [DataField]
    public float DrawFloor = 0.8f;

    /// <summary>
    /// Where the line leaves the entity, relative to its centre.
    /// </summary>
    [DataField]
    public Vector2 LineOriginOffset = Vector2.Zero;
}

/// <summary>
/// An IV bag: a container of fluid. Its solution is what the line moves.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IVBagComponent : Component
{
    [DataField]
    public string Solution = "pack";

    /// <summary>
    /// Number of fill sprites above empty; states are <see cref="FillBaseName"/> plus a level from 1.
    /// </summary>
    [DataField]
    public int MaxFillLevels = 7;

    [DataField]
    public string FillBaseName = "bloodpack";
}

/// <summary>
/// A stand that holds one IV bag and runs its line to a patient.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IVStandComponent : Component
{
    /// <summary>
    /// The item slot the bag hangs in.
    /// </summary>
    [DataField]
    public string Slot = "pack";

    [DataField]
    public string AttachedState = "hooked";

    [DataField]
    public string UnattachedState = "unhooked";

    [DataField]
    public string NoBagState = "empty";

    /// <summary>
    /// Percentages from 0 to 100 and the sprite state that shows from that fill upwards.
    /// </summary>
    [DataField]
    public List<(int Percentage, string State)> ReagentStates = new();
}

[Serializable, NetSerializable]
public enum IVVisuals : byte
{
    /// <summary>A float from 0 to 1.</summary>
    Fill,
    Color,
    HasBag,
    Attached,
}

[Serializable, NetSerializable]
public enum IVVisualLayers : byte
{
    Base,
    Reagent,
}

[Serializable, NetSerializable]
public sealed partial class IVAttachDoAfterEvent : SimpleDoAfterEvent;
