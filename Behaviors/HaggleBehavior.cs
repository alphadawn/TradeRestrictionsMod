using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace ArtOfTheTrade.Behaviors
{
    public class HaggleBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, float> _haggleHistory = new Dictionary<string, float>();

        private float _buyModifier = 1f;
        private float _sellModifier = 1f;
        private string _currentMerchantId = null;

        public float BuyModifier => _buyModifier;
        public float SellModifier => _sellModifier;
        public float CurrentPriceModifier => _buyModifier;
        public string CurrentMerchantId => _currentMerchantId;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_haggleHistory);
                dataStore.SyncData("ArtOfTheTrade_HaggleHistory", ref json);
            }
            if (dataStore.IsLoading)
            {
                var json = "";
                if (dataStore.SyncData("ArtOfTheTrade_HaggleHistory", ref json) && !string.IsNullOrEmpty(json))
                    _haggleHistory = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, float>>(json)
                        ?? new System.Collections.Generic.Dictionary<string, float>();
            }
        }

        private void OnDailyTick()
        {
            _haggleHistory.Clear();
            ResetModifier();
        }

        public void ResetModifier()
        {
            _buyModifier = 1f;
            _sellModifier = 1f;
            _currentMerchantId = null;
        }

        public bool CanHaggleWith(Hero merchant, string characterId = null)
        {
            string id = characterId ?? merchant?.StringId;
            if (id == null) return false;
            float today = (float)CampaignTime.Now.ToDays;
            if (_haggleHistory.TryGetValue(id, out float lastDay))
                return (int)lastDay != (int)today;
            return true;
        }

        public int GetSuccessChance(int tier)
        {
            var player = Hero.MainHero;
            int trade = player.GetSkillValue(DefaultSkills.Trade);
            int charm = player.GetSkillValue(DefaultSkills.Charm);
            int skillBonus = (int)((trade + charm) / 2f / 300f * 25f);

            return tier switch
            {
                0 => 70 + skillBonus,
                1 => 45 + skillBonus,
                2 => 25 + skillBonus,
                _ => 50
            };
        }

        public void AttemptHaggle(Hero merchant, int tier, string characterId = null)
        {
            string id = characterId ?? merchant?.StringId;
            if (id == null) return;
            if (!CanHaggleWith(merchant, id)) return;

            _haggleHistory[id] = (float)CampaignTime.Now.ToDays;
            _currentMerchantId = id;

            int chance = GetSuccessChance(tier);
            int roll = MBRandom.RandomInt(0, 100);
            bool success = roll < chance;

            string tierName = tier == 0 ? "Safe" : tier == 1 ? "Moderate" : "Bold";

            if (success)
            {
                // Tier 0 Safe:     buy -10%, sell +10%
                // Tier 1 Moderate: buy -20%, sell +20%
                // Tier 2 Bold:     buy -35%, sell +35%
                _buyModifier  = tier == 0 ? 0.90f : tier == 1 ? 0.80f : 0.65f;
                _sellModifier = tier == 0 ? 1.10f : tier == 1 ? 1.20f : 1.35f;
                int pct = (int)((1f - _buyModifier) * 100);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Haggle succeeded! ({tierName}, {roll} vs {chance}) Buy -{pct}% / Sell +{pct}%.",
                    Colors.Green));
            }
            else
            {
                _buyModifier  = 1.25f;
                _sellModifier = 0.75f;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Haggle failed! ({tierName}, {roll} vs {chance}) Buy +25% / Sell -25%.",
                    Colors.Red));
            }
        }

        public static HaggleBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<HaggleBehavior>();
    }
}

