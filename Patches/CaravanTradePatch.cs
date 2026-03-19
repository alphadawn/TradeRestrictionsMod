using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace ArtOfTheTrade.Patches
{
    [HarmonyPatch(typeof(EnterSettlementAction), "ApplyForParty")]
    public class CaravanTradePatch
    {
        [HarmonyPrefix]
        public static bool Prefix(MobileParty mobileParty, Settlement settlement)
        {
            try
            {
                if (mobileParty == null || settlement == null) return true;
                if (!settlement.IsTown) return true;
                if (!mobileParty.IsCaravan) return true;
                if (mobileParty == MobileParty.MainParty) return true;
                if (Campaign.Current == null) return true;

                var caravanFaction = mobileParty.ActualClan?.MapFaction
                                  ?? mobileParty.LeaderHero?.MapFaction;
                var townFaction = settlement.MapFaction;

                if (caravanFaction == null || townFaction == null) return true;
                if (caravanFaction == townFaction) return true;
                if (caravanFaction.IsAtWarWith(townFaction)) return true;

                var tradeAgreements = Campaign.Current
                    .GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
                var kingdomA = caravanFaction as Kingdom;
                var kingdomB = townFaction as Kingdom;

                bool hasDeal = kingdomA != null && kingdomB != null
                    && tradeAgreements != null
                    && tradeAgreements.HasTradeAgreement(kingdomA, kingdomB);

                if (!hasDeal)
                {
                    var homeComp = mobileParty.PartyComponent as CaravanPartyComponent;
                    var home = homeComp?.HomeSettlement ?? mobileParty.HomeSettlement;
                    if (home != null)
                        mobileParty.SetMoveGoToSettlement(home, MobileParty.NavigationType.All, false);

                    InformationManager.DisplayMessage(new InformationMessage(
                        $"{mobileParty.Name} blocked from {settlement.Name} — no trade agreement.",
                        Colors.Yellow));

                    return false;
                }

                return true;
            }
            catch { return true; }
        }
    }
}

