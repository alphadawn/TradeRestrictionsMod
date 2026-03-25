using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using ArtOfTheTrade.Models;
using ArtOfTheTrade.Save;
using ArtOfTheTrade.Settings;

namespace ArtOfTheTrade.Behaviors
{
    public class CaravanHandBehavior : CampaignBehaviorBase
    {
        // Delegates directly to ModSaveManager.Data so data is always in sync with the JSON file
        private List<CaravanHand> Hands_ => ModSaveManager.Data.CaravanHands;

        public IReadOnlyList<CaravanHand> Hands => Hands_.AsReadOnly();
        public int TotalHands => Hands_.Count;

        public int MaxAllowed
        {
            get
            {
                int cap = ArtOfTradeSettings.Instance?.MaxCaravanHands ?? 5;
                int partySize = MobileParty.MainParty?.MemberRoster?.TotalManCount ?? 0;
                return System.Math.Min(cap, partySize / 10);
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

        public int TotalDailyUpkeep => Hands_.Sum(h => GetDailyUpkeep(h.AnimalType));
        public int TotalCapacityBonus => Hands_.Sum(h => GetCapacityBonus(h.AnimalType));
        public int CountOf(CaravanAnimalType type) => Hands_.Count(h => h.AnimalType == type);

        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore) { /* handled by ModDataBehavior */ }

        public bool TryHire(CaravanAnimalType type)
        {
            if (!(ArtOfTradeSettings.Instance?.EnableCaravanHands ?? true))
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "Caravan hands are disabled in the mod settings.", Colors.Red));
                return false;
            }
            int cap = ArtOfTradeSettings.Instance?.MaxCaravanHands ?? 5;
            if (Hands_.Count >= cap)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You cannot have more than {cap} caravan hands.", Colors.Red));
                return false;
            }
            if (Hands_.Count >= MaxAllowed)
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

            Hands_.Add(new CaravanHand { AnimalType = type, OutfitIndex = MBRandom.RandomInt(0, 3) });

            InformationManager.DisplayMessage(new InformationMessage(
                $"Hired a {GetAnimalName(type)} handler. (+{GetCapacityBonus(type)} carry capacity, {GetDailyUpkeep(type)} gold/day)",
                Colors.Green));
            return true;
        }

        public bool TryDismiss(CaravanAnimalType type)
        {
            var hand = Hands_.FirstOrDefault(h => h.AnimalType == type);
            if (hand == null) return false;

            Hands_.Remove(hand);

            var item = MBObjectManager.Instance.GetObject<ItemObject>(GetItemId(type));
            if (item != null)
                MobileParty.MainParty?.ItemRoster?.AddToCounts(item, -1);

            InformationManager.DisplayMessage(new InformationMessage(
                $"Dismissed the {GetAnimalName(type)} handler.", Colors.Yellow));
            return true;
        }

        private void OnDailyTick()
        {
            if (Hands_.Count == 0) return;

            SyncWithRoster();

            if (Hands_.Count == 0) return;

            int totalUpkeep = Hands_.Sum(h => GetDailyUpkeep(h.AnimalType));
            if (Hero.MainHero.Gold >= totalUpkeep)
            {
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, totalUpkeep, true);
            }
            else
            {
                var costliest = Hands_.OrderByDescending(h => GetDailyUpkeep(h.AnimalType)).First();
                Hands_.Remove(costliest);

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
                while (Hands_.Count(h => h.AnimalType == type) > inRoster)
                    Hands_.Remove(Hands_.First(h => h.AnimalType == type));
            }
        }

        // Called on capture — animals scatter, no refund
        public void DismissAll()
        {
            if (Hands_.Count == 0) return;

            foreach (CaravanAnimalType type in System.Enum.GetValues(typeof(CaravanAnimalType)))
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(GetItemId(type));
                if (item == null) continue;
                int count = Hands_.Count(h => h.AnimalType == type);
                if (count > 0)
                    MobileParty.MainParty?.ItemRoster?.AddToCounts(item, -count);
            }

            int total = Hands_.Count;
            Hands_.Clear();

            InformationManager.DisplayMessage(new InformationMessage(
                $"Your {total} caravan hand(s) scattered when you were captured.", Colors.Yellow));
        }

        public static bool IsEnabled => ArtOfTradeSettings.Instance?.EnableCaravanHands ?? true;

        public static CaravanHandBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<CaravanHandBehavior>();
    }
}