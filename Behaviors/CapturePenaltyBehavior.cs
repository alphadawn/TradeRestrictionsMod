using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using ArtOfTheTrade.Save;
using ArtOfTheTrade.Settings;

namespace ArtOfTheTrade.Behaviors
{
    public class CapturePenaltyBehavior : CampaignBehaviorBase
    {
        // All persistent state lives in ModSaveManager.Data
        private string LastCaptorId        { get => ModSaveManager.Data.LastCaptorId;        set => ModSaveManager.Data.LastCaptorId = value; }
        private int    GoldTakenVal        { get => ModSaveManager.Data.GoldTaken;           set => ModSaveManager.Data.GoldTaken = value; }
        private int    ItemValueTakenVal   { get => ModSaveManager.Data.ItemValueTaken;      set => ModSaveManager.Data.ItemValueTaken = value; }
        private string GracePeriodCaptor   { get => ModSaveManager.Data.GracePeriodCaptorId; set => ModSaveManager.Data.GracePeriodCaptorId = value; }
        private float  GracePeriodEndsDay  { get => ModSaveManager.Data.GracePeriodEndsDay;  set => ModSaveManager.Data.GracePeriodEndsDay = value; }

        public int GoldTaken      => ModSaveManager.Data.GoldTaken;
        public int ItemValueTaken => ModSaveManager.Data.ItemValueTaken;

        public static CapturePenaltyBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<CapturePenaltyBehavior>();

        public override void RegisterEvents()
        {
            CampaignEvents.HeroPrisonerTaken.AddNonSerializedListener(this, OnHeroPrisonerTaken);
        }

        public override void SyncData(IDataStore dataStore) { /* handled by ModDataBehavior */ }

        public bool HasPendingClaimAgainst(Hero hero) =>
            hero != null
            && hero.StringId == LastCaptorId
            && (GoldTakenVal > 0 || ItemValueTakenVal > 0);

        public int CalculateLordOffer(Hero lord)
        {
            int totalLost = GoldTakenVal + ItemValueTakenVal;
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
            GoldTakenVal = 0;
            ItemValueTakenVal = 0;
            LastCaptorId = null;
        }

        private void OnHeroPrisonerTaken(PartyBase capturer, Hero prisoner)
        {
            if (prisoner != Hero.MainHero) return;

            var settings = ArtOfTradeSettings.Instance;
            if (!(settings?.EnableCapturePenalty ?? true)) return;

            var captorHero = capturer?.LeaderHero;
            float today = (float)CampaignTime.Now.ToDays;

            // ── Grace period: same lord captured us again right after releasing us ──
            if (captorHero != null
                && captorHero.StringId == GracePeriodCaptor
                && today < GracePeriodEndsDay)
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

                if ((settings?.EnableMercyRelease ?? true) && mercy > 0)
                {
                    // Merciful lord: releases without taking anything
                    float graceDays = settings?.GracePeriodDays ?? 1f;
                    GracePeriodCaptor  = captorHero.StringId;
                    GracePeriodEndsDay = today + graceDays;
                    LastCaptorId       = null;
                    GoldTakenVal       = 0;
                    ItemValueTakenVal  = 0;
                    EndCaptivityAction.ApplyByReleasedByChoice(prisoner, captorHero);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{captorHero.Name} looks you over and shakes his head. \"You are barely worth the trouble. Take your things and go.\"",
                        Colors.Yellow));
                    return;
                }
                else
                {
                    // No mercy — takes a percentage of gold only, leaves items
                    float pct = (settings?.LowTierGoldPenaltyPct ?? 25) / 100f;
                    int taken = (settings?.LordsTakeGold ?? true) ? Math.Max(0, (int)(playerGold * pct)) : 0;
                    LastCaptorId      = captorHero.StringId;
                    GoldTakenVal      = taken;
                    ItemValueTakenVal = 0;

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
            LastCaptorId      = captorHero?.StringId;
            GoldTakenVal      = 0;
            ItemValueTakenVal = 0;

            if ((settings?.LordsTakeGold ?? true) && playerGold > 0)
            {
                GoldTakenVal = playerGold;
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
            if (!(settings?.LordsTakeInventory ?? true)) return;

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

            ItemValueTakenVal = itemValue;
            if (itemValue > 0)
                InformationManager.DisplayMessage(new InformationMessage(
                    "Your inventory was seized by your captors.", Colors.Red));
        }
    }
}
