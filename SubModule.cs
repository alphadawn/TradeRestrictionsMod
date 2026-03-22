using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using ArtOfTheTrade.Behaviors;
using TaleWorlds.CampaignSystem.Settlements;
using ArtOfTheTrade.Dialogs;

namespace ArtOfTheTrade
{
    public class SubModule : MBSubModuleBase
    {
        private readonly Harmony _harmony = new Harmony("mod.traderestrictions");

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            _harmony.PatchAll();
            
        }

        private void OnMissionStarted(IMission mission)
        {
            var m = mission as Mission;
            if (m == null) return;

            // Only add to town and castle missions
            if (m.HasMissionBehavior<ArtOfTheTrade.Missions.PackAnimalMissionBehavior>()) return;

            var settlement = Settlement.CurrentSettlement;
            if (settlement == null) return;
            if (!settlement.IsTown && !settlement.IsCastle) return;

            m.AddMissionBehavior(new ArtOfTheTrade.Missions.PackAnimalMissionBehavior());
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (gameStarter is CampaignGameStarter campaignStarter)
            {
                // ModDataBehavior MUST be first — its SyncData loads the external JSON
                // before any other behavior touches ModSaveManager.Data
                campaignStarter.AddBehavior(new ModDataBehavior());

                var certDialog = new CertificateDialogBehavior();
                campaignStarter.AddBehavior(new TradeRestrictionBehavior());
                campaignStarter.AddBehavior(new StashBehavior());
                campaignStarter.AddBehavior(new CapturePenaltyBehavior());
                campaignStarter.AddBehavior(new HaggleBehavior());
                campaignStarter.AddBehavior(certDialog);
                campaignStarter.AddBehavior(new StashMenuBehavior());
                campaignStarter.AddBehavior(new CaravanHandBehavior());
                campaignStarter.AddBehavior(new CaravanHandDialogBehavior());
                campaignStarter.AddBehavior(new MarketIntelDialogBehavior());
                campaignStarter.AddBehavior(new CaptureNegotiationBehavior());
            }
        }
    }
}









