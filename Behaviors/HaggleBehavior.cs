using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using ArtOfTheTrade.Save;
using ArtOfTheTrade.Settings;

namespace ArtOfTheTrade.Behaviors
{
    public enum HaggleOutcome { FullSuccess, CounterOffer, Failure }

    public class MerchantRecord
    {
        public float CooldownEndDay { get; set; } = 0f;
        public int RepScore { get; set; } = 0; // -50 to +50, persists across days
    }

    public class HaggleBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, MerchantRecord> MerchantData_ => ModSaveManager.Data.MerchantData;

        private float _buyModifier = 1f;
        private float _sellModifier = 1f;
        private string _currentMerchantId = null;
        private HaggleOutcome _lastOutcome = HaggleOutcome.Failure;
        private float _pendingCounterDiscount = 0f;
        private int _currentTier = 0;

        public float BuyModifier => _buyModifier;
        public float SellModifier => _sellModifier;
        public float CurrentPriceModifier => _buyModifier;
        public string CurrentMerchantId => _currentMerchantId;
        public HaggleOutcome LastOutcome => _lastOutcome;
        public float PendingCounterDiscount => _pendingCounterDiscount;

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore) { /* handled by ModDataBehavior */ }

        private MerchantRecord GetOrCreate(string id)
        {
            if (!MerchantData_.TryGetValue(id, out var record))
            {
                record = new MerchantRecord();
                MerchantData_[id] = record;
            }
            return record;
        }

        private void OnDailyTick()
        {
            ResetModifier();
        }

        public void ResetModifier()
        {
            _buyModifier = 1f;
            _sellModifier = 1f;
            _currentMerchantId = null;
            _lastOutcome = HaggleOutcome.Failure;
            _pendingCounterDiscount = 0f;
            _currentTier = 0;
        }

        // Builds a unique id for the current conversation merchant.
        // Hero merchants (e.g. village notables) already have a globally-unique StringId.
        // Generic town traders (weaponsmith, armorer, goods trader, horse merchant) are
        // shared CharacterObject templates — the SAME StringId in every town — so we must
        // qualify them with the current settlement, otherwise a cooldown at one town's
        // weaponsmith would lock out every weaponsmith everywhere.
        public static string GetConversationMerchantId()
        {
            var hero = Hero.OneToOneConversationHero;
            if (hero != null) return hero.StringId;

            var character = CharacterObject.OneToOneConversationCharacter;
            if (character == null) return null;

            string settlementId = Settlement.CurrentSettlement?.StringId ?? "";
            return settlementId + ":" + character.StringId;
        }

        public bool CanHaggleWith(Hero merchant, string characterId = null)
        {
            string id = characterId ?? merchant?.StringId;
            if (id == null) return false;
            float today = (float)CampaignTime.Now.ToDays;
            if (MerchantData_.TryGetValue(id, out var record))
                return today >= record.CooldownEndDay;
            return true;
        }

        public int GetRepScore(string merchantId)
        {
            if (merchantId == null) return 0;
            return MerchantData_.TryGetValue(merchantId, out var r) ? r.RepScore : 0;
        }

        public int GetSuccessChance(int tier)
        {
            var player = Hero.MainHero;
            int trade = player.GetSkillValue(DefaultSkills.Trade);
            int charm = player.GetSkillValue(DefaultSkills.Charm);
            int skillBonus = (int)((trade + charm) / 2f / 300f * 25f);

            // Rep bonus from current conversation target (±10)
            string merchantId = GetConversationMerchantId();
            int repBonus = 0;
            if (merchantId != null && MerchantData_.TryGetValue(merchantId, out var record))
                repBonus = record.RepScore / 5;

            // Renown bonus (0 to +5)
            int renownBonus = MBMath.ClampInt((int)((player.Clan?.Renown ?? 0f) / 500f * 5f), 0, 5);

            // Relation bonus (±5)
            int relationBonus = 0;
            var conversationHero = Hero.OneToOneConversationHero;
            if (conversationHero != null)
                relationBonus = MBMath.ClampInt(conversationHero.GetRelation(player) / 10, -5, 5);

            int baseChance = tier switch { 0 => 70, 1 => 45, 2 => 25, _ => 50 };

            // Certificate bonus
            var town = Settlement.CurrentSettlement?.Town;
            int certBonus = ArtOfTradeSettings.Instance?.CertificateHaggleBonus ?? 10;
            if (town != null && TradeRestrictionBehavior.Current?.HasValidCertificate(town) == true)
                baseChance += certBonus;

            // Caravan hand bonus
            int handBonusPerHand = ArtOfTradeSettings.Instance?.HandHaggleBonusPerHand ?? 2;
            int hands = CaravanHandBehavior.Current?.TotalHands ?? 0;
            baseChance += MBMath.ClampInt(hands * handBonusPerHand, 0, 10);

            return MBMath.ClampInt(baseChance + skillBonus + repBonus + renownBonus + relationBonus, 5, 95);
        }

        private float GetSuccessDiscount(int tier) =>
            tier == 0 ? 0.10f : tier == 1 ? 0.20f : 0.35f;

        private void ApplySuccessModifier(int tier)
        {
            float discount = GetSuccessDiscount(tier);
            _buyModifier = 1f - discount;
            _sellModifier = 1f + discount;
        }

        private void AwardXP(float tradeXp, float charmXp)
        {
            Hero.MainHero?.HeroDeveloper?.AddSkillXp(DefaultSkills.Trade, tradeXp);
            Hero.MainHero?.HeroDeveloper?.AddSkillXp(DefaultSkills.Charm, charmXp);
        }

        private void SetCooldown(string id, float days)
        {
            GetOrCreate(id).CooldownEndDay = (float)CampaignTime.Now.ToDays + days;
        }

        public HaggleOutcome AttemptHaggle(Hero merchant, int tier, string characterId = null)
        {
            string id = characterId ?? merchant?.StringId;
            if (id == null) { _lastOutcome = HaggleOutcome.Failure; return _lastOutcome; }
            if (!CanHaggleWith(merchant, id)) { _lastOutcome = HaggleOutcome.Failure; return _lastOutcome; }

            _currentMerchantId = id;
            _currentTier = tier;

            int chance = GetSuccessChance(tier);
            int roll = MBRandom.RandomInt(0, 100);
            AwardXP(15f, 5f); // XP for attempting

            if (roll < (int)(chance * 0.6f))
            {
                // Full success
                ApplySuccessModifier(tier);
                var record = GetOrCreate(id);
                record.RepScore = MBMath.ClampInt(record.RepScore + 3, -50, 50);
                SetCooldown(id, 1f);
                AwardXP(25f, 15f);
                _lastOutcome = HaggleOutcome.FullSuccess;
                int pct = (int)(GetSuccessDiscount(tier) * 100);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Haggle succeeded! Buy -{pct}% / Sell +{pct}%. (Rep: {record.RepScore}/50)", Colors.Green));
            }
            else if (roll < chance)
            {
                // Counter-offer: merchant offers half the discount
                _pendingCounterDiscount = GetSuccessDiscount(tier) / 2f;
                _lastOutcome = HaggleOutcome.CounterOffer;
                // No cooldown set yet — wait for player response
            }
            else
            {
                // Failure
                _buyModifier = 1.25f;
                _sellModifier = 0.75f;
                var record = GetOrCreate(id);
                record.RepScore = MBMath.ClampInt(record.RepScore - 5, -50, 50);
                SetCooldown(id, ArtOfTradeSettings.Instance?.HaggleFailureCooldown ?? 3f);
                _lastOutcome = HaggleOutcome.Failure;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Haggle failed! Prices penalized for 3 days. (Rep: {record.RepScore}/50)", Colors.Red));
            }

            return _lastOutcome;
        }

        // Player accepts the merchant's counter-offer
        public void AcceptCounter()
        {
            _buyModifier = 1f - _pendingCounterDiscount;
            _sellModifier = 1f + _pendingCounterDiscount;
            if (_currentMerchantId != null)
            {
                var record = GetOrCreate(_currentMerchantId);
                record.RepScore = MBMath.ClampInt(record.RepScore + 1, -50, 50);
                SetCooldown(_currentMerchantId, 1f);
            }
            AwardXP(15f, 8f);
            _lastOutcome = HaggleOutcome.FullSuccess;
            int pct = (int)(_pendingCounterDiscount * 100);
            InformationManager.DisplayMessage(new InformationMessage(
                $"Counter accepted. Buy -{pct}% / Sell +{pct}%.", Colors.Green));
        }

        // Player pushes for the full original discount after a counter-offer
        public HaggleOutcome AttemptPushHarder()
        {
            if (_currentMerchantId == null) { _lastOutcome = HaggleOutcome.Failure; return _lastOutcome; }

            int chance = GetSuccessChance(_currentTier) / 2; // halved odds when pushing
            int roll = MBRandom.RandomInt(0, 100);
            AwardXP(10f, 5f);

            if (roll < chance)
            {
                ApplySuccessModifier(_currentTier);
                var record = GetOrCreate(_currentMerchantId);
                record.RepScore = MBMath.ClampInt(record.RepScore + 3, -50, 50);
                SetCooldown(_currentMerchantId, 1f);
                AwardXP(35f, 20f);
                _lastOutcome = HaggleOutcome.FullSuccess;
                int pct = (int)(GetSuccessDiscount(_currentTier) * 100);
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You pushed hard and won the full deal! Buy -{pct}% / Sell +{pct}%.", Colors.Green));
            }
            else
            {
                // Deal collapsed — worse penalty, 7-day cooldown
                _buyModifier = 1.35f;
                _sellModifier = 0.65f;
                var record = GetOrCreate(_currentMerchantId);
                record.RepScore = MBMath.ClampInt(record.RepScore - 10, -50, 50);
                SetCooldown(_currentMerchantId, ArtOfTradeSettings.Instance?.HaggleCollapseCooldown ?? 7f);
                _lastOutcome = HaggleOutcome.Failure;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"The deal collapsed! Locked out for 7 days. (Rep: {record.RepScore}/50)", Colors.Red));
            }

            return _lastOutcome;
        }

        // Player walks away from the counter-offer — small cooldown, no penalty
        public void WalkAway()
        {
            if (_currentMerchantId != null)
                SetCooldown(_currentMerchantId, 1f);
            ResetModifier();
        }

        public static bool IsEnabled => ArtOfTradeSettings.Instance?.EnableHaggle ?? true;

        public static HaggleBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<HaggleBehavior>();
    }
}