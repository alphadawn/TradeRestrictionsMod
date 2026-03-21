using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ArtOfTheTrade.Behaviors
{
    public class CapturePenaltyBehavior : CampaignBehaviorBase
    {
        private string _lastCaptorId = null;
        private int _goldTaken = 0;
        private int _itemValueTaken = 0;

        // Grace period — prevents immediate recapture after a mercy/early-tier release
        private string _gracePeriodCaptorId = null;
        private float _gracePeriodEndsDay = 0f;

        public int GoldTaken => _goldTaken;
        public int ItemValueTaken => _itemValueTaken;

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
            dataStore.SyncData("ArtOfTheTrade_CaptureItemValueTaken", ref _itemValueTaken);
            dataStore.SyncData("ArtOfTheTrade_GracePeriodCaptorId", ref _gracePeriodCaptorId);
            dataStore.SyncData("ArtOfTheTrade_GracePeriodEndsDay", ref _gracePeriodEndsDay);
        }

        public bool HasPendingClaimAgainst(Hero hero) =>
            hero != null
            && hero.StringId == _lastCaptorId
            && (_goldTaken > 0 || _itemValueTaken > 0);

        public int CalculateLordOffer(Hero lord)
        {
            int totalLost = _goldTaken + _itemValueTaken;
            if (totalLost <= 0 || lord == null) return 0;

            float pct = 0.30f;
            pct += lord.GetTraitLevel(DefaultTraits.Honor) * 0.05f;
            pct += lord.GetTraitLevel(DefaultTraits.Generosity) * 0.05f;

            int relation = lord.GetRelation(Hero.MainHero);
            pct += MBMath.ClampFloat(relation / 100f * 0.10f, -0.10f, 0.10f);

            float renown = Hero.MainHero.Clan?.Renown ?? 0f;
            pct += MBMath.ClampFloat(renown / 1000f * 0.10f, 0f, 0.10f);

            pct = MBMath.ClampFloat(pct, 0.10f, 0.80f);
            return (int)(totalLost * pct);
        }

        public void ApplyRecovery(int amount, Hero lord)
        {
            if (amount > 0)
            {
                if (lord != null && lord.Gold >= amount)
                    GiveGoldAction.ApplyBetweenCharacters(lord, Hero.MainHero, amount, true);
                else
                    GiveGoldAction.ApplyBetweenCharacters(null, Hero.MainHero, amount, true);
            }
            _goldTaken = 0;
            _itemValueTaken = 0;
            _lastCaptorId = null;
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner != Hero.MainHero) return;

            var captorHero = capturer?.LeaderHero;
            float today = (float)CampaignTime.Now.ToDays;

            // ── Grace period: same lord captured us again right after releasing us ──
            if (captorHero != null
                && captorHero.StringId == _gracePeriodCaptorId
                && today < _gracePeriodEndsDay)
            {
                EndCaptivityAction.ApplyByReleasedByChoice(prisoner, captorHero);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{captorHero.Name} sighs and waves you off again. \"Just go.\"", Colors.Yellow));
                return;
            }

            // Caravan hands scatter before any seizure
            CaravanHandBehavior.Current?.DismissAll();

            int playerGold = prisoner.Gold;
            int clanTier = Hero.MainHero.Clan?.Tier ?? 0;
            bool isEarlyTier = clanTier <= 1 && captorHero != null;

            // ── Clan tier ≤ 1: lord reacts differently ──────────────────────
            if (isEarlyTier)
            {
                int mercy = captorHero.GetTraitLevel(DefaultTraits.Mercy);

                if (mercy > 0)
                {
                    // Merciful lord: releases without taking anything
                    _gracePeriodCaptorId = captorHero.StringId;
                    _gracePeriodEndsDay = today + 1.0f;
                    _lastCaptorId = null;
                    _goldTaken = 0;
                    _itemValueTaken = 0;
                    EndCaptivityAction.ApplyByReleasedByChoice(prisoner, captorHero);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{captorHero.Name} looks you over and shakes his head. \"You are barely worth the trouble. Take your things and go.\"",
                        Colors.Yellow));
                    return;
                }
                else
                {
                    // No mercy, but recognises you're small — takes only 25% of gold, leaves items
                    int taken = Math.Max(0, playerGold / 4);
                    _lastCaptorId = captorHero.StringId;
                    _goldTaken = taken;
                    _itemValueTaken = 0;

                    if (taken > 0)
                    {
                        GiveGoldAction.ApplyBetweenCharacters(prisoner, captorHero, taken);
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"{captorHero.Name} takes {taken} gold and shrugs. \"Consider this a lesson, upstart. You're not worth my time.\"",
                            Colors.Red));
                    }
                    else
                    {
                        InformationManager.DisplayMessage(new InformationMessage(
                            $"You were captured by {captorHero.Name}. \"You have nothing worth taking. Pathetic.\"",
                            Colors.Red));
                    }
                    return;
                }
            }

            // ── Full penalty (normal clan tier or bandits) ──────────────────
            _lastCaptorId = captorHero?.StringId;
            _goldTaken = playerGold;
            _itemValueTaken = 0;

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
                        $"You were captured by bandits! They looted {playerGold} gold.", Colors.Red));
                }
            }

            // Seize inventory (stash gold is safe — it's not in ItemRoster or Hero.Gold)
            var playerRoster = MobileParty.MainParty?.ItemRoster;
            if (playerRoster == null || playerRoster.Count == 0) return;

            int itemValue = 0;
            var items = playerRoster.ToList();
            foreach (var el in items)
            {
                if (el.Amount <= 0 || el.EquipmentElement.Item == null) continue;
                itemValue += el.EquipmentElement.Item.Value * el.Amount;
                playerRoster.AddToCounts(el.EquipmentElement.Item, -el.Amount);

                if (captorHero?.PartyBelongedTo != null)
                    captorHero.PartyBelongedTo.ItemRoster.AddToCounts(el.EquipmentElement.Item, el.Amount);
                else if (capturer?.MobileParty != null)
                    capturer.MobileParty.ItemRoster.AddToCounts(el.EquipmentElement.Item, el.Amount);
            }

            _itemValueTaken = itemValue;
            if (itemValue > 0)
                InformationManager.DisplayMessage(new InformationMessage(
                    "Your inventory was seized by your captors.", Colors.Red));
        }
    }
}
