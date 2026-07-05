using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using ArtOfTheTrade.Behaviors;
using ArtOfTheTrade.Settings;

namespace ArtOfTheTrade.Patches
{
    [HarmonyPatch(typeof(DefaultTradeItemPriceFactorModel), nameof(DefaultTradeItemPriceFactorModel.GetPrice),
        new System.Type[] {
            typeof(EquipmentElement),
            typeof(MobileParty),
            typeof(PartyBase),
            typeof(bool),
            typeof(float),
            typeof(float),
            typeof(float)
        })]
    public class TradePatch
    {
        private static string _lastWarnedSettlement = null;

        [HarmonyPostfix]
        public static void Postfix(
            EquipmentElement itemRosterElement,
            MobileParty clientParty,
            PartyBase merchant,
            bool isSelling,
            float inStoreValue,
            float supply,
            float demand,
            ref int __result)
        {
            try
            {
                if (Campaign.Current == null) return;
                if (Hero.MainHero == null) return;
                if (MobileParty.MainParty == null) return;

                var playerParty = MobileParty.MainParty;
                if (clientParty != playerParty && merchant?.MobileParty != playerParty) return;

                var settlement = merchant?.Settlement ?? clientParty?.CurrentSettlement;
                if (settlement == null) return;

                // Apply trade restriction penalty — towns only
                var s = ArtOfTradeSettings.Instance;
                if (settlement.IsTown && settlement.Town != null && (s?.EnableTradeRestrictions ?? true))
                {
                    var behavior = TradeRestrictionBehavior.Current;
                    if (behavior != null && !behavior.CanPlayerTradeAt(settlement.Town))
                    {
                        float buyMult  = s?.BuyPenaltyMultiplier  ?? 5f;
                        float sellMult = s?.SellPenaltyMultiplier ?? 0.5f;

                        if (isSelling)
                            __result = (int)(__result * sellMult);
                        else
                            __result = (int)(__result * buyMult);

                        if (!isSelling && _lastWarnedSettlement != settlement.StringId)
                        {
                            _lastWarnedSettlement = settlement.StringId;
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"No trade deal or certificate for {settlement.Name}. Buy prices are {buyMult}×, sell prices {sellMult}×.", Colors.Red));
                        }
                    }
                    else
                    {
                        _lastWarnedSettlement = null;
                    }
                }

                // Apply haggle modifier — towns and villages
                // isSelling = true means the PLAYER is selling → apply SellModifier (1 + discount)
                // isSelling = false means the PLAYER is buying → apply BuyModifier (1 - discount)
                var haggle = HaggleBehavior.Current;
                if (haggle != null)
                {
                    if (isSelling && haggle.SellModifier != 1f)
                        __result = (int)(__result * haggle.SellModifier);
                    else if (!isSelling && haggle.BuyModifier != 1f)
                        __result = (int)(__result * haggle.BuyModifier);
                }
            }
            catch { }
        }
    }
}


