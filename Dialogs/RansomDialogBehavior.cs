using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using ArtOfTheTrade.Behaviors;

namespace ArtOfTheTrade.Dialogs
{
    // Renamed from RansomDialogBehavior — handles two pre-battle negotiation scenarios
    // via the encounter game menu:
    //   Scenario 1 (weak): offer gold to avoid capture
    //   Scenario 2 (strong): demand goods back from the lord who previously captured you
    public class CaptureNegotiationBehavior : CampaignBehaviorBase
    {
        private int _pendingRecoveryAmount = 0;

        public static CaptureNegotiationBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<CaptureNegotiationBehavior>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            AddEncounterOptions(starter);
            AddRansomMenus(starter);
            AddDemandMenus(starter);
        }

        private static Hero GetEncounteredLeader() =>
            PlayerEncounter.EncounteredParty?.LeaderHero;

        // ── Hook into the encounter game menu ────────────────────────────────

        private void AddEncounterOptions(CampaignGameStarter starter)
        {
            // Scenario 1: offer payment for safe passage
            starter.AddGameMenuOption(
                "encounter", "aot_pay_ransom",
                "Offer payment for safe passage.",
                args =>
                {
                    var leader = GetEncounteredLeader();
                    if (leader == null || leader.Clan == null || leader.Clan.IsMinorFaction) return false;
                    if (Hero.MainHero.Gold < 100) return false;
                    args.optionLeaveType = GameMenuOption.LeaveType.Trade;
                    return true;
                },
                args =>
                {
                    int gold = Hero.MainHero.Gold;
                    MBTextManager.SetTextVariable("AOT_RANSOM_25", (gold / 4).ToString());
                    MBTextManager.SetTextVariable("AOT_RANSOM_50", (gold / 2).ToString());
                    MBTextManager.SetTextVariable("AOT_RANSOM_75", (gold * 3 / 4).ToString());
                    GameMenu.SwitchToMenu("aot_ransom_offer");
                },
                false, 3, false);

            // Scenario 2: demand return of goods from the lord who captured you
            starter.AddGameMenuOption(
                "encounter", "aot_demand_return",
                "Return what you took from me, or face the consequences.",
                args =>
                {
                    var leader = GetEncounteredLeader();
                    if (leader == null) return false;
                    if (CapturePenaltyBehavior.Current?.HasPendingClaimAgainst(leader) != true) return false;
                    args.optionLeaveType = GameMenuOption.LeaveType.Conversation;
                    return true;
                },
                args =>
                {
                    var leader = GetEncounteredLeader();
                    _pendingRecoveryAmount = CapturePenaltyBehavior.Current?.CalculateLordOffer(leader) ?? 0;
                    MBTextManager.SetTextVariable("AOT_RECOVERY_AMOUNT", _pendingRecoveryAmount.ToString());
                    MBTextManager.SetTextVariable("AOT_CAPTOR_NAME", leader?.Name?.ToString() ?? "He");
                    GameMenu.SwitchToMenu("aot_demand_menu");
                },
                false, 4, false);
        }

        // ── Scenario 1: ransom offer sub-menu ────────────────────────────────

        private void AddRansomMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu("aot_ransom_offer",
                "You approach the enemy commander. \"Name your price for letting me pass unharmed.\"",
                args => { });

            starter.AddGameMenuOption("aot_ransom_offer", "aot_ransom_25",
                "Offer {AOT_RANSOM_25} gold (25% of your wealth).",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Trade; return true; },
                args => { TryPayRansom(25); }, false, 0, false);

            starter.AddGameMenuOption("aot_ransom_offer", "aot_ransom_50",
                "Offer {AOT_RANSOM_50} gold (50% of your wealth).",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Trade; return true; },
                args => { TryPayRansom(50); }, false, 1, false);

            starter.AddGameMenuOption("aot_ransom_offer", "aot_ransom_75",
                "Offer {AOT_RANSOM_75} gold (75% of your wealth).",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Trade; return true; },
                args => { TryPayRansom(75); }, false, 2, false);

            starter.AddGameMenuOption("aot_ransom_offer", "aot_ransom_cancel",
                "Never mind. I'd rather fight.",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                args => { GameMenu.SwitchToMenu("encounter"); }, true, 3, false);

            // Accepted
            starter.AddGameMenu("aot_ransom_success",
                "He considers your offer and nods. \"Take your freedom. And don't let me see you again.\"",
                args => { });

            starter.AddGameMenuOption("aot_ransom_success", "aot_ransom_success_leave",
                "Leave.",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                args => { PlayerEncounter.Finish(); }, true, 0, false);

            // Rejected
            starter.AddGameMenu("aot_ransom_fail",
                "He scoffs. \"Keep your coin. I'll take everything after the battle.\"",
                args => { });

            starter.AddGameMenuOption("aot_ransom_fail", "aot_ransom_fail_back",
                "Then we fight.",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                args => { GameMenu.SwitchToMenu("encounter"); }, true, 0, false);
        }

        // ── Scenario 2: demand return sub-menu ───────────────────────────────

        private void AddDemandMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenu("aot_demand_menu",
                "{AOT_CAPTOR_NAME} shifts uneasily. \"I'll return {AOT_RECOVERY_AMOUNT} gold worth of what I took. That is my offer.\"",
                args => { });

            starter.AddGameMenuOption("aot_demand_menu", "aot_demand_accept",
                "Accept his offer.",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Trade; return true; },
                args =>
                {
                    var leader = GetEncounteredLeader();
                    CapturePenaltyBehavior.Current?.ApplyRecovery(_pendingRecoveryAmount, leader);
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"You recovered {_pendingRecoveryAmount} gold.", Colors.Green));
                    PlayerEncounter.Finish();
                }, false, 0, false);

            starter.AddGameMenuOption("aot_demand_menu", "aot_demand_refuse",
                "Refuse. I'll take it all by force!",
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                args => { GameMenu.SwitchToMenu("encounter"); }, true, 1, false);
        }

        private void TryPayRansom(int pct)
        {
            int gold = Hero.MainHero.Gold;
            int amount = gold * pct / 100;
            if (amount <= 0) { GameMenu.SwitchToMenu("encounter"); return; }

            var leader = GetEncounteredLeader();

            float baseChance = pct == 25 ? 0.30f : pct == 50 ? 0.55f : 0.75f;
            int charm = Hero.MainHero.GetSkillValue(DefaultSkills.Charm);
            int trade = Hero.MainHero.GetSkillValue(DefaultSkills.Trade);
            float skillBonus = (charm + trade) / 2f / 300f * 0.20f;
            float chance = baseChance + skillBonus;

            if (MBRandom.RandomFloat < chance)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, leader, amount, true);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You paid {amount} gold for safe passage.", Colors.Green));
                GameMenu.SwitchToMenu("aot_ransom_success");
            }
            else
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Your offer was rejected.", Colors.Red));
                GameMenu.SwitchToMenu("aot_ransom_fail");
            }
        }
    }
}
