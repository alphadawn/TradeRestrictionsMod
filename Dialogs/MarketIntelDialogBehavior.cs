using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ArtOfTheTrade.Save;
using ArtOfTheTrade.Settings;

namespace ArtOfTheTrade.Dialogs
{
    public class MarketIntelDialogBehavior : CampaignBehaviorBase
    {
        private int IntelCost    => ArtOfTradeSettings.Instance?.MarketIntelCost      ?? 50;
        private float NearbyRadius => ArtOfTradeSettings.Instance?.MarketIntelRadius  ?? 150f;
        private float CooldownDays => ArtOfTradeSettings.Instance?.MarketIntelCooldown ?? 3f;

        private Dictionary<string, float> _lastIntelDay => ModSaveManager.Data.LastIntelDay;
        private string _lastTip = null;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Entry — tavernkeeper_talk is the correct token for tavernkeeper dialog options
            starter.AddPlayerLine(
                "intel_ask_entry", "tavernkeeper_talk", "intel_ask_npc",
                "What are merchants talking about lately?",
                IsTavernkeeper, null
            );

            // Tavernkeeper response — branches on cooldown
            starter.AddDialogLine(
                "intel_cooldown_npc", "intel_ask_npc", "tavernkeeper_pretalk",
                "Nothing new since we last spoke. Give it a day or two and I may have something worth your coin.",
                IsOnCooldown, null
            );

            starter.AddDialogLine(
                "intel_offer_npc", "intel_ask_npc", "intel_price_options",
                $"Travelers talk... for {IntelCost} gold I can tell you what I have heard about the markets nearby. Interested?",
                null, null
            );

            // Player decides
            starter.AddPlayerLine(
                "intel_buy_yes", "intel_price_options", "intel_result_npc",
                $"Here, {IntelCost} gold. What have you heard?",
                CanAffordIntel, OnBuyIntel
            );

            starter.AddPlayerLine(
                "intel_buy_no", "intel_price_options", "close_window",
                "Not today.",
                null, null
            );

            // Result — tip text was set in OnBuyIntel, then returns to main tavernkeeper options
            starter.AddDialogLine(
                "intel_result_npc", "intel_result_npc", "tavernkeeper_pretalk",
                "{INTEL_TIP_TEXT}",
                SetIntelTipText, null
            );
        }

        // ---- Conditions / Consequences ----

        private bool IsTavernkeeper()
        {
            return (ArtOfTradeSettings.Instance?.EnableMarketIntel ?? true)
                && CharacterObject.OneToOneConversationCharacter?.Occupation == Occupation.Tavernkeeper;
        }

        private bool IsOnCooldown()
        {
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null) return false;
            float today = (float)CampaignTime.Now.ToDays;
            return _lastIntelDay.TryGetValue(settlement.StringId, out float lastDay)
                && (today - lastDay) < CooldownDays;
        }

        private bool CanAffordIntel()
        {
            return Hero.MainHero.Gold >= IntelCost;
        }

        private void OnBuyIntel()
        {
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null) return;

            Hero.MainHero.Gold -= IntelCost;
            _lastIntelDay[settlement.StringId] = (float)CampaignTime.Now.ToDays;
            _lastTip = GenerateTip(settlement) ?? "Honestly, it has been quiet out there. No unusual prices that I have heard of. Sorry — keep the coin.";
        }

        private bool SetIntelTipText()
        {
            MBTextManager.SetTextVariable("INTEL_TIP_TEXT", _lastTip ?? "...");
            return true;
        }

        // ---- Tip generation ----

        private string GenerateTip(Settlement currentSettlement)
        {
            var candidates = new List<(Settlement town, ItemObject item, float ratio)>();

            foreach (var settlement in Campaign.Current.Settlements)
            {
                if (!settlement.IsTown || settlement == currentSettlement) continue;

                float dist = (settlement.GetPosition2D - currentSettlement.GetPosition2D).Length;
                if (dist > NearbyRadius) continue;

                var town = settlement.Town;
                if (town == null) continue;

                foreach (var element in settlement.ItemRoster)
                {
                    var item = element.EquipmentElement.Item;
                    if (item == null || item.Value <= 0) continue;
                    if (!item.IsTradeGood) continue;

                    try
                    {
                        // Pass null party so TradePatch doesn't apply the player's certificate penalty
                        int price = town.GetItemPrice(new EquipmentElement(item), null, false);
                        if (price <= 0) continue;

                        float ratio = (float)price / item.Value;
                        if (ratio > 1.2f)
                            candidates.Add((settlement, item, ratio));
                    }
                    catch { }
                }
            }

            if (candidates.Count == 0) return null;

            // Pick the highest price premium
            var best = candidates.OrderByDescending(c => c.ratio).First();
            int pct = (int)((best.ratio - 1f) * 100f);

            return FormatTip(best.town, best.item, pct);
        }

        private string FormatTip(Settlement town, ItemObject item, int pct)
        {
            if (pct >= 80)
                return $"A trader came in just yesterday half-panicked — said {item.Name} is nearly impossible to find in {town.Name}. Someone is paying {pct}% above the usual rate. If you can get some there, you will do very well.";
            if (pct >= 40)
                return $"Word from a merchant passing through — {item.Name} is fetching about {pct}% above the going rate in {town.Name}. Demand has been strong. Worth a look if you have any to spare.";

            return $"Nothing dramatic, but {item.Name} has been selling a little high in {town.Name} lately — around {pct}% above normal. Might be worth stopping by.";
        }

        public override void SyncData(IDataStore dataStore) { /* handled by ModDataBehavior */ }
    }
}