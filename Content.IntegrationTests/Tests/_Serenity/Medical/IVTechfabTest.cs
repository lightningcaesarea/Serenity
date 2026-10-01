using Content.Shared.Lathe;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
public sealed class IVTechfabTest
{
    private static readonly ProtoId<LatheRecipePackPrototype> Pack = "IVStatic";
    private static readonly ProtoId<LatheRecipePrototype> BagRecipe = "IVBag";
    private static readonly ProtoId<LatheRecipePrototype> StandRecipe = "IVStand";

    private static readonly EntProtoId Bag = "IVBag";
    private static readonly EntProtoId Stand = "IVStand";

    private static readonly EntProtoId MedicalTechfab = "MedicalTechFab";
    private static readonly EntProtoId[] OtherTechfabs = ["NanoTrasenTechFab", "SyndicateTechFab"];

    private static LatheComponent Lathe(IPrototypeManager proto, IComponentFactory factory, EntProtoId id)
    {
        Assert.That(proto.Index(id).TryGetComponent<LatheComponent>(out var lathe, factory), $"{id} has a lathe");
        return lathe!;
    }

    /// <summary>
    /// The medical techfab prints empty IV bags and IV stands. The pack is its own so the NanoTrasen and Syndicate
    /// techfabs, which share MedicalStatic, don't get them too.
    /// </summary>
    [Test]
    public async Task MedicalTechfabPrintsIVEquipment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var proto = pair.Server.ProtoMan;
        var factory = pair.Server.ResolveDependency<IComponentFactory>();

        Assert.Multiple(() =>
        {
            Assert.That(Lathe(proto, factory, MedicalTechfab).StaticPacks, Does.Contain(Pack));

            foreach (var id in OtherTechfabs)
            {
                Assert.That(Lathe(proto, factory, id).StaticPacks, Does.Not.Contain(Pack), $"{id} shouldn't print IV equipment");
            }

            Assert.That(proto.Index(Pack).Recipes, Is.EquivalentTo(new[] { BagRecipe, StandRecipe }));

            // The bag prints empty: the filled variants are for stocking, not printing.
            Assert.That(proto.Index(BagRecipe).Result, Is.EqualTo(Bag));
            Assert.That(proto.Index(StandRecipe).Result, Is.EqualTo(Stand));
        });

        await pair.CleanReturnAsync();
    }
}
