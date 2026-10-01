using Content.Shared._Serenity.Skills;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Chemistry;

[TestFixture]
public sealed class ChemMasterSkillTest
{
    /// <summary>
    /// The ChemMaster needs the Chemistry skill, like the chemical dispenser. A rewrite of the ChemMaster prototype
    /// once dropped the lock silently, so this checks both machines still carry it.
    /// </summary>
    [Test]
    public async Task ChemMasterAndDispenserRequireChemistry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ResolveDependency<IPrototypeManager>();
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        Assert.Multiple(() =>
        {
            foreach (var id in new[] { "ChemMaster", "ChemDispenserEmpty" })
            {
                var found = proto.Index<EntityPrototype>(id).TryGetComponent<SkillRequiredComponent>(out var required, factory);
                Assert.That(found, $"{id} must have SkillRequired");
                Assert.That(required?.Skills, Does.Contain((ProtoId<SkillPrototype>) "Chemistry"), $"{id} must require Chemistry");
            }
        });

        await pair.CleanReturnAsync();
    }
}
