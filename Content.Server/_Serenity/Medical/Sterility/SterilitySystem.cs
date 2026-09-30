using System.Linq;
using Content.Shared._Serenity.Medical.Sterility;
using Content.Shared._Starlight.Medical.Surgery.Components;
using Content.Shared._Starlight.Medical.Surgery.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Forensics.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Serenity.Medical.Sterility;

/// <summary>
/// Makes surgery care about cleanliness. When a step completes, the tools and gloves used and the surgeon's
/// protection decide how dirty the operation was; too dirty harms the patient (sepsis damage, and other systems
/// react to <see cref="SurgeryStepDirtiedEvent"/>). The step then dirties the tools and gloves and leaves the
/// patient's DNA on them, so the next patient is exposed to it.
/// </summary>
public sealed partial class SterilitySystem : SharedSterilitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const string GlovesSlot = "gloves";
    private const string MaskSlot = "mask";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SurgeryStepDirtinessComponent, SurgeryStepCompleteEvent>(OnStepComplete);
    }

    private void OnStepComplete(Entity<SurgeryStepDirtinessComponent> ent, ref SurgeryStepCompleteEvent args)
    {
        ProcessStep(args.User, args.Body, args.Tools, ent.Comp);
    }

    /// <summary>
    /// Works out how dirty the operation was and what that does to the patient, then dirties the tools and gloves.
    /// Returns the total dirtiness used (before this step's own dirt).
    /// </summary>
    public float ProcessStep(EntityUid user, EntityUid patient, IReadOnlyCollection<EntityUid> held, SurgeryStepDirtinessComponent step)
    {
        var config = Config;
        var tools = held.Where(HasComp<SurgeryToolComponent>).ToList();

        // Gloves and masks only matter for surgeons that wear clothes (not borgs and the like)
        var hasInventory = HasComp<InventoryComponent>(user);
        EntityUid? gloves = null;
        if (hasInventory && _inventory.TryGetSlotEntity(user, GlovesSlot, out var glovesEntity))
            gloves = glovesEntity;

        var patientDna = TryComp<DnaComponent>(patient, out var dna) ? dna.DNA : null;

        // What the operation is exposed to
        var total = tools.Sum(GetDirtiness);
        var contaminants = new HashSet<string>();
        foreach (var tool in tools)
        {
            contaminants.UnionWith(GetDnas(tool));
        }

        if (hasInventory)
        {
            if (gloves is { } worn)
            {
                total += GetDirtiness(worn);
                contaminants.UnionWith(GetDnas(worn));
            }
            else
            {
                total += config.MissingGlovesDirt;
            }

            if (!_inventory.TryGetSlotEntity(user, MaskSlot, out _))
                total += config.MissingMaskDirt;
        }

        // The patient's own DNA on the tools is not a contaminant
        if (patientDna != null)
            contaminants.Remove(patientDna);

        total += contaminants.Count * config.CrossContaminationDirt;

        var sepsis = config.SepsisDamage(total);
        if (sepsis > 0f && !_mobState.IsDead(patient))
        {
            var damage = new DamageSpecifier(_proto.Index(config.SepsisDamageType), FixedPoint2.New(sepsis));
            _damageable.TryChangeDamage(patient, damage, ignoreResistances: true, origin: user);
            _popup.PopupEntity(Loc.GetString("surgery-unsterile-warning"), user, user, PopupType.SmallCaution);
        }
        else
        {
            sepsis = 0f;
        }

        var ev = new SurgeryStepDirtiedEvent(user, total, sepsis);
        RaiseLocalEvent(patient, ref ev);

        // Now the step makes everything dirtier and leaves the patient's DNA behind
        var toolDirt = step.ToolDirt ?? config.StepToolDirt;
        var gloveDirt = step.GloveDirt ?? config.StepGloveDirt;

        foreach (var tool in tools)
        {
            AddDirt(tool, toolDirt);
            AddDna(tool, patientDna);
        }

        if (gloves is { } glovesUsed)
        {
            AddDirt(glovesUsed, gloveDirt);
            AddDna(glovesUsed, patientDna);
        }

        return total;
    }
}
