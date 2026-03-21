using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ArtOfTheTrade.Behaviors;
using ArtOfTheTrade.Models;

namespace ArtOfTheTrade.Dialogs
{
    public class CaravanHandDialogBehavior : CampaignBehaviorBase
    {
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
            // Entry — hire (from both trader talk and hero main options)
            foreach (var token in new[] { "weaponsmith_talk_player", "hero_main_options" })
            {
                starter.AddPlayerLine(
                    $"caravan_hire_entry_{token}", token, "caravan_hire_intro",
                    "I am looking to hire some pack animal handlers.",
                    CanOpenHireMenu, null
                );

                starter.AddPlayerLine(
                    $"caravan_manage_entry_{token}", token, "caravan_manage_npc",
                    "Let me check on my caravan hands.",
                    HasAnyHands, null
                );
            }

            // Hire intro NPC line
            starter.AddDialogLine(
                "caravan_hire_intro", "caravan_hire_intro", "caravan_hire_options",
                "I have fine animals available. What are you after?",
                null, null
            );

            // Hire options — player picks type
            starter.AddPlayerLine(
                "caravan_hire_sumpter", "caravan_hire_options", "caravan_hired_npc",
                $"A sumpter horse handler. ({CaravanHandBehavior.GetHirePrice(CaravanAnimalType.SumpterHorse)} gold, +{CaravanHandBehavior.GetCapacityBonus(CaravanAnimalType.SumpterHorse)} capacity, {CaravanHandBehavior.GetDailyUpkeep(CaravanAnimalType.SumpterHorse)} gold/day)",
                CanHireMore, () => CaravanHandBehavior.Current?.TryHire(CaravanAnimalType.SumpterHorse)
            );

            starter.AddPlayerLine(
                "caravan_hire_mule", "caravan_hire_options", "caravan_hired_npc",
                $"A mule handler. ({CaravanHandBehavior.GetHirePrice(CaravanAnimalType.Mule)} gold, +{CaravanHandBehavior.GetCapacityBonus(CaravanAnimalType.Mule)} capacity, {CaravanHandBehavior.GetDailyUpkeep(CaravanAnimalType.Mule)} gold/day)",
                CanHireMore, () => CaravanHandBehavior.Current?.TryHire(CaravanAnimalType.Mule)
            );

            starter.AddPlayerLine(
                "caravan_hire_camel", "caravan_hire_options", "caravan_hired_npc",
                $"A camel handler. ({CaravanHandBehavior.GetHirePrice(CaravanAnimalType.Camel)} gold, +{CaravanHandBehavior.GetCapacityBonus(CaravanAnimalType.Camel)} capacity, {CaravanHandBehavior.GetDailyUpkeep(CaravanAnimalType.Camel)} gold/day)",
                CanHireMore, () => CaravanHandBehavior.Current?.TryHire(CaravanAnimalType.Camel)
            );

            starter.AddPlayerLine(
                "caravan_hire_cancel", "caravan_hire_options", "close_window",
                "Never mind.",
                null, null
            );

            // Hired NPC confirmation
            starter.AddDialogLine(
                "caravan_hired_npc", "caravan_hired_npc", "close_window",
                "Consider it done. They will follow your caravan.",
                null, null
            );

            // Manage — NPC status line
            starter.AddDialogLine(
                "caravan_manage_npc", "caravan_manage_npc", "caravan_manage_options",
                "{CARAVAN_STATUS_TEXT}",
                SetCaravanStatusText, null
            );

            // Manage options — dismiss by type
            starter.AddPlayerLine(
                "caravan_dismiss_sumpter", "caravan_manage_options", "caravan_dismissed_npc",
                "Release a sumpter horse handler.",
                () => (CaravanHandBehavior.Current?.CountOf(CaravanAnimalType.SumpterHorse) ?? 0) > 0,
                () => CaravanHandBehavior.Current?.TryDismiss(CaravanAnimalType.SumpterHorse)
            );

            starter.AddPlayerLine(
                "caravan_dismiss_mule", "caravan_manage_options", "caravan_dismissed_npc",
                "Release a mule handler.",
                () => (CaravanHandBehavior.Current?.CountOf(CaravanAnimalType.Mule) ?? 0) > 0,
                () => CaravanHandBehavior.Current?.TryDismiss(CaravanAnimalType.Mule)
            );

            starter.AddPlayerLine(
                "caravan_dismiss_camel", "caravan_manage_options", "caravan_dismissed_npc",
                "Release a camel handler.",
                () => (CaravanHandBehavior.Current?.CountOf(CaravanAnimalType.Camel) ?? 0) > 0,
                () => CaravanHandBehavior.Current?.TryDismiss(CaravanAnimalType.Camel)
            );

            starter.AddPlayerLine(
                "caravan_manage_done", "caravan_manage_options", "close_window",
                "That will be all.",
                null, null
            );

            // Dismissed NPC confirmation
            starter.AddDialogLine(
                "caravan_dismissed_npc", "caravan_dismissed_npc", "close_window",
                "Understood. Paid and sent on their way.",
                null, null
            );
        }

        // ---- Conditions ----

        private bool IsHorseTrader()
        {
            var character = CharacterObject.OneToOneConversationCharacter;
            return character?.Occupation == Occupation.HorseTrader;
        }

        private bool CanOpenHireMenu()
        {
            return IsHorseTrader();
        }

        private bool CanHireMore()
        {
            var b = CaravanHandBehavior.Current;
            return b != null && b.TotalHands < 5 && b.TotalHands < b.MaxAllowed;
        }

        private bool HasAnyHands()
        {
            if (!IsHorseTrader()) return false;
            return (CaravanHandBehavior.Current?.TotalHands ?? 0) > 0;
        }

        private bool SetCaravanStatusText()
        {
            var b = CaravanHandBehavior.Current;
            if (b == null) return false;

            int sumpter = b.CountOf(CaravanAnimalType.SumpterHorse);
            int mules = b.CountOf(CaravanAnimalType.Mule);
            int camels = b.CountOf(CaravanAnimalType.Camel);

            var parts = new System.Collections.Generic.List<string>();
            if (sumpter > 0) parts.Add($"{sumpter} sumpter horse{(sumpter > 1 ? "s" : "")}");
            if (mules > 0) parts.Add($"{mules} mule{(mules > 1 ? "s" : "")}");
            if (camels > 0) parts.Add($"{camels} camel{(camels > 1 ? "s" : "")}");

            string roster = string.Join(", ", parts);
            string text = $"You have {b.TotalHands} handler{(b.TotalHands > 1 ? "s" : "")}: {roster}. " +
                          $"Upkeep: {b.TotalDailyUpkeep} gold/day. " +
                          $"Capacity bonus: +{b.TotalCapacityBonus}. " +
                          $"Slot limit: {b.TotalHands}/{b.MaxAllowed} (party size {MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0}/50). " +
                          $"Who would you like to release?";

            MBTextManager.SetTextVariable("CARAVAN_STATUS_TEXT", text);
            return true;
        }

        public override void SyncData(IDataStore dataStore) { }
    }
}