using Content.Shared.Contraband;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._Serenity.Contraband;

/// <summary>
/// Buys contraband placed on <see cref="ContrabandExchangePalletComponent"/>s on the same grid and pays
/// out in cash. Prices are derived from each item's contraband severity unless it carries a
/// <c>ContrabandValue</c> override.
/// </summary>
[RegisterComponent, Access(typeof(ContrabandExchangeSystem))]
public sealed partial class ContrabandExchangeConsoleComponent : Component
{
    /// <summary>Stack spawned as payment. Defaults to Federal Bills.</summary>
    [DataField]
    public ProtoId<StackPrototype> RewardType = "Credit";

    /// <summary>Payout per item by contraband severity. Severities not listed sell for nothing.</summary>
    [DataField]
    public Dictionary<ProtoId<ContrabandSeverityPrototype>, int> SeverityValues = new()
    {
        ["Minor"] = 50,
        ["Restricted"] = 150,
        ["Major"] = 400,
        ["Magical"] = 400,
        ["GrandTheft"] = 800,
        ["HighlyIllegal"] = 800,
        ["Syndicate"] = 1200,
    };
}
