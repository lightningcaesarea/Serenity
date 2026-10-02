using Content.Server._Serenity.Medical.Recovery;
using Content.Server.Medical.SuitSensors;
using Content.Shared.Implants;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Serenity.Medical;

[TestFixture]
[TestOf(typeof(MedicalRecoverySystem))]
public sealed class MedicalRecoveryTrackerTest
{
    private static readonly EntProtoId Implant = "MedicalRecoveryImplant";

    /// <summary>
    /// A carrier of the recovery implant is on the crew monitor like a suit sensor wearer: the sensor reports their
    /// vitals and position, and they can't turn it off.
    /// </summary>
    [Test]
    public async Task ImplantReportsToTheCrewMonitor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var implants = entMan.System<SharedSubdermalImplantSystem>();
        var sensors = entMan.System<SuitSensorSystem>();

        await server.WaitAssertion(() =>
        {
            var patient = entMan.SpawnEntity("MobHuman", mapData.GridCoords);
            var implant = implants.AddImplant(patient, Implant);
            Assert.That(implant, Is.Not.Null, "the implant should fit");

            var sensor = entMan.GetComponent<SuitSensorComponent>(implant!.Value);
            Assert.That(sensor.User, Is.EqualTo(patient), "the sensor should know who carries it");
            Assert.That(sensor.ControlsLocked, Is.True, "the carrier can't switch it off");
            Assert.That(sensor.Mode, Is.EqualTo(SuitSensorMode.SensorCords), "vitals and position");

            var status = sensors.GetSensorState(implant.Value);
            Assert.That(status, Is.Not.Null, "the crew monitor should see the carrier");
            Assert.That(status!.OwnerUid, Is.EqualTo(entMan.GetNetEntity(patient)));

            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Arrival is announced against the pad's grid name, and falls back to "the station" off a grid.
    /// </summary>
    [Test]
    public async Task ArrivalNamesThePadsGrid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapData = await pair.CreateTestMap();
        var recovery = entMan.System<MedicalRecoverySystem>();
        var meta = entMan.System<MetaDataSystem>();
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            meta.SetEntityName(mapData.Grid.Owner, "NT Serenity");
            var pad = entMan.SpawnEntity("MedicalRecoveryPad", mapData.GridCoords);
            Assert.That(recovery.GetLocationName(pad), Is.EqualTo("NT Serenity"));

            var message = loc.GetString("medical-recovery-announce-arrived", ("name", "Bob"), ("location", recovery.GetLocationName(pad)));
            Assert.That(message, Does.Contain("NT Serenity"));
            Assert.That(message, Does.Not.Contain("the station"));

            var adrift = entMan.SpawnEntity("MedicalRecoveryPad", new MapCoordinates(500, 500, mapData.MapId)); // far from the grid
            Assert.That(recovery.GetLocationName(adrift), Is.EqualTo("the station"), "no grid, no name");

            entMan.DeleteEntity(pad);
            entMan.DeleteEntity(adrift);
        });

        await pair.CleanReturnAsync();
    }
}
