using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace ArtOfTheTrade.Settings
{
    public class ArtOfTradeSettings : AttributeGlobalSettings<ArtOfTradeSettings>
    {
        public override string Id          => "ArtOfTheTrade_v1";
        public override string DisplayName => "Art of the Trade";
        public override string FolderName  => "ArtOfTheTrade";

        // ── 1. Trade Restrictions ─────────────────────────────────────────────

        [SettingPropertyBool("Enable Trade Restrictions", Order = 0, RequireRestart = false,
            HintText = "Toggle the entire trade restriction system. When disabled, all towns are freely tradeable.")]
        [SettingPropertyGroup("1. Trade Restrictions", GroupOrder = 1)]
        public bool EnableTradeRestrictions { get; set; } = true;

        [SettingPropertyFloatingInteger("Buy Penalty Multiplier", 1f, 10f, "0.0", Order = 1, RequireRestart = false,
            HintText = "Price multiplier when buying at a restricted town without access. Default: 5×.")]
        [SettingPropertyGroup("1. Trade Restrictions", GroupOrder = 1)]
        public float BuyPenaltyMultiplier { get; set; } = 5f;

        [SettingPropertyFloatingInteger("Sell Penalty Multiplier", 0.1f, 1f, "0.00", Order = 2, RequireRestart = false,
            HintText = "Price multiplier when selling at a restricted town without access. Default: 0.5×.")]
        [SettingPropertyGroup("1. Trade Restrictions", GroupOrder = 1)]
        public float SellPenaltyMultiplier { get; set; } = 0.5f;

        // ── 2. Haggle ─────────────────────────────────────────────────────────

        [SettingPropertyBool("Enable Haggle System", Order = 0, RequireRestart = false,
            HintText = "Toggle the haggle dialog options entirely.")]
        [SettingPropertyGroup("2. Haggle", GroupOrder = 2)]
        public bool EnableHaggle { get; set; } = true;

        [SettingPropertyInteger("Certificate Haggle Bonus (%)", 0, 30, Order = 1, RequireRestart = false,
            HintText = "Bonus to haggle success chance when holding a valid trade certificate at the town. Default: 10.")]
        [SettingPropertyGroup("2. Haggle", GroupOrder = 2)]
        public int CertificateHaggleBonus { get; set; } = 10;

        [SettingPropertyInteger("Hand Haggle Bonus Per Hand (%)", 0, 10, Order = 2, RequireRestart = false,
            HintText = "Bonus to haggle success chance per hired caravan hand. Default: 2 (max +10 at 5 hands).")]
        [SettingPropertyGroup("2. Haggle", GroupOrder = 2)]
        public int HandHaggleBonusPerHand { get; set; } = 2;

        [SettingPropertyFloatingInteger("Failure Cooldown (days)", 0f, 30f, "0.0", Order = 3, RequireRestart = false,
            HintText = "Days before re-haggling with the same merchant after a failed attempt. Default: 3.")]
        [SettingPropertyGroup("2. Haggle", GroupOrder = 2)]
        public float HaggleFailureCooldown { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Collapse Cooldown (days)", 0f, 60f, "0.0", Order = 4, RequireRestart = false,
            HintText = "Days before re-haggling with the same merchant after a deal collapses. Default: 7.")]
        [SettingPropertyGroup("2. Haggle", GroupOrder = 2)]
        public float HaggleCollapseCooldown { get; set; } = 7f;

        // ── 3. Caravan Hands ──────────────────────────────────────────────────

        [SettingPropertyBool("Enable Caravan Hands", Order = 0, RequireRestart = false,
            HintText = "Toggle the entire caravan hands feature.")]
        [SettingPropertyGroup("3. Caravan Hands", GroupOrder = 3)]
        public bool EnableCaravanHands { get; set; } = true;

        [SettingPropertyBool("Spawn Player Camel", Order = 1, RequireRestart = false,
            HintText = "Player spawns on a camel when entering outdoor town / castle / village scenes.")]
        [SettingPropertyGroup("3. Caravan Hands", GroupOrder = 3)]
        public bool SpawnPlayerCamel { get; set; } = true;

        [SettingPropertyBool("Spawn Companion Camels", Order = 2, RequireRestart = false,
            HintText = "Companions also spawn on camels in outdoor settlement scenes.")]
        [SettingPropertyGroup("3. Caravan Hands", GroupOrder = 3)]
        public bool SpawnCompanionCamels { get; set; } = true;

        [SettingPropertyBool("Spawn Handlers in Settlements", Order = 3, RequireRestart = false,
            HintText = "Hired caravan hands appear as mounted agents in outdoor settlement scenes.")]
        [SettingPropertyGroup("3. Caravan Hands", GroupOrder = 3)]
        public bool SpawnHandlersInSettlements { get; set; } = true;

        [SettingPropertyInteger("Max Caravan Hands", 1, 10, Order = 4, RequireRestart = false,
            HintText = "Maximum number of caravan hands the player can hire. Default: 5.")]
        [SettingPropertyGroup("3. Caravan Hands", GroupOrder = 3)]
        public int MaxCaravanHands { get; set; } = 5;

        // ── 4. Capture Penalty ────────────────────────────────────────────────

        [SettingPropertyBool("Enable Capture Penalty", Order = 0, RequireRestart = false,
            HintText = "Toggle the entire capture penalty system.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public bool EnableCapturePenalty { get; set; } = true;

        [SettingPropertyBool("Lords Take Gold", Order = 1, RequireRestart = false,
            HintText = "Your gold is seized when captured by a lord or bandit.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public bool LordsTakeGold { get; set; } = true;

        [SettingPropertyBool("Lords Take Inventory", Order = 2, RequireRestart = false,
            HintText = "Your inventory items are seized when captured by a lord or bandit.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public bool LordsTakeInventory { get; set; } = true;

        [SettingPropertyInteger("Low Tier Gold Penalty (%)", 0, 100, Order = 3, RequireRestart = false,
            HintText = "Percentage of gold taken when your clan tier is ≤1 and the captor has no mercy. Default: 25.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public int LowTierGoldPenaltyPct { get; set; } = 25;

        [SettingPropertyBool("Enable Mercy Release", Order = 4, RequireRestart = false,
            HintText = "Merciful lords release low-tier players without taking anything, then set a grace period.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public bool EnableMercyRelease { get; set; } = true;

        [SettingPropertyFloatingInteger("Grace Period (days)", 0f, 30f, "0.0", Order = 5, RequireRestart = false,
            HintText = "Anti-recapture window after a mercy release. Same lord won't capture you again within this window. Default: 1.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public float GracePeriodDays { get; set; } = 1f;

        [SettingPropertyBool("Enable Safe Passage Offer", Order = 6, RequireRestart = false,
            HintText = "Show the option to pay gold for safe passage in the encounter menu before battle.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public bool EnableSafePassageOffer { get; set; } = true;

        [SettingPropertyBool("Enable Demand Goods Back", Order = 7, RequireRestart = false,
            HintText = "Show the option to demand return of seized goods when encountering your former captor.")]
        [SettingPropertyGroup("4. Capture Penalty", GroupOrder = 4)]
        public bool EnableDemandGoodsBack { get; set; } = true;

        // ── 5. Stash ──────────────────────────────────────────────────────────

        [SettingPropertyBool("Enable Stash", Order = 0, RequireRestart = false,
            HintText = "Toggle the stash system entirely.")]
        [SettingPropertyGroup("5. Stash", GroupOrder = 5)]
        public bool EnableStash { get; set; } = true;

        [SettingPropertyInteger("Access Fee (gold)", 0, 10000, Order = 1, RequireRestart = false,
            HintText = "Gold fee per stash visit at towns you don't own or have a workshop in. Default: 50.")]
        [SettingPropertyGroup("5. Stash", GroupOrder = 5)]
        public int StashAccessFee { get; set; } = 50;

        [SettingPropertyBool("Free if Own Town", Order = 2, RequireRestart = false,
            HintText = "No access fee at towns your clan owns.")]
        [SettingPropertyGroup("5. Stash", GroupOrder = 5)]
        public bool FreeStashIfOwnTown { get; set; } = true;

        [SettingPropertyBool("Free if Workshop", Order = 3, RequireRestart = false,
            HintText = "No access fee at towns where you have a workshop.")]
        [SettingPropertyGroup("5. Stash", GroupOrder = 5)]
        public bool FreeStashIfWorkshop { get; set; } = true;

        // ── 6. Market Intelligence ────────────────────────────────────────────

        [SettingPropertyBool("Enable Market Intelligence", Order = 0, RequireRestart = false,
            HintText = "Toggle the tavernkeeper market tip feature.")]
        [SettingPropertyGroup("6. Market Intelligence", GroupOrder = 6)]
        public bool EnableMarketIntel { get; set; } = true;

        [SettingPropertyInteger("Tip Cost (gold)", 0, 10000, Order = 1, RequireRestart = false,
            HintText = "Gold cost per market tip from the tavernkeeper. Default: 50.")]
        [SettingPropertyGroup("6. Market Intelligence", GroupOrder = 6)]
        public int MarketIntelCost { get; set; } = 50;

        [SettingPropertyFloatingInteger("Tip Cooldown (days)", 0f, 30f, "0.0", Order = 2, RequireRestart = false,
            HintText = "Days before buying another tip from the same tavernkeeper. Default: 3.")]
        [SettingPropertyGroup("6. Market Intelligence", GroupOrder = 6)]
        public float MarketIntelCooldown { get; set; } = 3f;

        [SettingPropertyFloatingInteger("Search Radius (map units)", 50f, 500f, "0", Order = 3, RequireRestart = false,
            HintText = "Radius within which nearby towns are scanned for price tips. Default: 150.")]
        [SettingPropertyGroup("6. Market Intelligence", GroupOrder = 6)]
        public float MarketIntelRadius { get; set; } = 150f;
    }
}
