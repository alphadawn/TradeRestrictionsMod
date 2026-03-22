using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Library;
using ArtOfTheTrade.Models;
using ArtOfTheTrade.Missions;
using ArtOfTheTrade.Save;
using TaleWorlds.MountAndBlade;

namespace ArtOfTheTrade.Behaviors
{
    public class TradeRestrictionBehavior : CampaignBehaviorBase
    {
        private List<TradeCertificate> _certificates => ModSaveManager.Data.Certificates;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener(this, OnMissionStarted);
        }

        public override void SyncData(IDataStore dataStore) { /* handled by ModDataBehavior */ }

        private void OnMissionStarted(IMission mission)
        {
            var m = mission as TaleWorlds.MountAndBlade.Mission;
            if (m == null) return;

            var settlement = TaleWorlds.CampaignSystem.Settlements.Settlement.CurrentSettlement;
            if (settlement == null) return;
            if (!settlement.IsTown && !settlement.IsCastle && !settlement.IsVillage) return;

            if (!m.HasMissionBehavior<ArtOfTheTrade.Missions.PackAnimalMissionBehavior>())
                m.AddMissionBehavior(new ArtOfTheTrade.Missions.PackAnimalMissionBehavior());
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
            // Certificate is automatically revoked when at war with the town's faction
            if (Hero.MainHero?.MapFaction != null && town.MapFaction != null
                && Hero.MainHero.MapFaction.IsAtWarWith(town.MapFaction))
                return false;

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

            string duration = durationInDays >= 84f ? "1 year" : durationInDays >= 42f ? "6 months" : durationInDays >= 7f ? "1 week" : "3 days";
            InformationManager.DisplayMessage(new InformationMessage(
                $"Certificate purchased for {town.Name}. Valid for {duration}."));
        }

        public static TradeRestrictionBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<TradeRestrictionBehavior>();
    }
}











