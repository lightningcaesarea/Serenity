// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity: the pod is loaded onto its own
// temporary map instead of borrowing the shipyard's, and the tip-off goes to Security.

using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles.Systems;
using Content.Shared.Database;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Shuttles.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._Serenity.Smuggling;

public sealed partial class DeadDropSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeadDropComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DeadDropComponent, GetVerbsEvent<InteractionVerb>>(AddSearchVerb);
    }

    private void OnStartup(EntityUid uid, DeadDropComponent component, ComponentStartup args)
    {
        component.NextDrop ??= _timing.CurTime + TimeSpan.FromSeconds(_random.Next(component.MinimumCoolDown, component.MaximumCoolDown));
    }

    private void AddSearchVerb(EntityUid uid, DeadDropComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null || _timing.CurTime < component.NextDrop)
            return;

        var user = args.User;
        var hands = args.Hands;
        args.Verbs.Add(new InteractionVerb
        {
            IconEntity = GetNetEntity(uid),
            Act = () => SendDeadDrop(uid, component, user, hands),
            Text = Loc.GetString("deaddrop-search-text"),
            Priority = 3,
        });
    }

    private void SendDeadDrop(EntityUid uid, DeadDropComponent component, EntityUid user, HandsComponent hands)
    {
        if (_timing.CurTime < component.NextDrop)
            return;

        var userXform = Transform(user);
        if (userXform.MapID == MapId.Nullspace)
            return;

        // Load the pod on a throwaway map, fling it at the player's map, and let the map clean itself up.
        var stagingMap = _map.CreateMap(out var stagingId);
        if (!_loader.TryLoadGrid(stagingId, component.DropGrid, out var grid))
        {
            Log.Error($"Dead drop: failed to load {component.DropGrid}.");
            Del(stagingMap);
            return;
        }

        var pod = grid.Value.Owner;
        _shuttle.SetIFFColor(pod, component.Color);
        _shuttle.AddIFFFlag(pod, IFFFlags.HideLabel);

        var dropLocation = _random.NextVector2(component.MinimumDistance, component.MaximumDistance);
        var targetMap = _map.GetMap(userXform.MapID);

        if (TryComp<ShuttleComponent>(pod, out var shuttle))
        {
            _shuttle.FTLToCoordinates(pod, shuttle, new EntityCoordinates(targetMap, dropLocation), Angle.Zero,
                startupTime: 0f, hyperspaceTime: component.TravelTime);
        }
        else
        {
            Log.Error($"Dead drop: {component.DropGrid} has no ShuttleComponent; dropping it in place.");
        }

        var despawn = EnsureComp<TimedDespawnComponent>(stagingMap);
        despawn.Lifetime = 15f;

        // Tip off security, naming only the grid the search came from.
        var sender = userXform.GridUid ?? uid;
        _radio.SendRadioMessage(sender, Loc.GetString("deaddrop-security-report"), component.ReportChannel, uid);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} summoned a dead drop to {dropLocation} from {ToPrettyString(uid):source} at {Transform(uid).Coordinates}");

        var hint = new StringBuilder();
        hint.AppendLine(Loc.GetString("deaddrop-hint-pretext"));
        hint.AppendLine();
        hint.AppendLine($"{dropLocation.X:F0}, {dropLocation.Y:F0}");
        hint.AppendLine();
        hint.AppendLine(Loc.GetString("deaddrop-hint-posttext"));

        var paper = Spawn(component.HintPaper, Transform(uid).Coordinates);
        _paper.SetContent(paper, hint.ToString());
        _meta.SetEntityName(paper, Loc.GetString("deaddrop-hint-name"));
        _meta.SetEntityDescription(paper, Loc.GetString("deaddrop-hint-desc"));
        _hands.PickupOrDrop(user, paper, handsComp: hands);

        component.NextDrop = _timing.CurTime + TimeSpan.FromSeconds(_random.Next(component.MinimumCoolDown, component.MaximumCoolDown));
    }
}
