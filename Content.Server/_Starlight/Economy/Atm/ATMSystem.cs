using Content.Server.Hands.Systems;
using Content.Server.Stack;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Server.Player;
using Robust.Shared.Player;
using Content.Server.Mind;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared._NullLink;
using Content.Shared._Starlight.Economy.Atm;

namespace Content.Server._Starlight.Economy.Atm;
public sealed partial class ATMSystem : SharedATMSystem
{
    [Dependency] private IPlayerRolesManager _playerRolesManager = default!;
    [Dependency] private Content.Shared._Serenity.Economy.ISerenityPlayerResourcesManager _playerResources = default!; // Serenity: reason-carrying variant
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    private static readonly EntProtoId<StackComponent> _cash = "NTCredit";
    private readonly object _transferLock = new();
    public override void Initialize()
    {
        SubscribeLocalEvent<ATMComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<NTCashComponent, AfterInteractEvent>(OnAfterInteract);
        Subs.BuiEvents<ATMComponent>(ATMUIKey.Key, subs =>
        {
            subs.Event<ATMWithdrawBuiMsg>(OnWithdraw);
            subs.Event<ATMTransferBuiMsg>(OnTransfer);
        });
        base.Initialize();
    }

    private void OnWithdraw(EntityUid uid, ATMComponent component, ATMWithdrawBuiMsg args)
    {
        if (!_playerResources.TryGetResource(args.Actor, "credits", out var balance) || balance < args.Amount || args.Amount <= 0)
            return;

        if (!_players.TryGetSessionByEntity(args.Actor, out var actorSession))
            return;

        var newBalance = balance - args.Amount;

        _playerResources.TryUpdateResource(actorSession, "credits", -args.Amount, "atm-withdraw"); // Serenity
        var cash = SpawnAtPosition(_cash, Transform(uid).Coordinates);
        var stack = EnsureComp<StackComponent>(cash);
        _stack.SetCount((cash, stack), args.Amount);
        _hands.TryPickup(args.Actor, cash);
        _uiSystem.SetUiState(uid, ATMUIKey.Key, new ATMBuiState() { Balance = (int)newBalance });
        _audioSystem.PlayPvs(component.WithdrawSound, uid);

        _adminLogger.Add(LogType.Economy, LogImpact.Low,
            $"{ToPrettyString(args.Actor):player} withdrew {args.Amount} Sector Credits at {ToPrettyString(uid):entity} (balance {newBalance})"); // Serenity
    }

    private void OnAfterInteract(Entity<NTCashComponent> ent, ref AfterInteractEvent args)
    {
        if (TryComp<StackComponent>(ent.Owner, out var stack)
            && args.Target.HasValue
            && TryComp<ATMComponent>(args.Target, out var atm)
            && _playerResources.TryGetResource(args.User, "credits", out var balance))
        {
            args.Handled = true; // If we don't do this - debug assert and crash at the dev build.
            // Serenity: no deposit fee. The currency exchange machine is the only place value is
            // skimmed; charging again here would double-tax cash that has already been converted.
            var diff = stack.Count;
            var newBalance = balance += diff;
            if (_players.TryGetSessionByEntity(args.User, out var userSession))
                _playerResources.TryUpdateResource(userSession, "credits", diff, "atm-deposit"); // Serenity
            else
                _playerResources.TryUpdateResource(args.User, "credits", diff);
            QueueDel(ent);
            _uiSystem.SetUiState(args.Target.Value, ATMUIKey.Key, new ATMBuiState() { Balance = (int)newBalance! });
            _audioSystem.PlayPvs(atm.DepositSound, args.Target.Value);

            _adminLogger.Add(LogType.Economy, LogImpact.Low,
                $"{ToPrettyString(args.User):player} deposited {diff} Sector Credits at {ToPrettyString(args.Target.Value):entity} (balance {newBalance})"); // Serenity
        }
    }

    private void OnTransfer(EntityUid uid, ATMComponent component, ATMTransferBuiMsg args)
    {
        if (!_playerResources.TryGetResource(args.Actor, "credits", out var balance) || balance < args.Amount || args.Amount < 0)
            return;

        if (string.IsNullOrWhiteSpace(args.Recipient))
        {
            _uiSystem.SetUiState(uid, ATMUIKey.Key, new ATMBuiState
            {
                Balance = (int)balance,
                Message = Loc.GetString("economy-atm-transfer-error-no-recipient"),
                IsError = true
            });
            return;
        }

        if (!_players.TryGetSessionByEntity(args.Actor, out var senderSession))
        {
            _uiSystem.SetUiState(uid, ATMUIKey.Key, new ATMBuiState
            {
                Balance = (int)balance,
                Message = Loc.GetString("economy-atm-transfer-error-generic"),
                IsError = true
            });
            return;
        }

        var matches = new List<ICommonSession>();

        foreach (var reg in _playerRolesManager.Players)
        {
            if (_mind.TryGetMind(reg.Session.UserId, out _, out var mind)
                && !string.IsNullOrWhiteSpace(mind.CharacterName)
                && string.Equals(mind.CharacterName, args.Recipient, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(reg.Session);
            }
        }

        if (matches.Count != 1)
        {
            var key = matches.Count == 0
                ? "economy-atm-transfer-error-no-recipient"
                : "economy-atm-transfer-error-ambiguous";

            _uiSystem.SetUiState(uid, ATMUIKey.Key, new ATMBuiState
            {
                Balance = (int)balance,
                Message = Loc.GetString(key),
                IsError = true
            });
            return;
        }

        var recipientSession = matches[0];

        if (recipientSession.UserId == senderSession.UserId)
        {
            _uiSystem.SetUiState(uid, ATMUIKey.Key, new ATMBuiState
            {
                Balance = (int)balance,
                Message = Loc.GetString("economy-atm-transfer-error-self"),
                IsError = true
            });
            return;
        }

        lock (_transferLock)
        {
            if (!_playerResources.TryGetResource(recipientSession, "credits", out _))
            {
                _uiSystem.SetUiState(uid, ATMUIKey.Key, new ATMBuiState
                {
                    Balance = (int)balance,
                    Message = Loc.GetString("economy-atm-transfer-error-no-recipient"),
                    IsError = true
                });
                return;
            }

            var newBalance = balance -= args.Amount;

            _playerResources.TryUpdateResource(recipientSession, "credits", args.Amount);
            _playerResources.TryUpdateResource(args.Actor, "credits", -args.Amount);

            var recipientName = _mind.TryGetMind(recipientSession.UserId, out _, out var rMind)
                ? rMind.CharacterName ?? recipientSession.Name
                : recipientSession.Name;

            _uiSystem.SetUiState(uid, ATMUIKey.Key, new ATMBuiState
            {
                Balance = (int)newBalance,
                Message = Loc.GetString("economy-atm-transfer-success", ("amount", args.Amount), ("recipient", recipientName)),
                IsError = false
            });

            _adminLogger.Add(
                LogType.Action,
                LogImpact.Medium,
                $"{ToPrettyString(args.Actor):player} transferred {args.Amount} cr. to {recipientName} via {ToPrettyString(uid):entity}");
        }
    }

    private void OnBeforeActivatableUIOpen(Entity<ATMComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        _playerResources.TryGetResource(args.User, "credits", out var balance);

        _uiSystem.SetUiState(ent.Owner, ATMUIKey.Key, new ATMBuiState() { Balance = (int?)balance ?? 0 });
    }
}
