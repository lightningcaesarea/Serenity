using System.Collections.Generic;
using System.Linq;
using Content.Server._Serenity.Skills;
using Content.Shared._Serenity.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.Tests._Serenity.Skills;

[TestFixture]
[TestOf(typeof(SkillSystem))]
public sealed class SkillPrototypeTest
{
    /// <summary>
    /// Catches YAML mistakes that would otherwise fail silently in game: dangling or circular prerequisites,
    /// jobs missing a prerequisite, do-after rules naming an event that doesn't exist, and unknown skills
    /// on books and locks.
    /// </summary>
    [Test]
    public async Task SkillDataIsConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var proto = server.ResolveDependency<IPrototypeManager>();
        var reflection = server.ResolveDependency<IReflectionManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        var doAfterEvents = reflection.GetAllChildren<DoAfterEvent>().Select(t => t.Name).ToHashSet();
        var skills = proto.EnumeratePrototypes<SkillPrototype>().ToDictionary(s => s.ID);

        Assert.Multiple(() =>
        {
            foreach (var skill in skills.Values)
            {
                foreach (var required in skill.Requires)
                {
                    Assert.That(skills.ContainsKey(required), $"{skill.ID} requires unknown skill {required}");
                }

                Assert.That(HasCycle(skill.ID, skills, new HashSet<string>()), Is.False, $"{skill.ID} has circular prerequisites");

                foreach (var rule in skill.DoAfters)
                {
                    Assert.That(rule.HasFilters, $"{skill.ID} has a do-after rule with no filters, which never matches");
                    foreach (var ev in rule.Events)
                    {
                        Assert.That(doAfterEvents, Does.Contain(ev), $"{skill.ID} names do-after event {ev}, which doesn't exist");
                    }
                }
            }

            foreach (var jobSkills in proto.EnumeratePrototypes<JobSkillsPrototype>())
            {
                Assert.That(proto.HasIndex<JobPrototype>(jobSkills.ID), $"jobSkills {jobSkills.ID} isn't a job");

                foreach (var skill in jobSkills.Skills)
                {
                    if (!skills.TryGetValue(skill, out var skillProto))
                    {
                        Assert.Fail($"job {jobSkills.ID} grants unknown skill {skill}");
                        continue;
                    }

                    foreach (var required in skillProto.Requires)
                    {
                        Assert.That(jobSkills.Skills, Does.Contain(required),
                            $"job {jobSkills.ID} grants {skill} but not its prerequisite {required}");
                    }
                }
            }

            foreach (var entity in proto.EnumeratePrototypes<EntityPrototype>())
            {
                if (entity.TryGetComponent<SkillBookComponent>(out var book, factory))
                    Assert.That(skills.ContainsKey(book.Skill), $"{entity.ID} teaches unknown skill {book.Skill}");

                if (entity.TryGetComponent<SkillRequiredComponent>(out var required, factory))
                {
                    foreach (var skill in required.Skills)
                    {
                        Assert.That(skills.ContainsKey(skill), $"{entity.ID} requires unknown skill {skill}");
                    }
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasCycle(string id, Dictionary<string, SkillPrototype> skills, HashSet<string> visiting)
    {
        if (!visiting.Add(id))
            return true;

        if (skills.TryGetValue(id, out var skill) && skill.Requires.Any(r => HasCycle(r, skills, visiting)))
            return true;

        visiting.Remove(id);
        return false;
    }

    /// <summary>
    /// Skills granted during spawning land on the body, move to the mind when it arrives, and then
    /// follow the mind to a new body.
    /// </summary>
    [Test]
    public async Task SkillsFollowTheMind()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var skillSys = entMan.System<SkillSystem>();
            var mindSys = entMan.System<SharedMindSystem>();

            var body = entMan.Spawn("MobHuman");
            Assert.That(skillSys.GrantSkill(body, "Medicine"));
            Assert.That(skillSys.HasSkill(body, "Medicine"), "a mindless body reads its pending skills");
            Assert.That(skillSys.GetMissingPrerequisites(body, "Surgery"), Is.Empty);
            Assert.That(skillSys.GetMissingPrerequisites(body, "Xenoarchaeology"), Does.Contain(new ProtoId<SkillPrototype>("Research")));

            var mind = mindSys.CreateMind(null);
            mindSys.TransferTo(mind, body, mind: mind);

            Assert.That(entMan.HasComponent<PendingSkillsComponent>(body), Is.False, "pending skills are flushed onto the mind");
            Assert.That(entMan.GetComponent<SkillsComponent>(mind).Skills, Does.Contain(new ProtoId<SkillPrototype>("Medicine")));

            var clone = entMan.Spawn("MobHuman");
            mindSys.TransferTo(mind, clone, mind: mind);
            Assert.That(skillSys.HasSkill(clone, "Medicine"), "the skill follows the mind into a new body");
            Assert.That(skillSys.HasSkill(body, "Medicine"), Is.False, "the old body no longer knows it");

            Assert.That(skillSys.RevokeSkill(clone, "Medicine"));
            Assert.That(skillSys.HasSkill(clone, "Medicine"), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
