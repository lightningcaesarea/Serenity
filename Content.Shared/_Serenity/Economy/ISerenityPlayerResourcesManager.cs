using Content.Shared._NullLink;
using Robust.Shared.Player;

namespace Content.Shared._Serenity.Economy;

/// <summary>
/// Serenity extension of Starlight's resources interface that lets callers attach a reason to a
/// change. The reason lands in the <c>serenity_resource_transaction</c> ledger so admins can see
/// *why* a balance moved, not just by how much. Starlight's own interface has no such parameter,
/// so systems that want auditable changes depend on this one instead.
/// </summary>
public interface ISerenityPlayerResourcesManager : ISharedNullLinkPlayerResourcesManager
{
    /// <summary>Add <paramref name="delta"/> to a resource and record <paramref name="reason"/> in the ledger.</summary>
    bool TryUpdateResource(ICommonSession session, string id, double delta, string reason);

    /// <summary>Set a resource to an absolute value and record <paramref name="reason"/> in the ledger.</summary>
    bool TrySetResource(ICommonSession session, string id, double value, string reason);
}
