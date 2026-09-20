using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared._Serenity.Consent;

/// <summary>
/// Entity-level consent queries. Prefer these over <see cref="ConsentCheck"/> whenever you have
/// a mob rather than a raw preferences dictionary.
/// </summary>
public abstract partial class SharedConsentSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// Whether the player behind <paramref name="mob"/> allows <paramref name="toggle"/>.
    /// A mob with no consent snapshot (NPCs, unattached bodies) only passes toggles whose
    /// prototype default is true.
    /// </summary>
    public bool Allows(EntityUid mob, string toggle)
    {
        if (!_proto.TryIndex<ConsentTogglePrototype>(toggle, out var proto))
            return false;

        if (!TryComp<PlayerConsentComponent>(mob, out var consent))
            return proto.Default;

        return consent.Allowed.Contains(toggle);
    }

    /// <summary>
    /// True when <paramref name="mob"/> allows every toggle in the list. An empty list passes.
    /// </summary>
    public bool AllowsAll(EntityUid mob, IEnumerable<ProtoId<ConsentTogglePrototype>> toggles)
    {
        foreach (var toggle in toggles)
        {
            if (!Allows(mob, toggle))
                return false;
        }

        return true;
    }

    /// <summary>
    /// True when both parties allow every toggle. When the two are the same entity it is checked once.
    /// </summary>
    public bool Mutual(EntityUid first, EntityUid second, IEnumerable<ProtoId<ConsentTogglePrototype>> toggles)
    {
        var list = toggles as IList<ProtoId<ConsentTogglePrototype>> ?? toggles.ToList();
        if (!AllowsAll(first, list))
            return false;

        return first == second || AllowsAll(second, list);
    }
}
