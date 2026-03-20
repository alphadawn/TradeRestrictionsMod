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
        private string _lastCaptorId = null;
        private int _goldTaken = 0;
        private int _attemptsLeft = 0;

        public string LastRansomResultText { get; private set; }
        public int GoldTaken => _goldTaken;
        public int AttemptsLeft => _attemptsLeft;

        public bool CanNegotiateRansom(Hero withHero) =>
            withHero != null
            && withHero.StringId == _lastCaptorId
            && _goldTaken > 0
            && _attemptsLeft > 0;

        public static CapturePenaltyBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<CapturePenaltyBehavior>();

        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        }

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("ArtOfTheTrade_CaptureLastCaptorId", ref _lastCaptorId);
            dataStore.SyncData("ArtOfTheTrade_CaptureGoldTaken", ref _goldTaken);
            dataStore.SyncData("ArtOfTheTrade_CaptureAttemptsLeft", ref _attemptsLeft);
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner != Hero.MainHero) return;

            // Caravan hands scatter — removes their animal items from roster before we seize inventory
            CaravanHandBehavior.Current?.DismissAll();

            var captorHero = capturer?.LeaderHero;
            int playerGold = prisoner.Gold;

            // Track for ransom negotiation
            _lastCaptorId = captorHero?.StringId;
            _goldTaken = playerGold;
            _attemptsLeft = captorHero != null ? 2 : 0; // no ransom from bandits

            if (playerGold > 0)
            {
                if (captorHero != null)
                {
                    GiveGoldAction.ApplyBetweenCharacters(prisoner, captorHero, playerGold);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You were captured! {captorHero.Name} took {playerGold} gold.", Colors.Red));
                }
                else
                {
                    GiveGoldAction.ApplyBetweenCharacters(prisoner, null, playerGold);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You were captured by bandits! They looted {playerGold} gold from you.", Colors.Red));
                }
            }

            // Seize inventory (stash is safe — it's not in ItemRoster)
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

        public void DoRansomAttempt()
        {
            if (_attemptsLeft <= 0) return;

            bool isFirstAttempt = _attemptsLeft == 2;
            _attemptsLeft--;

            int charm = Hero.MainHero.GetSkillValue(DefaultSkills.Charm);
            int trade = Hero.MainHero.GetSkillValue(DefaultSkills.Trade);
            float skillBonus = (charm + trade) / 2f / 300f * 0.30f;
            float baseChance = isFirstAttempt ? 0.45f : 0.25f;
            float chance = baseChance + skillBonus;

            float roll = MBRandom.RandomFloat;

            if (roll < chance)
            {
                int returnPct = isFirstAttempt ? 40 : 20;
                int returned = (int)(_goldTaken * returnPct / 100f);

                var captor = Hero.MainHero.PartyBelongedToAsPrisoner?.LeaderHero
                    ?? Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == _lastCaptorId);

                if (captor != null && returned > 0)
                    GiveGoldAction.ApplyBetweenCharacters(captor, Hero.MainHero, returned, true);

                _goldTaken = 0;
                _attemptsLeft = 0;
                LastRansomResultText =
                    $"...Very well. Take {returned} gold and consider the matter closed. Do not push your luck.";
            }
            else if (_attemptsLeft <= 0)
            {
                LastRansomResultText =
                    "You had your chance. Both times. I owe you nothing — this matter is closed.";
            }
            else
            {
                LastRansomResultText =
                    "I am not convinced. You have one more chance before I lose patience entirely.";
            }
        }
    }
}