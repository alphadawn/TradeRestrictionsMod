using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.CampaignSystem;
using ArtOfTheTrade.Behaviors;
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

        protected override void OnGameStart(Game game, IGameStarter gameStarter)
        {
            base.OnGameStart(game, gameStarter);
            if (gameStarter is CampaignGameStarter campaignStarter)
            {
                var certDialog = new CertificateDialogBehavior();
                campaignStarter.AddBehavior(new TradeRestrictionBehavior());
                campaignStarter.AddBehavior(new StashBehavior());
                campaignStarter.AddBehavior(new CapturePenaltyBehavior());
                campaignStarter.AddBehavior(new HaggleBehavior());
                campaignStarter.AddBehavior(certDialog);
                campaignStarter.AddBehavior(new StashMenuBehavior());
            }
        }
    }
}





