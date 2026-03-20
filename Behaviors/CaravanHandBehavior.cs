using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using ArtOfTheTrade.Models;

namespace ArtOfTheTrade.Behaviors
{
    public class CaravanHandBehavior : CampaignBehaviorBase
    {
        private List<CaravanHand> _hands = new List<CaravanHand>();

        public IReadOnlyList<CaravanHand> Hands => _hands.AsReadOnly();
        public int TotalHands => _hands.Count;

        public int MaxAllowed
        {
            get
            {
                int partySize = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
                return System.Math.Min(5, partySize / 10);
            }
        }

        public static int GetHirePrice(CaravanAnimalType type) => type switch
        {
            CaravanAnimalType.SumpterHorse => 50,
            CaravanAnimalType.Mule => 100,
            CaravanAnimalType.Camel => 300,
            _ => 100
        };

        public static int GetDailyUpkeep(CaravanAnimalType type) => type switch
        {
            CaravanAnimalType.SumpterHorse => 3,
            CaravanAnimalType.Mule => 5,
            CaravanAnimalType.Camel => 10,
            _ => 5
        };

        public static int GetCapacityBonus(CaravanAnimalType type) => type switch
        {
            CaravanAnimalType.SumpterHorse => 30,
            CaravanAnimalType.Mule => 50,
            CaravanAnimalType.Camel => 100,
            _ => 50
        };

        public static string GetItemId(CaravanAnimalType type) => type switch
        {
            CaravanAnimalType.SumpterHorse => "sumpter_horse",
            CaravanAnimalType.Mule => "mule",
            CaravanAnimalType.Camel => "camel",
            _ => "mule"
        };

        public static string GetAnimalName(CaravanAnimalType type) => type switch
        {
            CaravanAnimalType.SumpterHorse => "sumpter horse",
            CaravanAnimalType.Mule => "mule",
            CaravanAnimalType.Camel => "camel",
            _ => "mule"
        };

        public int TotalDailyUpkeep => _hands.Sum(h => GetDailyUpkeep(h.AnimalType));
        public int TotalCapacityBonus => _hands.Sum(h => GetCapacityBonus(h.AnimalType));
        public int CountOf(CaravanAnimalType type) => _hands.Count(h => h.AnimalType == type);

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_hands);
                dataStore.SyncData("ArtOfTheTrade_CaravanHands", ref json);
            }
            if (dataStore.IsLoading)
            {
                var json = "";
                if (dataStore.SyncData("ArtOfTheTrade_CaravanHands", ref json) && !string.IsNullOrEmpty(json))
                    _hands = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CaravanHand>>(json)
                        ?? new List<CaravanHand>();
            }
        }

        public bool TryHire(CaravanAnimalType type)
        {
            if (_hands.Count >= 5)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "You cannot have more than 5 caravan hands.", Colors.Red));
                return false;
            }
            if (_hands.Count >= MaxAllowed)
            {
                string msg = MaxAllowed == 0
                    ? "You need at least 10 party members before you can manage caravan hands."
                    : $"Your party can only manage {MaxAllowed} caravan hands right now. Grow your party to hire more.";
                InformationManager.DisplayMessage(new InformationMessage(msg, Colors.Red));
                return false;
            }

            int price = GetHirePrice(type);
            if (Hero.MainHero.Gold < price)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You need {price} gold to hire a {GetAnimalName(type)} handler.", Colors.Red));
                return false;
            }

            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, price, true);

            var item = MBObjectManager.Instance.GetObject<ItemObject>(GetItemId(type));
            if (item != null)
                MobileParty.MainParty?.ItemRoster?.AddToCounts(item, 1);

            _hands.Add(new CaravanHand { AnimalType = type, OutfitIndex = MBRandom.RandomInt(0, 3) });

            InformationManager.DisplayMessage(new InformationMessage(
                $"Hired a {GetAnimalName(type)} handler. (+{GetCapacityBonus(type)} carry capacity, {GetDailyUpkeep(type)} gold/day)",
                Colors.Green));
            return true;
        }

        public bool TryDismiss(CaravanAnimalType type)
        {
            var hand = _hands.FirstOrDefault(h => h.AnimalType == type);
            if (hand == null) return false;

            _hands.Remove(hand);

            var item = MBObjectManager.Instance.GetObject<ItemObject>(GetItemId(type));
            if (item != null)
                MobileParty.MainParty?.ItemRoster?.AddToCounts(item, -1);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Dismissed the {GetAnimalName(type)} handler.", Colors.Yellow));
            return true;
        }

        private void OnDailyTick()
        {
            if (_hands.Count == 0) return;

            SyncWithRoster();

            if (_hands.Count == 0) return;

            int totalUpkeep = _hands.Sum(h => GetDailyUpkeep(h.AnimalType));
            if (Hero.MainHero.Gold >= totalUpkeep)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, totalUpkeep, true);
            }
            else
            {
                var costliest = _hands.OrderByDescending(h => GetDailyUpkeep(h.AnimalType)).First();
                _hands.Remove(costliest);

                var item = MBObjectManager.Instance.GetObject<ItemObject>(GetItemId(costliest.AnimalType));
                if (item != null)
                    MobileParty.MainParty?.ItemRoster?.AddToCounts(item, -1);

                InformationManager.DisplayMessage(new InformationMessage(
                    $"You could not afford upkeep — your {GetAnimalName(costliest.AnimalType)} handler has left.",
                    Colors.Red));
            }
        }

        private void SyncWithRoster()
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return;

            foreach (CaravanAnimalType type in System.Enum.GetValues(typeof(CaravanAnimalType)))
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(GetItemId(type));
                if (item == null) continue;

                int inRoster = roster.GetItemNumber(item);
                while (_hands.Count(h => h.AnimalType == type) > inRoster)
                    _hands.Remove(_hands.First(h => h.AnimalType == type));
            }
        }

        // Called on capture — animals scatter, no refund
        public void DismissAll()
        {
            if (_hands.Count == 0) return;

            foreach (CaravanAnimalType type in System.Enum.GetValues(typeof(CaravanAnimalType)))
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(GetItemId(type));
                if (item == null) continue;
                int count = _hands.Count(h => h.AnimalType == type);
                if (count > 0)
                    MobileParty.MainParty?.ItemRoster?.AddToCounts(item, -count);
            }

            int total = _hands.Count;
            _hands.Clear();

            InformationManager.DisplayMessage(new InformationMessage(
                $"Your {total} caravan hand(s) scattered when you were captured.", Colors.Yellow));
        }

        public static CaravanHandBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<CaravanHandBehavior>();
    }
}