using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ArtOfTheTrade.Behaviors;

namespace ArtOfTheTrade.Dialogs
{
    public class StashMenuBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddMenus(starter);
        }

        public void AddMenus(CampaignGameStarter starter)
        {
            // Add to town menu
            starter.AddGameMenuOption(
                "town", "town_gold_stash",
                "Manage gold stash",
                TownStashCondition,
                x => GameMenu.SwitchToMenu("gold_stash_menu"),
                false, 5, false
            );

            // Add to castle menu
            starter.AddGameMenuOption(
                "castle", "castle_gold_stash",
                "Manage gold stash",
                CastleStashCondition,
                x => GameMenu.SwitchToMenu("gold_stash_menu"),
                false, 5, false
            );

            // Main stash menu
            starter.AddGameMenu(
                "gold_stash_menu",
                "{STASH_STATUS}",
                StashMenuInit,
                GameMenu.MenuOverlayType.None,
                GameMenu.MenuFlags.None
            );

            starter.AddGameMenuOption(
                "gold_stash_menu", "stash_deposit",
                "Deposit gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return Hero.MainHero.Gold > 0; },
                x => GameMenu.SwitchToMenu("gold_stash_deposit"),
                false, 0, false
            );

            starter.AddGameMenuOption(
                "gold_stash_menu", "stash_withdraw",
                "Withdraw gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) > 0; },
                x => GameMenu.SwitchToMenu("gold_stash_withdraw"),
                false, 1, false
            );

            starter.AddGameMenuOption(
                "gold_stash_menu", "stash_leave",
                "Leave",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                x => GameMenu.SwitchToMenu(Settlement.CurrentSettlement?.IsTown == true ? "town" : "castle"),
                true, 2, false
            );

            // Deposit menu
            starter.AddGameMenu(
                "gold_stash_deposit",
                "{DEPOSIT_TEXT}",
                x => MBTextManager.SetTextVariable("DEPOSIT_TEXT", $"You have {Hero.MainHero.Gold} gold. How much to deposit?"),
                GameMenu.MenuOverlayType.None,
                GameMenu.MenuFlags.None
            );

            starter.AddGameMenuOption("gold_stash_deposit", "deposit_100", "Deposit 100 gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return Hero.MainHero.Gold >= 100; },
                x => { StashBehavior.Current?.DepositGold(Settlement.CurrentSettlement, 100); GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 0, false);

            starter.AddGameMenuOption("gold_stash_deposit", "deposit_1000", "Deposit 1,000 gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return Hero.MainHero.Gold >= 1000; },
                x => { StashBehavior.Current?.DepositGold(Settlement.CurrentSettlement, 1000); GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 1, false);

            starter.AddGameMenuOption("gold_stash_deposit", "deposit_10000", "Deposit 10,000 gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return Hero.MainHero.Gold >= 10000; },
                x => { StashBehavior.Current?.DepositGold(Settlement.CurrentSettlement, 10000); GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 2, false);

            starter.AddGameMenuOption("gold_stash_deposit", "deposit_all", "Deposit all gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return Hero.MainHero.Gold > 0; },
                x => { StashBehavior.Current?.DepositGold(Settlement.CurrentSettlement, Hero.MainHero.Gold); GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 3, false);

            starter.AddGameMenuOption("gold_stash_deposit", "deposit_back", "Back",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                x => GameMenu.SwitchToMenu("gold_stash_menu"), true, 4, false);

            // Withdraw menu
            starter.AddGameMenu(
                "gold_stash_withdraw",
                "{WITHDRAW_TEXT}",
                x => {
                    var stash = StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement);
                    MBTextManager.SetTextVariable("WITHDRAW_TEXT", $"Stash contains {stash?.StoredGold ?? 0} gold. How much to withdraw?");
                },
                GameMenu.MenuOverlayType.None,
                GameMenu.MenuFlags.None
            );

            starter.AddGameMenuOption("gold_stash_withdraw", "withdraw_100", "Withdraw 100 gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) >= 100; },
                x => { StashBehavior.Current?.WithdrawGold(Settlement.CurrentSettlement, 100); GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 0, false);

            starter.AddGameMenuOption("gold_stash_withdraw", "withdraw_1000", "Withdraw 1,000 gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) >= 1000; },
                x => { StashBehavior.Current?.WithdrawGold(Settlement.CurrentSettlement, 1000); GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 1, false);

            starter.AddGameMenuOption("gold_stash_withdraw", "withdraw_10000", "Withdraw 10,000 gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) >= 10000; },
                x => { StashBehavior.Current?.WithdrawGold(Settlement.CurrentSettlement, 10000); GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 2, false);

            starter.AddGameMenuOption("gold_stash_withdraw", "withdraw_all", "Withdraw all gold",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Continue; return (StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement)?.StoredGold ?? 0) > 0; },
                x => { var stash = StashBehavior.Current?.GetOrCreateStash(Settlement.CurrentSettlement); if (stash != null) { StashBehavior.Current?.WithdrawGold(Settlement.CurrentSettlement, stash.StoredGold); } GameMenu.SwitchToMenu("gold_stash_menu"); }, false, 3, false);

            starter.AddGameMenuOption("gold_stash_withdraw", "withdraw_back", "Back",
                x => { x.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                x => GameMenu.SwitchToMenu("gold_stash_menu"), true, 4, false);
        }

        private bool TownStashCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown;
        }

        private bool CastleStashCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsCastle;
        }

        private void StashMenuInit(MenuCallbackArgs args)
        {
            var settlement = Settlement.CurrentSettlement;
            var stash = StashBehavior.Current?.GetOrCreateStash(settlement);
            MBTextManager.SetTextVariable("STASH_STATUS",
                $"Your stash at {settlement?.Name} contains {stash?.StoredGold ?? 0} gold.");
        }
    }
}

