using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ArtOfTheTrade.Behaviors
{
    public class CapturePenaltyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner != Hero.MainHero) return;

            var captorHero = capturer?.LeaderHero;
            int playerGold = prisoner.Gold;

            if (playerGold > 0)
            {
                if (captorHero != null)
                {
                    // Lord capture — gold goes to captor
                    GiveGoldAction.ApplyBetweenCharacters(prisoner, captorHero, playerGold);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You were captured! {captorHero.Name} took {playerGold} gold.", Colors.Red));
                }
                else
                {
                    // Bandit capture — gold is lost (looted and scattered)
                    GiveGoldAction.ApplyBetweenCharacters(prisoner, null, playerGold);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You were captured by bandits! They looted {playerGold} gold from you.", Colors.Red));
                }
            }

            // Transfer inventory
            var playerRoster = MobileParty.MainParty?.ItemRoster;
            if (playerRoster == null || playerRoster.Count == 0) return;

            var items = playerRoster.ToList();
            foreach (var el in items)
            {
                if (el.Amount <= 0) continue;
                playerRoster.AddToCounts(el.EquipmentElement.Item, -el.Amount);

                if (captorHero?.PartyBelongedTo != null)
                    captorHero.PartyBelongedTo.ItemRoster.AddToCounts(el.EquipmentElement.Item, el.Amount);
                else if (capturer?.MobileParty != null)
                    capturer.MobileParty.ItemRoster.AddToCounts(el.EquipmentElement.Item, el.Amount);
            }

            InformationManager.DisplayMessage(new InformationMessage(
                "Your inventory was seized by your captors.", Colors.Red));
        }
    }
}
