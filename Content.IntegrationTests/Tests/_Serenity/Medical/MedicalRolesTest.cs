using System.Collections.Generic;
using System.Linq;
using Content.Shared._Serenity.Skills;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
public sealed class MedicalRolesTest
{
    private static readonly ProtoId<JobSkillsPrototype> DoctorSkills = "MedicalDoctor";

    /// <summary>
    /// The medical department is three working roles: Nurse (the MedicalIntern job), Doctor and Paramedic.
    /// Chemist, Psychologist, Chief Medical Officer and Surgeon are retired (disabled, ids kept so maps and
    /// playtime still load). The Doctor takes over chemistry, so it must hold the Chemistry skill.
    /// </summary>
    [Test]
    public async Task MedicalIsNurseDoctorParamedic()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ResolveDependency<IPrototypeManager>();

        Assert.Multiple(() =>
        {
            foreach (var id in new[] { "MedicalIntern", "MedicalDoctor", "Paramedic" })
            {
                Assert.That(proto.Index<JobPrototype>(id).SetPreference, Is.True, $"{id} should be selectable");
            }

            foreach (var id in new[] { "Chemist", "Psychologist", "ChiefMedicalOfficer", "Surgeon" })
            {
                Assert.That(proto.Index<JobPrototype>(id).SetPreference, Is.False, $"{id} should be retired");
            }

            Assert.That(proto.Index(DoctorSkills).Skills,
                Does.Contain((ProtoId<SkillPrototype>) "Chemistry"), "doctors run chemistry now");
            Assert.That(proto.Index(DoctorSkills).Skills,
                Does.Contain((ProtoId<SkillPrototype>) "Surgery"), "doctors do surgery");

            // The starting skills of each working role
            Assert.That(SkillsOf(proto, "MedicalIntern"), Is.EquivalentTo(new[] { "Medicine", "Diagnostics" }), "Nurse");
            Assert.That(SkillsOf(proto, "MedicalDoctor"), Is.EquivalentTo(new[] { "Medicine", "Diagnostics", "Surgery", "Chemistry" }), "Doctor");
            Assert.That(SkillsOf(proto, "Paramedic"), Is.EquivalentTo(new[] { "Medicine", "Diagnostics" }), "Paramedic");
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<string> SkillsOf(IPrototypeManager proto, string job)
    {
        return proto.Index<JobSkillsPrototype>(job).Skills.Select(skill => skill.Id);
    }
}
