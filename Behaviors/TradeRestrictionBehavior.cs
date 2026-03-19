using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;
using ArtOfTheTrade.Models;

namespace ArtOfTheTrade.Behaviors
{
    public class TradeRestrictionBehavior : CampaignBehaviorBase
    {
        private List<TradeCertificate> _certificates = new List<TradeCertificate>();

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_certificates);
                dataStore.SyncData("ArtOfTheTrade_Certificates", ref json);
            }
            if (dataStore.IsLoading)
            {
                var json = "";
                if (dataStore.SyncData("ArtOfTheTrade_Certificates", ref json) && !string.IsNullOrEmpty(json))
                    _certificates = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<ArtOfTheTrade.Models.TradeCertificate>>(json)
                        ?? new System.Collections.Generic.List<ArtOfTheTrade.Models.TradeCertificate>();
            }
        }

        private void OnDailyTick()
        {
            float today = (float)CampaignTime.Now.ToDays;
            _certificates.RemoveAll(c => !c.IsValid(today));
        }

        public bool CanPlayerTradeAt(Town town)
        {
            var player = Hero.MainHero;
            var townFaction = town.MapFaction;

            // Same faction always allowed
            if (player.MapFaction == townFaction) return true;

            // Has a valid certificate
            if (HasValidCertificate(town)) return true;

            // Check trade deal between factions (works for both kingdom players and clanless)
            if (player.MapFaction != null && townFaction != null)
                return !player.MapFaction.IsAtWarWith(townFaction)
                       && HasTradeDeal(player.MapFaction, townFaction);

            return false;
        }

        private bool HasTradeDeal(IFaction factionA, IFaction factionB)
        {
            var tradeAgreements = Campaign.Current?.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
            if (tradeAgreements == null) return false;

            var kingdomA = factionA as Kingdom;
            var kingdomB = factionB as Kingdom;

            if (kingdomA == null || kingdomB == null) return false;

            return tradeAgreements.HasTradeAgreement(kingdomA, kingdomB);
        }

        public bool HasValidCertificate(Town town)
        {
            float today = (float)CampaignTime.Now.ToDays;
            return _certificates.Any(c => c.TownId == town.StringId && c.IsValid(today));
        }

        public void TryBuyCertificateWithPrice(Town town, int price, float durationInDays)
        {
            var player = Hero.MainHero;

            if (HasValidCertificate(town))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You already have a valid certificate for {town.Name}."));
                return;
            }

            if (player.Gold < price)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You need {price} gold for a certificate here."));
                return;
            }

            GiveGoldAction.ApplyForCharacterToSettlement(player, town.Settlement, price);
            _certificates.Add(new TradeCertificate(town, (float)CampaignTime.Now.ToDays, durationInDays));

            string duration = durationInDays >= 84f ? "1 year" : "6 months";
            InformationManager.DisplayMessage(new InformationMessage(
                $"Certificate purchased for {town.Name}. Valid for {duration}."));
        }

        public static TradeRestrictionBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<TradeRestrictionBehavior>();
    }
}








