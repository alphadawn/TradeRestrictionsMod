using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ArtOfTheTrade.Behaviors;

namespace ArtOfTheTrade.Dialogs
{
    public class RansomDialogBehavior : CampaignBehaviorBase
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

        private void AddDialogs(CampaignGameStarter starter)
        {
            // Entry — visible when talking to the lord who captured you
            starter.AddPlayerLine(
                "ransom_ask_entry", "hero_main_options", "ransom_ask_npc",
                "I would like to negotiate the return of some of my property.",
                CanAskRansom, null
            );

            // Lord response
            starter.AddDialogLine(
                "ransom_lord_response", "ransom_ask_npc", "ransom_player_choice",
                "You have some nerve asking that. But I am not unreasonable. Make your case — and make it convincing.",
                null, null
            );

            // Player choices
            starter.AddPlayerLine(
                "ransom_attempt_persuade", "ransom_player_choice", "ransom_result_npc",
                "[Persuade] You know this arrangement benefits us both. A gesture of good faith opens doors for future dealings.",
                () => CapturePenaltyBehavior.Current?.AttemptsLeft > 0,
                () => CapturePenaltyBehavior.Current?.DoRansomAttempt()
            );

            starter.AddPlayerLine(
                "ransom_give_up", "ransom_player_choice", "lord_pretalk",
                "Never mind. Forget I asked.",
                null, null
            );

            // Result — text set during DoRansomAttempt
            starter.AddDialogLine(
                "ransom_result_line", "ransom_result_npc", "lord_pretalk",
                "{RANSOM_RESULT_TEXT}",
                SetRansomResultText, null
            );
        }

        private bool CanAskRansom()
        {
            if (!Hero.MainHero.IsPrisoner) return false;
            var captor = Hero.OneToOneConversationHero;
            if (captor == null) return false;
            return CapturePenaltyBehavior.Current?.CanNegotiateRansom(captor) == true;
        }

        private bool SetRansomResultText()
        {
            MBTextManager.SetTextVariable("RANSOM_RESULT_TEXT",
                CapturePenaltyBehavior.Current?.LastRansomResultText ?? "...");
            return true;
        }
    }
}