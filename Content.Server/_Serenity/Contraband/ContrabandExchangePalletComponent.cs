namespace Content.Server._Serenity.Contraband;

/// <summary>
/// Anything sitting on this (anchored, on the console's grid) is offered to the contraband exchange console.
/// </summary>
[RegisterComponent, Access(typeof(ContrabandExchangeSystem))]
public sealed partial class ContrabandExchangePalletComponent : Component;
