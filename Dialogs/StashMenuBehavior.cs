using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ArtOfTheTrade.Behaviors;
using ArtOfTheTrade.Models;

namespace ArtOfTheTrade.Dialogs
{
    public class StashMenuBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
        }

        // ── Entry ─────────────────────────────────────────────────────────────

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Entry from tavernkeeper dialog
            starter.AddPlayerLine(
                "stash_ask_entry", "tavernkeeper_talk", "stash_keeper_response",
                "I'd like to store something safely.",
                CanOpenStash, SetStashVars);

            // NPC response — branches on affordability of fee
            starter.AddDialogLine(
                "stash_keeper_no_gold", "stash_keeper_response", "tavernkeeper_pretalk",
                "Safe storage costs 50 gold. Come back when you have it.",
                CantAffordFee, null);

            starter.AddDialogLine(
                "stash_keeper_opened", "stash_keeper_response", "stash_main_options",
                "{STASH_STATUS_TEXT}",
                AffordsFeeOrFree, ChargeFee);

            // ── Main options ────────────────────────────────────────────────

            starter.AddPlayerLine(
                "stash_go_deposit", "stash_main_options", "stash_gold_how_npc",
                "Deposit gold (I have {HERO_GOLD}g).",
                () => { SetGoldVars(); return Hero.MainHero.Gold > 0; }, null);

            starter.AddPlayerLine(
                "stash_go_withdraw", "stash_main_options", "stash_gold_how_npc",
                "Withdraw gold ({STASH_GOLD}g stored).",
                () => { SetGoldVars(); return (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) > 0; }, null);

            starter.AddPlayerLine(
                "stash_store_items", "stash_main_options", "tavernkeeper_pretalk",
                "Store items from my inventory.",
                HasInventoryItems, ShowDepositInquiry);

            starter.AddPlayerLine(
                "stash_retrieve_items", "stash_main_options", "tavernkeeper_pretalk",
                "Retrieve stored items ({STASH_ITEM_COUNT} stored).",
                HasStoredItems, ShowRetrieveInquiry);

            starter.AddPlayerLine(
                "stash_leave", "stash_main_options", "tavernkeeper_pretalk",
                "That's all, thanks.",
                null, null);

            // ── Gold submenu ────────────────────────────────────────────────

            // NPC "how much?" bridge — stores deposit or withdraw mode in context
            starter.AddDialogLine(
                "stash_gold_how", "stash_gold_how_npc", "stash_gold_options",
                "How much would you like?",
                null, null);

            // Deposit options
            starter.AddPlayerLine(
                "stash_dep_100", "stash_gold_options", "stash_gold_ack",
                "Deposit 100 gold.",
                () => Hero.MainHero.Gold >= 100, () => DoGoldOp(true, 100));

            starter.AddPlayerLine(
                "stash_dep_1000", "stash_gold_options", "stash_gold_ack",
                "Deposit 1,000 gold.",
                () => Hero.MainHero.Gold >= 1000, () => DoGoldOp(true, 1000));

            starter.AddPlayerLine(
                "stash_dep_10000", "stash_gold_options", "stash_gold_ack",
                "Deposit 10,000 gold.",
                () => Hero.MainHero.Gold >= 10000, () => DoGoldOp(true, 10000));

            starter.AddPlayerLine(
                "stash_dep_all", "stash_gold_options", "stash_gold_ack",
                "Deposit all ({HERO_GOLD}g).",
                () => Hero.MainHero.Gold > 0, () => DoGoldOp(true, Hero.MainHero.Gold));

            // Withdraw options
            starter.AddPlayerLine(
                "stash_wd_100", "stash_gold_options", "stash_gold_ack",
                "Withdraw 100 gold.",
                () => (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) >= 100,
                () => DoGoldOp(false, 100));

            starter.AddPlayerLine(
                "stash_wd_1000", "stash_gold_options", "stash_gold_ack",
                "Withdraw 1,000 gold.",
                () => (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) >= 1000,
                () => DoGoldOp(false, 1000));

            starter.AddPlayerLine(
                "stash_wd_10000", "stash_gold_options", "stash_gold_ack",
                "Withdraw 10,000 gold.",
                () => (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) >= 10000,
                () => DoGoldOp(false, 10000));

            starter.AddPlayerLine(
                "stash_wd_all", "stash_gold_options", "stash_gold_ack",
                "Withdraw all ({STASH_GOLD}g).",
                () => (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) > 0,
                () =>
                {
                    var stash = StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement);
                    if (stash != null) DoGoldOp(false, stash.StoredGold);
                });

            starter.AddPlayerLine(
                "stash_gold_back", "stash_gold_options", "stash_main_options_npc",
                "Never mind.",
                null, null);

            // NPC acknowledgement after gold operation
            starter.AddDialogLine(
                "stash_gold_ack_npc", "stash_gold_ack", "stash_main_options",
                "{STASH_STATUS_TEXT}",
                SetStashStatusText, null);

            // NPC bridge back to main options (after "back")
            starter.AddDialogLine(
                "stash_main_options_bridge", "stash_main_options_npc", "stash_main_options",
                "Anything else?",
                null, null);
        }

        // ── Conditions ────────────────────────────────────────────────────────

        private bool CanOpenStash()
        {
            if (CharacterObject.OneToOneConversationCharacter?.Occupation != Occupation.Tavernkeeper) return false;
            var s = Settlement.CurrentSettlement;
            return s != null && StashBehavior.Current?.CanPlayerStashAt(s) == true;
        }

        private bool CantAffordFee()
        {
            var s = Settlement.CurrentSettlement;
            if (s == null) return false;
            var b = StashBehavior.Current;
            if (b == null) return false;
            return !b.IsFreeStashAt(s) && Hero.MainHero.Gold < 50;
        }

        private bool AffordsFeeOrFree()
        {
            var s = Settlement.CurrentSettlement;
            if (s == null) return false;
            var b = StashBehavior.Current;
            if (b == null) return false;
            return b.IsFreeStashAt(s) || Hero.MainHero.Gold >= 50;
        }

        private bool HasInventoryItems()
        {
            return MobileParty.MainParty?.ItemRoster?.Any(e => e.Amount > 0 && e.EquipmentElement.Item != null) == true;
        }

        private bool HasStoredItems()
        {
            var stash = StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement);
            int count = stash?.StoredItems?.Sum(i => i.Count) ?? 0;
            MBTextManager.SetTextVariable("STASH_ITEM_COUNT", count.ToString());
            return count > 0;
        }

        // ── Consequences ──────────────────────────────────────────────────────

        private void SetStashVars()
        {
            SetStashStatusText();
            SetGoldVars();
        }

        private bool SetStashStatusText()
        {
            var s = Settlement.CurrentSettlement;
            var stash = StashBehavior.Current?.GetOrCreateStash(s);
            int gold = stash?.StoredGold ?? 0;
            int items = stash?.StoredItems?.Sum(i => i.Count) ?? 0;
            bool free = StashBehavior.Current?.IsFreeStashAt(s) == true;
            string feeNote = free ? "" : " [50 gold access fee]";
            MBTextManager.SetTextVariable("STASH_STATUS_TEXT",
                $"Your stash at {s?.Name}: {gold} gold, {items} item(s) stored.{feeNote} What do you need?");
            return true;
        }

        private void SetGoldVars()
        {
            MBTextManager.SetTextVariable("HERO_GOLD", Hero.MainHero.Gold.ToString());
            var stash = StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement);
            MBTextManager.SetTextVariable("STASH_GOLD", (stash?.StoredGold ?? 0).ToString());
        }

        private void ChargeFee()
        {
            var s = Settlement.CurrentSettlement;
            if (s != null) StashBehavior.Current?.TryChargeAccessFee(s);
            SetStashStatusText();
        }

        private void DoGoldOp(bool deposit, int amount)
        {
            var s = Settlement.CurrentSettlement;
            if (deposit)
                StashBehavior.Current?.DepositGold(s, amount);
            else
                StashBehavior.Current?.WithdrawGold(s, amount);
            SetStashVars();
        }

        // ── Item inquiry ──────────────────────────────────────────────────────

        private void ShowDepositInquiry()
        {
            var settlement = Settlement.CurrentSettlement;
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null || settlement == null) return;

            var elements = new List<InquiryElement>();
            foreach (var entry in roster)
            {
                if (entry.Amount <= 0 || entry.EquipmentElement.Item == null) continue;
                var item = entry.EquipmentElement.Item;
                elements.Add(new InquiryElement(
                    item.StringId,
                    $"{item.Name} x{entry.Amount}",
                    null,
                    true,
                    $"Value: ~{item.Value * entry.Amount}g"));
            }

            if (elements.Count == 0) return;

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Store Items",
                    $"Select items to store at {settlement.Name}:",
                    elements,
                    true,
                    1,
                    elements.Count,
                    "Store",
                    "Cancel",
                    selected =>
                    {
                        foreach (var el in selected)
                        {
                            string itemId = el.Identifier as string;
                            if (itemId == null) continue;
                            var item = Game.Current.ObjectManager.GetObject<ItemObject>(itemId);
                            if (item == null) continue;
                            int count = roster.GetItemNumber(item);
                            if (count > 0)
                                StashBehavior.Current?.DepositItem(settlement, item, count);
                        }
                    },
                    _ => { }),
                false);
        }

        private void ShowRetrieveInquiry()
        {
            var settlement = Settlement.CurrentSettlement;
            var stash = StashBehavior.Current?.GetOrCreateStash(settlement);
            if (stash == null || settlement == null) return;

            var elements = new List<InquiryElement>();
            foreach (var stored in stash.StoredItems)
            {
                var item = Game.Current.ObjectManager.GetObject<ItemObject>(stored.ItemId);
                if (item == null) continue;
                elements.Add(new InquiryElement(
                    stored.ItemId,
                    $"{item.Name} x{stored.Count}",
                    null,
                    true,
                    $"Value: ~{item.Value * stored.Count}g"));
            }

            if (elements.Count == 0) return;

            MBInformationManager.ShowMultiSelectionInquiry(
                new MultiSelectionInquiryData(
                    "Retrieve Items",
                    $"Select items to retrieve from {settlement.Name}:",
                    elements,
                    true,
                    1,
                    elements.Count,
                    "Retrieve",
                    "Cancel",
                    selected =>
                    {
                        foreach (var el in selected)
                        {
                            string itemId = el.Identifier as string;
                            if (itemId != null)
                                StashBehavior.Current?.WithdrawItem(settlement, itemId,
                                    stash.StoredItems.FirstOrDefault(i => i.ItemId == itemId)?.Count ?? 0);
                        }
                    },
                    _ => { }),
                false);
        }
    }
}