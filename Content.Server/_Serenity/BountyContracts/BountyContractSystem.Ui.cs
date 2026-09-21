// Ported from Frontier Station 14 at commit 5be37d18c2 (2024-07-01), MIT licensed.
// Copyright (c) 2017-2024 New Frontiers. Adapted for Serenity: anyone may post a contract; removing one
// needs the cartridge's access (or being its author); messages arrive through CartridgeMessageEvent.

using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Server.StationRecords.Systems;
using Content.Shared._Serenity.BountyContracts;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CartridgeLoader;
using Content.Shared.Database;
using Content.Shared.PDA;
using Content.Shared.StationRecords;

namespace Content.Server._Serenity.BountyContracts;

public sealed partial class BountyContractSystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    private void InitializeUi()
    {
        SubscribeLocalEvent<BountyContractsCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<BountyContractsCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
    }

    private void OnUiReady(EntityUid uid, BountyContractsCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        ShowList(args.Loader, uid);
    }

    private void OnUiMessage(EntityUid uid, BountyContractsCartridgeComponent component, CartridgeMessageEvent args)
    {
        var loader = GetEntity(args.LoaderUid);

        switch (args)
        {
            case BountyContractOpenCreateUiMsg:
                ShowCreate(loader);
                break;

            case BountyContractCloseCreateUiMsg:
            case BountyContractRefreshListUiMsg:
                ShowList(loader, uid);
                break;

            case BountyContractTryCreateMsg create:
                TryCreate(loader, uid, args.Actor, create.Contract);
                ShowList(loader, uid);
                break;

            case BountyContractTryRemoveUiMsg remove:
                TryRemove(loader, uid, args.Actor, remove.ContractId);
                ShowList(loader, uid);
                break;
        }
    }

    private void ShowCreate(EntityUid loader)
    {
        _cartridgeLoader.UpdateCartridgeUiState(loader, GetCreateState());
    }

    private void ShowList(EntityUid loader, EntityUid cartridge)
    {
        var contracts = GetAllContracts().ToList();
        var canRemove = HasAccess(loader, cartridge);
        _cartridgeLoader.UpdateCartridgeUiState(loader, new BountyContractListUiState(contracts, true, canRemove));
    }

    private BountyContractCreateUiState GetCreateState()
    {
        var targets = new HashSet<BountyContractTargetInfo>();
        var vessels = new HashSet<string>();

        var query = EntityQueryEnumerator<StationRecordsComponent, MetaDataComponent>();
        while (query.MoveNext(out var station, out var records, out var meta))
        {
            vessels.Add(meta.EntityName);

            foreach (var (_, record) in _records.GetRecordsOfType<GeneralStationRecord>(station, records))
            {
                targets.Add(new BountyContractTargetInfo { Name = record.Name, DNA = record.DNA });
            }
        }

        return new BountyContractCreateUiState(targets.ToList(), vessels.ToList());
    }

    private void TryCreate(EntityUid loader, EntityUid cartridge, EntityUid actor, BountyContractRequest request)
    {
        var name = Truncate(request.Name.Trim(), MaxNameLength);
        if (name.Length == 0 || request.Reward < 0)
            return;

        var author = GetContractAuthor(loader);
        var description = Truncate(request.Description.Trim(), MaxDescriptionLength);
        var vessel = Truncate(request.Vessel.Trim(), MaxVesselLength);

        var contract = CreateBountyContract(request.Category, name, request.Reward, description, vessel, request.DNA, author);
        if (contract == null)
            return;

        _adminLogger.Add(LogType.PdaInteract, LogImpact.Low,
            $"{ToPrettyString(actor):player} posted bounty contract {contract.ContractId} on '{name}' ({request.Category}) for {request.Reward} via {ToPrettyString(cartridge):cartridge}");
    }

    private void TryRemove(EntityUid loader, EntityUid cartridge, EntityUid actor, uint contractId)
    {
        if (!TryGetContract(contractId, out var contract))
            return;

        var author = GetContractAuthor(loader);
        var isAuthor = author != null && author == contract.Author;
        if (!isAuthor && !HasAccess(loader, cartridge))
            return;

        if (!RemoveBountyContract(contractId))
            return;

        _adminLogger.Add(LogType.PdaInteract, LogImpact.Low,
            $"{ToPrettyString(actor):player} removed bounty contract {contractId} ('{contract.Name}') via {ToPrettyString(cartridge):cartridge}");
    }

    private bool HasAccess(EntityUid loader, EntityUid cartridge)
    {
        // The cartridge's AccessReader is checked against the PDA holding it (which carries the ID card).
        return !TryComp<AccessReaderComponent>(cartridge, out var reader) || _accessReader.IsAllowed(loader, cartridge, reader);
    }

    private string? GetContractAuthor(EntityUid loader)
    {
        if (!TryComp<PdaComponent>(loader, out var pda) || !TryComp<IdCardComponent>(pda.ContainedId, out var id))
            return null;

        var name = id.FullName ?? Loc.GetString("bounty-contracts-unknown-author-name");
        var job = string.IsNullOrEmpty(id.LocalizedJobTitle) ? Loc.GetString("bounty-contracts-unknown-author-job") : id.LocalizedJobTitle;
        return Loc.GetString("bounty-contracts-author", ("name", name), ("job", job));
    }

    private static string Truncate(string value, int max)
    {
        return value.Length <= max ? value : value[..max];
    }
}
