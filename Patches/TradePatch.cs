using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using ArtOfTheTrade.Behaviors;

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
                if (settlement.IsTown && settlement.Town != null)
                {
                    var behavior = TradeRestrictionBehavior.Current;
                    if (behavior != null && !behavior.CanPlayerTradeAt(settlement.Town))
                    {
                        if (isSelling)
                            __result = (int)(__result * 0.5f);
                        else
                            __result = __result * 5;

                        if (!isSelling && _lastWarnedSettlement != settlement.StringId)
                        {
                            _lastWarnedSettlement = settlement.StringId;
                            InformationManager.DisplayMessage(new InformationMessage(
                                $"No trade deal or certificate for {settlement.Name}. Buy prices are 5x, sell prices halved.", Colors.Red));
                        }
                    }
                    else
                    {
                        _lastWarnedSettlement = null;
                    }
                }

                // Apply haggle modifier — towns and villages
                // isSelling = true means the merchant/settlement is selling (player is BUYING)
                // isSelling = false means the merchant/settlement is buying (player is SELLING)
                var haggle = HaggleBehavior.Current;
                if (haggle != null)
                {
                    if (isSelling && haggle.BuyModifier != 1f)
                        __result = (int)(__result * haggle.BuyModifier);
                    else if (!isSelling && haggle.SellModifier != 1f)
                        __result = (int)(__result * haggle.SellModifier);
                }
            }
            catch { }
        }
    }
}


