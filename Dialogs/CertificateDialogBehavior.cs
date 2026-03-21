using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ArtOfTheTrade.Behaviors;

namespace ArtOfTheTrade.Dialogs
{
    public class CertificateDialogBehavior : CampaignBehaviorBase
    {
        private bool _banditPersuasionSucceeded = false;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddDialogs(starter);
            AddHaggleDialogs(starter);
        }

        public void AddHaggleDialogs(CampaignGameStarter starter)
        {
            // Entry: town traders
            foreach (var token in new[] { "weaponsmith_talk_player", "hero_main_options" })
            {
                starter.AddPlayerLine(
                    $"haggle_start_{token}", token, "haggle_merchant_response",
                    "I would like to discuss your prices...",
                    CanHaggle, null
                );
            }

            // Entry: village notable (headman / rural notable) — haggle
            starter.AddPlayerLine(
                "haggle_start_village", "hero_main_options", "haggle_merchant_response",
                "I would like to work out a good deal with your village...",
                CanHaggleVillageNotable, null
            );

            // Entry: village notable — open trade screen directly
            starter.AddPlayerLine(
                "village_trade_open", "hero_main_options", "close_window",
                "I would like to trade with the village.",
                IsVillageNotable, OpenVillageTrade
            );

            starter.AddDialogLine(
                "haggle_merchant_response", "haggle_merchant_response", "haggle_options",
                "Hmm... Let us see what we can work out. What did you have in mind?",
                null, null
            );

            // Tier options — moderate/bold hidden if player lacks Trade skill
            starter.AddPlayerLine(
                "haggle_safe", "haggle_options", "haggle_outcome",
                "{HAGGLE_SAFE_TEXT}",
                SetSafeText,
                () => HaggleBehavior.Current?.AttemptHaggle(
                    Hero.OneToOneConversationHero ?? CharacterObject.OneToOneConversationCharacter?.HeroObject,
                    0, CharacterObject.OneToOneConversationCharacter?.StringId)
            );

            starter.AddPlayerLine(
                "haggle_moderate", "haggle_options", "haggle_outcome",
                "{HAGGLE_MODERATE_TEXT}",
                SetModerateText,
                () => HaggleBehavior.Current?.AttemptHaggle(
                    Hero.OneToOneConversationHero ?? CharacterObject.OneToOneConversationCharacter?.HeroObject,
                    1, CharacterObject.OneToOneConversationCharacter?.StringId)
            );

            starter.AddPlayerLine(
                "haggle_bold", "haggle_options", "haggle_outcome",
                "{HAGGLE_BOLD_TEXT}",
                SetBoldText,
                () => HaggleBehavior.Current?.AttemptHaggle(
                    Hero.OneToOneConversationHero ?? CharacterObject.OneToOneConversationCharacter?.HeroObject,
                    2, CharacterObject.OneToOneConversationCharacter?.StringId)
            );

            starter.AddPlayerLine(
                "haggle_cancel", "haggle_options", "close_window",
                "Never mind, forget I asked.",
                null, null
            );

            // Outcome: merchant counter-offer
            starter.AddDialogLine(
                "haggle_counter_npc", "haggle_outcome", "haggle_counter_options",
                "{HAGGLE_COUNTER_TEXT}",
                SetCounterText, null
            );

            // Outcome: final result (success or failure)
            starter.AddDialogLine(
                "haggle_final_npc", "haggle_outcome", "close_window",
                "{HAGGLE_FINAL_TEXT}",
                SetFinalResultText, null
            );

            // Counter-offer responses
            starter.AddPlayerLine(
                "haggle_counter_accept", "haggle_counter_options", "close_window",
                "Fair enough, I will take it.",
                null, () => HaggleBehavior.Current?.AcceptCounter()
            );

            starter.AddPlayerLine(
                "haggle_counter_push", "haggle_counter_options", "haggle_outcome",
                "Come now, surely you can do better than that.",
                null, () => HaggleBehavior.Current?.AttemptPushHarder()
            );

            starter.AddPlayerLine(
                "haggle_counter_walk", "haggle_counter_options", "haggle_walkaway_npc",
                "No deal. I will shop elsewhere.",
                null, () => HaggleBehavior.Current?.WalkAway()
            );

            starter.AddDialogLine(
                "haggle_walkaway_npc", "haggle_walkaway_npc", "close_window",
                "As you wish. Come back when you are willing to pay a fair price.",
                null, null
            );
        }

