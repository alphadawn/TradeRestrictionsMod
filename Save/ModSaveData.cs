using System.Collections.Generic;
using ArtOfTheTrade.Behaviors;
using ArtOfTheTrade.Models;

namespace ArtOfTheTrade.Save
{
    /// <summary>
    /// Single container for all mod-specific campaign data.
    /// Serialised to an external JSON file; never stored directly in the game save.
    /// </summary>
    public class ModSaveData
    {
        // CaravanHandBehavior
        public List<CaravanHand> CaravanHands { get; set; } = new List<CaravanHand>();

        // HaggleBehavior — per-merchant cooldowns and reputation
        public Dictionary<string, MerchantRecord> MerchantData { get; set; }
            = new Dictionary<string, MerchantRecord>();

        // CapturePenaltyBehavior
        public string LastCaptorId           { get; set; }
        public int    GoldTaken              { get; set; }
        public int    ItemValueTaken         { get; set; }
        public string GracePeriodCaptorId    { get; set; }
        public float  GracePeriodEndsDay     { get; set; }

        // TradeRestrictionBehavior — purchased certificates
        public List<TradeCertificate> Certificates { get; set; } = new List<TradeCertificate>();

        // StashBehavior — per-settlement vaults
        public Dictionary<string, Stash> Stashes { get; set; }
            = new Dictionary<string, Stash>();

        // MarketIntelDialogBehavior — per-settlement cooldown days
        public Dictionary<string, float> LastIntelDay { get; set; }
            = new Dictionary<string, float>();
    }
}
