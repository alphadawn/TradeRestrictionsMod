using System.Collections.Generic;

namespace ArtOfTheTrade.Models
{
    public class Stash
    {
        public string SettlementId { get; set; }
        public int StoredGold { get; set; }
        public List<StoredItem> StoredItems { get; set; } = new List<StoredItem>();

        public Stash() { }

        public Stash(string settlementId)
        {
            SettlementId = settlementId;
            StoredGold = 0;
        }
    }

    public class StoredItem
    {
        public string ItemId { get; set; }
        public int Count { get; set; }

        public StoredItem() { }

        public StoredItem(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
