using Content.Shared._Starlight.Economy;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Economy;

[TestFixture]
public sealed class SalaryTableTest
{
    private static readonly ProtoId<SalariesPrototype> Standard = "standart";

    /// <summary>
    /// Every Marine Police rank is paid, Senior Marine Trooper between Marine Trooper and Marine Quartermaster,
    /// and every paid job names who the salary comes from.
    /// </summary>
    [Test]
    public async Task StandardTableIsConsistent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ResolveDependency<IPrototypeManager>();
        var table = proto.Index(Standard);

        Assert.Multiple(() =>
        {
            Assert.That(table.Jobs.TryGetValue(new ProtoId<JobPrototype>("SeniorOfficer"), out var senior), "Senior Marine Trooper has no salary");
            Assert.That(senior, Is.GreaterThan(table.Jobs[new ProtoId<JobPrototype>("SecurityOfficer")]));
            Assert.That(senior, Is.LessThan(table.Jobs[new ProtoId<JobPrototype>("Warden")]));

            foreach (var job in table.Jobs.Keys)
            {
                Assert.That(proto.HasIndex(job), $"salary for unknown job {job}");
                Assert.That(table.Sender.ContainsKey(job), $"salary for {job} has no sender");
            }
        });

        await pair.CleanReturnAsync();
    }
}
