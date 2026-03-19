using System.Linq;
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
            // Entry points for each trader type
            foreach (var token in new[] { "weaponsmith_talk_player", "hero_main_options" })
            {
                starter.AddPlayerLine(
                    $"haggle_start_{token}", token, "haggle_merchant_response",
                    "I would like to discuss your prices...",
                    CanHaggle, null
                );
            }

            starter.AddDialogLine(
                "haggle_merchant_response", "haggle_merchant_response", "haggle_options",
                "Hmm... Let us see what we can work out. What did you have in mind?",
                null, null
            );

            starter.AddPlayerLine(
                "haggle_safe", "haggle_options", "haggle_result",
                "{HAGGLE_SAFE_TEXT}",
                SetSafeText, () => HaggleBehavior.Current?.AttemptHaggle(Hero.OneToOneConversationHero ?? CharacterObject.OneToOneConversationCharacter?.HeroObject, 0, CharacterObject.OneToOneConversationCharacter?.StringId)
            );

            starter.AddPlayerLine(
                "haggle_moderate", "haggle_options", "haggle_result",
                "{HAGGLE_MODERATE_TEXT}",
                SetModerateText, () => HaggleBehavior.Current?.AttemptHaggle(Hero.OneToOneConversationHero ?? CharacterObject.OneToOneConversationCharacter?.HeroObject, 1, CharacterObject.OneToOneConversationCharacter?.StringId)
            );

            starter.AddPlayerLine(
                "haggle_bold", "haggle_options", "haggle_result",
                "{HAGGLE_BOLD_TEXT}",
                SetBoldText, () => HaggleBehavior.Current?.AttemptHaggle(Hero.OneToOneConversationHero ?? CharacterObject.OneToOneConversationCharacter?.HeroObject, 2, CharacterObject.OneToOneConversationCharacter?.StringId)
            );

            starter.AddPlayerLine(
                "haggle_cancel", "haggle_options", "close_window",
                "Never mind, forget I asked.",
                null, null
            );

            starter.AddDialogLine(
                "haggle_result", "haggle_result", "close_window",
                "{HAGGLE_RESULT_TEXT}",
                SetResultText, null
            );
        }

        private bool CanHaggle()
        {
            var character = CharacterObject.OneToOneConversationCharacter;
            if (character == null) 
            { return false; 
            } var traderOccupations = new[]
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

            if (!isTrader) 
            { return false; 
            }

            var npc = Hero.OneToOneConversationHero ?? character.HeroObject;
            string charId = npc?.StringId ?? character.StringId;
            if (charId == null) return false;
            return HaggleBehavior.Current?.CanHaggleWith(npc, charId) ?? false;
        }

        private bool SetSafeText()
        {
            int chance = HaggleBehavior.Current?.GetSuccessChance(0) ?? 70;
            MBTextManager.SetTextVariable("HAGGLE_SAFE_TEXT",
                $"[Safe] A modest request — small discount, low risk. ({chance}% chance, -10% success / +25% fail)");
            return true;
        }

        private bool SetModerateText()
        {
            int chance = HaggleBehavior.Current?.GetSuccessChance(1) ?? 45;
            MBTextManager.SetTextVariable("HAGGLE_MODERATE_TEXT",
                $"[Moderate] Push a bit harder. ({chance}% chance, -20% success / +25% fail)");
            return true;
        }

        private bool SetBoldText()
        {
            int chance = HaggleBehavior.Current?.GetSuccessChance(2) ?? 25;
            MBTextManager.SetTextVariable("HAGGLE_BOLD_TEXT",
                $"[Bold] Go for the big discount. ({chance}% chance, -35% success / +25% fail)");
            return true;
        }

        private bool SetResultText()
        {
            float mod = HaggleBehavior.Current?.CurrentPriceModifier ?? 1f;
            if (mod < 1f)
                MBTextManager.SetTextVariable("HAGGLE_RESULT_TEXT",
                    "Very well, you drive a hard bargain. I will give you better prices today.");
            else
                MBTextManager.SetTextVariable("HAGGLE_RESULT_TEXT",
                    "Ha! You think I am fool? My prices stand — and I am adding a nuisance fee.");
            return true;
        }
        public override void SyncData(IDataStore dataStore) { }

        public void AddDialogs(CampaignGameStarter starter)
        {
            // ---------------------------------------------------------------
            // PATH 1: Ruling clan member
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
            // PATH 2: Merchant notable (lease — higher price, 6 months)
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
        }

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
            MBTextManager.SetTextVariable("CERT_PRICE_TEXT",
                $"A trade certificate for {town.Name} costs {price} gold, valid for one full year. Interested?");
            return true;
        }

        private bool SetMerchantCertPriceText()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return false;
            int price = CalculateCertificatePrice(town, true);
            MBTextManager.SetTextVariable("MERCHANT_CERT_PRICE_TEXT",
                $"I can offer a 6-month trade permit for {price} gold. Not cheap, but it gets you trading.");
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
            int price = CalculateCertificatePrice(town, true);
            MBTextManager.SetTextVariable("TRADER_CERT_PRICE_TEXT",
                $"I can arrange a 6-month trade permit for {price} gold. Interested?");
            return true;
        }

        private bool CanAffordTraderCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            return town != null && Hero.MainHero.Gold >= CalculateCertificatePrice(town, true);
        }

        private void OnBuyTraderCertificate()
        {
            var town = Settlement.CurrentSettlement?.Town;
            if (town == null) return;
            TradeRestrictionBehavior.Current?.TryBuyCertificateWithPrice(town, CalculateCertificatePrice(town, true), 42f);
        }

        public static int CalculateCertificatePrice(Town town, bool isMerchant)
        {
            float ratio = MBMath.ClampFloat(town.Prosperity / 8000f, 0f, 1f);
            int basePrice = (int)(2000 + ratio * 8000);
            return isMerchant ? (int)(basePrice * 1.5f) : basePrice;
        }
    }
}




























