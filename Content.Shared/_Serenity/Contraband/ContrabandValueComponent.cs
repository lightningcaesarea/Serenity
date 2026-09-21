namespace Content.Shared._Serenity.Contraband;

/// <summary>
/// Sets an explicit contraband exchange payout for this entity, overriding the value the console
/// would derive from its <c>Contraband</c> severity. Zero makes it unsellable.
/// </summary>
[RegisterComponent]
public sealed partial class ContrabandValueComponent : Component
{
    /// <summary>Payout in Federal Bills.</summary>
    [DataField(required: true)]
    public int Value;
}