        private bool CanHaggle()
        {
            var character = CharacterObject.OneToOneConversationCharacter;
            if (character == null) return false;

            var traderOccupations = new[]
            {
                Occupation.GoodsTrader,
                Occupation.Weaponsmith,
                Occupation.Armorer,
                Occupation.HorseTrader,
                Occupation.Tavernkeeper,
                Occupation.Blacksmith
            };

            bool isTrader = false;
            foreach (var occ in traderOccupations)
                if (character.Occupation == occ) { isTrader = true; break; }
            if (!isTrader) return false;

            var npc = Hero.OneToOneConversationHero ?? character.HeroObject;
            string charId = npc?.StringId ?? character.StringId;
            if (charId == null) return false;
            return HaggleBehavior.Current?.CanHaggleWith(npc, charId) ?? false;
        }

        private bool CanHaggleVillageNotable()
        {
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null || !settlement.IsVillage) return false;

            var character = CharacterObject.OneToOneConversationCharacter;
            if (character == null) return false;
            if (character.Occupation != Occupation.RuralNotable) return false;

            var hero = Hero.OneToOneConversationHero;
            if (hero == null) return false;
            return HaggleBehavior.Current?.CanHaggleWith(hero, hero.StringId) ?? false;
        }

        private bool IsVillageNotable()
        {
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null || !settlement.IsVillage) return false;
            var character = CharacterObject.OneToOneConversationCharacter;
            return character?.Occupation == Occupation.RuralNotable;
        }

        private void OpenVillageTrade()
        {
            InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
        }

        private bool SetSafeText()
        {
            int chance = HaggleBehavior.Current?.GetSuccessChance(0) ?? 70;
            var merchantId = Hero.OneToOneConversationHero?.StringId ?? CharacterObject.OneToOneConversationCharacter?.StringId;
            int rep = HaggleBehavior.Current?.GetRepScore(merchantId) ?? 0;
            string repTag = rep > 0 ? $", Rep +{rep}" : rep < 0 ? $", Rep {rep}" : "";
            MBTextManager.SetTextVariable("HAGGLE_SAFE_TEXT",
                $"[Safe] A modest request, small discount, low risk. ({chance}% chance{repTag})");
            return true;
        }

        private bool SetModerateText()
        {
            if (Hero.MainHero.GetSkillValue(DefaultSkills.Trade) < 50) return false;
            int chance = HaggleBehavior.Current?.GetSuccessChance(1) ?? 45;
            MBTextManager.SetTextVariable("HAGGLE_MODERATE_TEXT",
                $"[Moderate] Push a bit harder. ({chance}% chance, Trade 50+)");
            return true;
        }

        private bool SetBoldText()
        {
            if (Hero.MainHero.GetSkillValue(DefaultSkills.Trade) < 100) return false;
            int chance = HaggleBehavior.Current?.GetSuccessChance(2) ?? 25;
            MBTextManager.SetTextVariable("HAGGLE_BOLD_TEXT",
                $"[Bold] Go for the big discount. ({chance}% chance, Trade 100+)");
            return true;
        }

        private bool SetCounterText()
        {
            if (HaggleBehavior.Current?.LastOutcome != HaggleOutcome.CounterOffer) return false;
            int pct = (int)((HaggleBehavior.Current?.PendingCounterDiscount ?? 0f) * 100);
            MBTextManager.SetTextVariable("HAGGLE_COUNTER_TEXT",
                $"Hmm, I cannot go that far. But I could manage {pct}% off — take it or leave it.");
            return true;
        }

        private bool SetFinalResultText()
        {
            float mod = HaggleBehavior.Current?.CurrentPriceModifier ?? 1f;
            if (mod < 1f)
                MBTextManager.SetTextVariable("HAGGLE_FINAL_TEXT",
                    "Very well, you drive a hard bargain. I will give you better prices today.");
            else
                MBTextManager.SetTextVariable("HAGGLE_FINAL_TEXT",
                    "Ha! You think I am a fool? My prices stand — and I am adding a nuisance fee.");
            return true;
        }

        public override void SyncData(IDataStore dataStore) { }

        public void AddDialogs(CampaignGameStarter starter)
        {
            // ---------------------------------------------------------------
            // PATH 1: Ruling clan member (1 year)
            // ---------------------------------------------------------------
            starter.AddPlayerLine(
                "trade_cert_clan_ask", "hero_main_options", "trade_cert_clan_response",
                "I'd like to purchase a trade certificate for this town.",
                IsTalkingToRulingClanMember, null
            );
            starter.AddDialogLine(
                "trade_cert_clan_response", "trade_cert_clan_response", "trade_cert_clan_confirm",
                "{CERT_PRICE_TEXT}", SetClanCertPriceText, null
            );
            starter.AddPlayerLine(
                "trade_cert_clan_yes", "trade_cert_clan_confirm", "trade_cert_clan_bought",
                "Yes, I'll take it.",
                CanAffordClanCertificate, OnBuyClanCertificate
            );
            starter.AddPlayerLine(
                "trade_cert_clan_no", "trade_cert_clan_confirm", "close_window",
                "Too expensive. Perhaps another time.",
                null, null
            );
            starter.AddDialogLine(
                "trade_cert_clan_bought", "trade_cert_clan_bought", "close_window",
                "Excellent. One year of free trade. Safe travels.",
                null, null
            );

            // ---------------------------------------------------------------
            // PATH 2: Merchant notable (6 months)
            // ---------------------------------------------------------------
            starter.AddPlayerLine(
                "trade_cert_merchant_ask", "hero_main_options", "trade_cert_merchant_response",
                "I'd like to lease a trade permit for this town.",
                IsTalkingToMerchant, null
            );
            starter.AddDialogLine(
                "trade_cert_merchant_response", "trade_cert_merchant_response", "trade_cert_merchant_confirm",
                "{MERCHANT_CERT_PRICE_TEXT}", SetMerchantCertPriceText, null
            );
            starter.AddPlayerLine(
                "trade_cert_merchant_yes", "trade_cert_merchant_confirm", "trade_cert_merchant_bought",
                "Alright, I'll take it.",
                CanAffordMerchantCertificate, OnBuyMerchantCertificate
            );
            starter.AddPlayerLine(
                "trade_cert_merchant_no", "trade_cert_merchant_confirm", "close_window",
                "That's too much. I'll look elsewhere.",
                null, null
            );
            starter.AddDialogLine(
                "trade_cert_merchant_bought", "trade_cert_merchant_bought", "close_window",
                "Here you go. Six months only — come back if you need it renewed.",
                null, null
            );

            // ---------------------------------------------------------------
            // PATH 3: Regular trader (3-day pass)
            // ---------------------------------------------------------------
            foreach (var token in new[] { "weaponsmith_talk_player", "hero_main_options" })
            {
                starter.AddPlayerLine(
                    $"trade_pass_trader_ask_{token}", token, "trade_pass_trader_response",
                    "Can you arrange a short-term trade pass for this town?",
                    CanBuyFromTrader, null
                );
            }
            starter.AddDialogLine(
                "trade_pass_trader_response", "trade_pass_trader_response", "trade_pass_trader_confirm",
                "{TRADER_CERT_PRICE_TEXT}", SetTraderCertPriceText, null
            );
            starter.AddPlayerLine(
                "trade_pass_trader_yes", "trade_pass_trader_confirm", "trade_pass_trader_bought",
                "Alright, let's do it.",
                CanAffordTraderCertificate, OnBuyTraderCertificate
            );
            starter.AddPlayerLine(
                "trade_pass_trader_no", "trade_pass_trader_confirm", "close_window",
                "Too much for three days. Maybe later.",
                null, null
            );
            starter.AddDialogLine(
                "trade_pass_trader_bought", "trade_pass_trader_bought", "close_window",
                "Good. Three days — spend them wisely.",
                null, null
            );

            // ---------------------------------------------------------------
            // PATH 4: Gang leader — black-market cert via persuasion
            // ---------------------------------------------------------------
            starter.AddPlayerLine(
                "bandit_cert_ask", "hero_main_options", "bandit_cert_response",
                "I need access to the markets here. Word is you can help with that...",
                CanAskBanditCert, null
            );
            starter.AddDialogLine(
                "bandit_cert_response", "bandit_cert_response", "bandit_cert_options",
                "Heh... sharp eyes. Maybe I can. Depends how persuasive you are. What are you offering?",
                null, null
            );
            starter.AddPlayerLine(
                "bandit_cert_smooth", "bandit_cert_options", "bandit_cert_outcome",
                "{BANDIT_CERT_SMOOTH_TEXT}",
                SetBanditSmoothText, () => AttemptBanditPersuasion(0)
            );
            starter.AddPlayerLine(
                "bandit_cert_bold", "bandit_cert_options", "bandit_cert_outcome",
                "{BANDIT_CERT_BOLD_TEXT}",
                SetBanditBoldText, () => AttemptBanditPersuasion(1)
            );
            starter.AddPlayerLine(
                "bandit_cert_cancel", "bandit_cert_options", "close_window",
                "Never mind. I'll find another way.",
                null, null
            );
            // Bandit outcome: success fires first (condition returns false when failed)
            starter.AddDialogLine(
                "bandit_cert_success", "bandit_cert_outcome", "close_window",
                "{BANDIT_CERT_SUCCESS_TEXT}",
                SetBanditSuccessText, null
            );
            // Bandit outcome: failure fallback
            starter.AddDialogLine(
                "bandit_cert_fail", "bandit_cert_outcome", "close_window",
                "{BANDIT_CERT_FAIL_TEXT}",
                SetBanditFailText, null
            );
        }

        // ----- Certificate condition/consequence helpers -----

        private bool IsTalkingToRulingClanMember()
        {
            if (!NeedsCertificate()) return false;
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null || !settlement.IsTown) return false;
            var npc = Hero.OneToOneConversationHero;
            if (npc == null) return false;
            return npc.Clan != null && npc.Clan == settlement.OwnerClan;
        }

        private bool IsTalkingToMerchant()
        {
            if (!NeedsCertificate()) return false;
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null || !settlement.IsTown) return false;
            var npc = Hero.OneToOneConversationHero;
            if (npc == null) return false;
            return npc.Occupation == Occupation.Merchant && npc.CurrentSettlement == settlement;
        }

        private bool NeedsCertificate()
        {
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null || !settlement.IsTown) return false;
            var behavior = TradeRestrictionBehavior.Current;
            if (behavior == null) return false;
            if (behavior.CanPlayerTradeAt(settlement.Town)) return false;
            if (behavior.HasValidCertificate(settlement.Town)) return false;
            return true;
        }

        private bool SetClanCertPriceText()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return false;
            int price = CalculateCertificatePrice(town, false);
            string text;
            if (town.Prosperity >= 5000)
                text = $"Business is running smooth in {town.Name} — we do not really need new traders here. But if you insist, a year's certificate will cost you {price} gold.";
            else if (town.Prosperity >= 2500)
                text = $"Trade in {town.Name} is steady. A full year's trading certificate will set you back {price} gold. Interested?";
            else
                text = $"Between you and me, {town.Name} has seen better days. We welcome new blood — a full year's certificate for just {price} gold.";
            MBTextManager.SetTextVariable("CERT_PRICE_TEXT", text);
            return true;
        }

        private bool SetMerchantCertPriceText()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return false;
            int price = CalculateCertificatePrice(town, true);
            string text;
            if (town.Prosperity >= 5000)
                text = $"The markets here are thriving and we have no shortage of merchants. Securing a 6-month permit for {town.Name} will not be cheap — {price} gold.";
            else if (town.Prosperity >= 2500)
                text = $"Business here in {town.Name} is fair — nothing special. A 6-month trading permit will cost you {price} gold.";
            else
                text = $"Honestly, {town.Name} could use more goods coming through. I will get you a 6-month permit for {price} gold — help bring some life back to these stalls.";
            MBTextManager.SetTextVariable("MERCHANT_CERT_PRICE_TEXT", text);
            return true;
        }

        private bool CanAffordClanCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            return town != null && Hero.MainHero.Gold >= CalculateCertificatePrice(town, false);
        }

        private bool CanAffordMerchantCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            return town != null && Hero.MainHero.Gold >= CalculateCertificatePrice(town, true);
        }

        private void OnBuyClanCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return;
            TradeRestrictionBehavior.Current?.TryBuyCertificateWithPrice(town, CalculateCertificatePrice(town, false), 84f);
        }

        private void OnBuyMerchantCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return;
            TradeRestrictionBehavior.Current?.TryBuyCertificateWithPrice(town, CalculateCertificatePrice(town, true), 42f);
        }

        private bool CanBuyFromTrader()
        {
            var character = CharacterObject.OneToOneConversationCharacter;
            if (character == null) return false;

            var traderOccupations = new[]
            {
                Occupation.GoodsTrader, Occupation.Weaponsmith,
                Occupation.Armorer, Occupation.HorseTrader, Occupation.Blacksmith
            };

            bool isTrader = false;
            foreach (var occ in traderOccupations)
                if (character.Occupation == occ) { isTrader = true; break; }
            if (!isTrader) return false;

            var settlement = Settlement.CurrentSettlement;
            if (settlement == null || !settlement.IsTown) return false;

            var behavior = TradeRestrictionBehavior.Current;
            if (behavior == null) return false;
            if (behavior.CanPlayerTradeAt(settlement.Town)) return false;
            if (behavior.HasValidCertificate(settlement.Town)) return false;
            return true;
        }

        private bool SetTraderCertPriceText()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return false;
            int price = CalculateTraderPassPrice(town);
            string text;
            if (town.Prosperity >= 5000)
                text = $"Things are booming here in {town.Name}. A 3-day pass will not be cheap — {price} gold, take it or leave it.";
            else if (town.Prosperity >= 2500)
                text = $"I can put in a word for you — three days of access in {town.Name} for {price} gold. The usual arrangement.";
            else
                text = $"Things are quiet in {town.Name} right now. I will get you a 3-day pass for {price} gold — help bring some business around here.";
            MBTextManager.SetTextVariable("TRADER_CERT_PRICE_TEXT", text);
            return true;
        }

        private bool CanAffordTraderCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            return town != null && Hero.MainHero.Gold >= CalculateTraderPassPrice(town);
        }

        private void OnBuyTraderCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return;
            TradeRestrictionBehavior.Current?.TryBuyCertificateWithPrice(town, CalculateTraderPassPrice(town), 3f);
        }

        // ----- Bandit / gang leader cert helpers -----

        private bool CanAskBanditCert()
        {
            var npc = Hero.OneToOneConversationHero;
            if (npc == null || npc.Occupation != Occupation.GangLeader) return false;
            var settlement = Settlement.CurrentSettlement
                ?? npc.CurrentSettlement
                ?? npc.HomeSettlement;
            if (settlement == null || !settlement.IsTown) return false;
            var behavior = TradeRestrictionBehavior.Current;
            if (behavior == null) return false;
            return !behavior.CanPlayerTradeAt(settlement.Town) && !behavior.HasValidCertificate(settlement.Town);
        }

        private bool SetBanditSmoothText()
        {
            int chance = GetBanditPersuasionChance(0);
            MBTextManager.SetTextVariable("BANDIT_CERT_SMOOTH_TEXT",
                $"[Smooth] A small bribe and a friendly word. 3-day pass, half price. ({chance}% chance)");
            return true;
        }

        private bool SetBanditBoldText()
        {
            var player = Hero.MainHero;
            if (player.GetSkillValue(DefaultSkills.Charm) < 60 &&
                player.GetSkillValue(DefaultSkills.Roguery) < 60) return false;
            int chance = GetBanditPersuasionChance(1);
            MBTextManager.SetTextVariable("BANDIT_CERT_BOLD_TEXT",
                $"[Bold] Press harder for a 7-day pass, half price. ({chance}% chance, Charm/Roguery 60+)");
            return true;
        }

        private int GetBanditPersuasionChance(int tier)
        {
            var player = Hero.MainHero;
            int charm = player.GetSkillValue(DefaultSkills.Charm);
            int roguery = player.GetSkillValue(DefaultSkills.Roguery);
            int skillBonus = (int)((charm + roguery) / 2f / 300f * 30f);
            int baseChance = tier == 0 ? 65 : 35;
            return MBMath.ClampInt(baseChance + skillBonus, 5, 95);
        }

        private void AttemptBanditPersuasion(int tier)
        {
            var npc = Hero.OneToOneConversationHero;
            var settlement = Settlement.CurrentSettlement
                ?? npc?.CurrentSettlement
                ?? npc?.HomeSettlement;
            var town = settlement?.Town;
            if (town == null) { _banditPersuasionSucceeded = false; return; }

            int chance = GetBanditPersuasionChance(tier);
            int roll = MBRandom.RandomInt(0, 100);
            _banditPersuasionSucceeded = roll < chance;

            if (_banditPersuasionSucceeded)
            {
                int duration = tier == 0 ? 3 : 7;
                int price = (int)(CalculateTraderPassPrice(town) * 0.5f);
                TradeRestrictionBehavior.Current?.TryBuyCertificateWithPrice(town, price, (float)duration);
                Hero.MainHero?.HeroDeveloper?.AddSkillXp(DefaultSkills.Charm, 20f);
                Hero.MainHero?.HeroDeveloper?.AddSkillXp(DefaultSkills.Roguery, 30f);
            }
        }

        private bool SetBanditSuccessText()
        {
            if (!_banditPersuasionSucceeded) return false;
            MBTextManager.SetTextVariable("BANDIT_CERT_SUCCESS_TEXT",
                "Ha. You have a silver tongue, I will give you that. Consider it done — but do not push your luck.");
            return true;
        }

        private bool SetBanditFailText()
        {
            if (_banditPersuasionSucceeded) return false;
            MBTextManager.SetTextVariable("BANDIT_CERT_FAIL_TEXT",
                "Nice try. But words are cheap. Come back with something more convincing — or more coin.");
            return true;
        }

        public static int CalculateCertificatePrice(Town town, bool isMerchant)
        {
            float ratio = MBMath.ClampFloat(town.Prosperity / 8000f, 0f, 1f);
            int basePrice = (int)(2000 + ratio * 8000);
            return isMerchant ? (int)(basePrice * 1.5f) : basePrice;
        }

        public static int CalculateTraderPassPrice(Town town)
        {
            float ratio = MBMath.ClampFloat(town.Prosperity / 8000f, 0f, 1f);
            return (int)(200 + ratio * 300);
        }
    }
}