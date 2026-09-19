using Content.Server.Hands.Systems;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._Serenity.Economy;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Serenity.Economy;

public sealed partial class CurrencyExchangeSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    // SpaceCash ("spesos") uses the confusingly named "Credit" stack type; Starlight's
    // NTCredit is its own type. These are not interchangeable stacks.
    private static readonly ProtoId<StackPrototype> SpesoStack = "Credit";
    private static readonly ProtoId<StackPrototype> CreditStack = "NTCredit";

    private static readonly EntProtoId SpesoEntity = "SpaceCash";
    private static readonly EntProtoId CreditEntity = "NTCredit";

    public override void Initialize()
    {
        SubscribeLocalEvent<CurrencyExchangeComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(Entity<CurrencyExchangeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<StackComponent>(args.Used, out var stack))
            return;

        EntProtoId output;
        int amount;

        if (stack.StackTypeId == SpesoStack)
        {
            output = CreditEntity;
            amount = (int)MathF.Floor(stack.Count / ent.Comp.SpesosPerCredit * (1f - ent.Comp.Fee));
        }
        else if (stack.StackTypeId == CreditStack)
        {
            output = SpesoEntity;
            amount = (int)MathF.Floor(stack.Count * ent.Comp.SpesosPerCredit * (1f - ent.Comp.Fee));
        }
        else
        {
            return;
        }

        // Claim the interaction now that we know it is currency, so the stack is not also
        // handled as a generic insert by something else.
        args.Handled = true;

        if (amount < 1)
        {
            _popup.PopupEntity(Loc.GetString("currency-exchange-too-small"), ent, args.User);
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return;
        }

        var inputCount = stack.Count;
        QueueDel(args.Used);

        var spawned = SpawnAtPosition(output, Transform(ent).Coordinates);
        var newStack = EnsureComp<StackComponent>(spawned);
        _stack.SetCount((spawned, newStack), amount);
        _hands.TryPickup(args.User, spawned);

        _popup.PopupEntity(
            Loc.GetString("currency-exchange-success", ("input", inputCount), ("output", amount)),
            ent,
            args.User);
        _audio.PlayPvs(ent.Comp.ExchangeSound, ent);

        _adminLogger.Add(
            LogType.Action,
            LogImpact.Low,
            $"{ToPrettyString(args.User):player} exchanged {inputCount} {stack.StackTypeId} for {amount} {output} at {ToPrettyString(ent):entity}");
    }
}
