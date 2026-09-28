using System.Text.RegularExpressions;
using Content.Shared.CCVar;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Preferences;

public sealed partial class HumanoidCharacterProfile
{
    private static readonly Regex RestrictedCustomSpecieNameRegex = new(@"[^A-Za-z0-9 '\-,]|\B\s+|\s+\B"); //Starlight

    [DataField] public string SiliconVoice { get; set; } = "";

    [DataField] public string PersonalityDescription { get; set; } = string.Empty;

    [DataField] public string PersonalNotes { get; set; } = string.Empty;

    [DataField] public string OOCNotes { get; set; } = string.Empty;

    [DataField] public string Secrets { get; set; } = string.Empty;

    [DataField] public string ExploitableInfo { get; set; } = string.Empty;

    [DataField] public string CustomSpecieName { get; set; } = "";

    [DataField] public string ForcedPrototype { get; set; } = ""; // Starlight

    [DataField] public List<string> Cybernetics = [];

    [DataField] public string PhysicalDescription { get; set; } = string.Empty;

    /// <summary>
    /// Detailed text that can appear for the character if <see cref="CCVars.FlavorText"/> is enabled.
    /// </summary>
    [DataField]
    [Obsolete("Use PhysicalDescription instead!")]
    public string FlavorText
    {
        get => PhysicalDescription;
        set => PhysicalDescription = value;
    }

    /// <summary>
    /// Serenity - length of the sl_character_info varchar columns. Anything longer made the whole
    /// character save fail in the database instead of being cut short.
    /// </summary>
    public const int MaxLongTextLength = 4096;

    /// <summary>
    /// Serenity - truncates the free-text character info fields to what the database can hold.
    /// PhysicalDescription is already capped by the flavor text limit.
    /// </summary>
    private void EnsureValidLongText()
    {
        PersonalityDescription = ClampLongText(PersonalityDescription);
        PersonalNotes = ClampLongText(PersonalNotes);
        OOCNotes = ClampLongText(OOCNotes);
        Secrets = ClampLongText(Secrets);
        ExploitableInfo = ClampLongText(ExploitableInfo);
    }

    private static string ClampLongText(string? value)
    {
        value ??= string.Empty;
        return value.Length <= MaxLongTextLength ? value : value[..MaxLongTextLength];
    }

    public HumanoidCharacterProfile WithPhysicalDesc(string physicalDesc)
    {
        return new(this) { PhysicalDescription = physicalDesc };
    }

    public HumanoidCharacterProfile WithPersonalityDesc(string personalityDesc)
    {
        return new(this) { PersonalityDescription = personalityDesc };
    }

    public HumanoidCharacterProfile WithSecrets(string secrets)
    {
        return new(this) { Secrets = secrets };
    }

    public HumanoidCharacterProfile WithPersonalNotes(string personalNotes)
    {
        return new(this) { PersonalNotes = personalNotes };
    }

    public HumanoidCharacterProfile WithExploitable(string exploitable)
    {
        return new(this) { ExploitableInfo = exploitable };
    }

    public HumanoidCharacterProfile WithOOCNotes(string oocNotes)
    {
        return new(this) { OOCNotes = oocNotes };
    }

    public HumanoidCharacterProfile WithSiliconVoice(string id)
    {
        return new(this) { SiliconVoice = id };
    }

    public HumanoidCharacterProfile WithCustomSpecieName(string customspeciename)
    {
        return new(this) { CustomSpecieName = customspeciename };
    }

    public HumanoidCharacterProfile WithForcedPrototype(string forcedPrototype)
    {
        return new(this) { ForcedPrototype = forcedPrototype };
    }

    public HumanoidCharacterProfile WithCybernetics(List<string> cybernetics)
    {
        return new(this) { Cybernetics = cybernetics, };
    }

    /// <summary>
    /// Resolves the profile's current species, migrating an obsolete species ID when
    /// a replacement species declares it in SpeciesProfileMigration.
    /// </summary>
    private SpeciesPrototype? ResolveSpecies(IPrototypeManager prototypeManager)
    {
        // If the original species still exists, just migrate to that one.
        if (prototypeManager.TryIndex(Species, out SpeciesPrototype? species))
            return species;

        // If the original species no longer exists, find the "new" species to migrate to.
        foreach (var candidate in prototypeManager.EnumeratePrototypes<SpeciesPrototype>())
        {
            if (candidate.ProfileMigration?.OldSpecies.Contains(Species.Id) != true)
                continue;

            Species = candidate.ID;
            return candidate;
        }

        return null;
    }
}
