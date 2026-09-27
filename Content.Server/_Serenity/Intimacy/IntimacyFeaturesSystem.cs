using Content.Shared._Serenity.Intimacy;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Serenity.Intimacy;

/// <summary>
/// Fills <see cref="IntimacyParticipantComponent.Features"/> from what the character actually has,
/// so acts can require anatomy via <c>actorNeeds</c> / <c>targetNeeds</c>. Currently derives:
/// <list type="bullet">
/// <item><c>Tail</c> — any marking in the Tail category</item>
/// <item><c>Male</c> / <c>Female</c> — from the humanoid's sex (Unsexed sets neither)</item>
/// </list>
/// Recomputed whenever a player spawns into or attaches to a body, which covers character
/// creation, respawns, and body swaps.
/// </summary>
public sealed partial class IntimacyFeaturesSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private InventorySystem _inventory = default!;

    public const string FeatureTail = "Tail";
    public const string FeatureMale = "Male";
    public const string FeatureFemale = "Female";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(ev => Refresh(ev.Mob));
        SubscribeLocalEvent<PlayerAttachedEvent>(ev => Refresh(ev.Entity));
        SubscribeLocalEvent<IntimacyParticipantComponent, ComponentStartup>((uid, _, _) => Refresh(uid));

        // Covered or uncovered changes with every garment, and the anatomy itself with every marking edit.
        SubscribeLocalEvent<IntimacyParticipantComponent, DidEquipEvent>((uid, _, _) => Refresh(uid));
        SubscribeLocalEvent<IntimacyParticipantComponent, DidUnequipEvent>((uid, _, _) => Refresh(uid));
        SubscribeLocalEvent<IntimacyParticipantComponent, MarkingsUpdateEvent>(OnMarkingsUpdate);
    }

    private void OnMarkingsUpdate(Entity<IntimacyParticipantComponent> ent, ref MarkingsUpdateEvent args)
    {
        Refresh(ent.Owner);
    }

    public void Refresh(EntityUid mob)
    {
        if (!TryComp<IntimacyParticipantComponent>(mob, out var participant))
            return;

        var features = new HashSet<string>();

        if (TryComp<HumanoidAppearanceComponent>(mob, out var humanoid))
        {
            if (humanoid.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var tails) && tails.Count > 0)
                features.Add(FeatureTail);

            AddAnatomy(mob, humanoid, features);

            switch (humanoid.Sex)
            {
                case Sex.Male:
                    features.Add(FeatureMale);
                    break;
                case Sex.Female:
                    features.Add(FeatureFemale);
                    break;
            }
        }

        if (participant.Features.SetEquals(features))
            return;

        participant.Features = features;
        Dirty(mob, participant);
    }

    private void AddAnatomy(EntityUid mob, HumanoidAppearanceComponent humanoid, HashSet<string> features)
    {
        foreach (var anatomy in _proto.EnumeratePrototypes<IntimacyAnatomyPrototype>())
        {
            if (!HasVisibleMarking(humanoid, anatomy.Category))
                continue;

            features.Add(anatomy.Feature);

            var covered = false;
            foreach (var slot in anatomy.CoveringSlots)
            {
                if (_inventory.TryGetSlotEntity(mob, slot, out _))
                {
                    covered = true;
                    break;
                }
            }

            if (!covered)
                features.Add(anatomy.AccessibleFeature);
        }
    }

    private static bool HasVisibleMarking(HumanoidAppearanceComponent humanoid, MarkingCategories category)
    {
        if (!humanoid.MarkingSet.Markings.TryGetValue(category, out var markings))
            return false;

        foreach (var marking in markings)
        {
            if (marking.Visible)
                return true;
        }

        return false;
    }
}
