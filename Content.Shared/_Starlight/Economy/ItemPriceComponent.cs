namespace Content.Shared._Starlight.Economy
{
    [RegisterComponent]
    public sealed partial class ItemPriceComponent : Component
    {
        [DataField]
        public string PriceCategory = string.Empty;

        [DataField]
        public int FallbackPrice = 200;

        /// <summary>
        /// Serenity: whether a vending purchase of this item credits the station's cargo bank
        /// account 10x the price, same as every other priced vending item. Set false for items
        /// whose price is a genuine sink (e.g. debiting the buyer without also funding the
        /// station) rather than a market transaction.
        /// </summary>
        [DataField]
        public bool CreditStationCargo = true;
    }
}
