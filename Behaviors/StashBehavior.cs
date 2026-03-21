using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using ArtOfTheTrade.Models;

namespace ArtOfTheTrade.Behaviors
{
    public class StashBehavior : CampaignBehaviorBase
    {
        private Dictionary<string, Stash> _stashes = new Dictionary<string, Stash>();

        public override void RegisterEvents()
        {
            CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(
                this, OnSettlementOwnerChanged
            );
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_stashes);
                dataStore.SyncData("ArtOfTheTrade_Stashes", ref json);
            }
            if (dataStore.IsLoading)
            {
                var json = "";
                if (dataStore.SyncData("ArtOfTheTrade_Stashes", ref json) && !string.IsNullOrEmpty(json))
                    _stashes = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, ArtOfTheTrade.Models.Stash>>(json)
                        ?? new System.Collections.Generic.Dictionary<string, ArtOfTheTrade.Models.Stash>();
            }
        }

        public bool HasStashAt(Settlement settlement)
        {
            return _stashes.ContainsKey(settlement.StringId);
        }

        public Stash GetOrCreateStash(Settlement settlement)
        {
            if (!_stashes.ContainsKey(settlement.StringId))
                _stashes[settlement.StringId] = new Stash(settlement.StringId);
            return _stashes[settlement.StringId];
        }

        // Any town qualifies (via tavernkeeper), free if player owns/has workshop there
        public bool CanPlayerStashAt(Settlement settlement) =>
            settlement != null && settlement.IsTown;

        // Free access: player owns the town or has a workshop there
        public bool IsFreeStashAt(Settlement settlement)
        {
            if (settlement == null) return false;
            var player = Hero.MainHero;
            if (settlement.IsCastle && settlement.OwnerClan == player.Clan) return true;
            if (settlement.IsTown && settlement.Town.Workshops.Any(w => w.Owner == player)) return true;
            return false;
        }

        // Returns true and charges 50g if needed; returns false if player can't afford
        public bool TryChargeAccessFee(Settlement settlement)
        {
            if (IsFreeStashAt(settlement)) return true;
            const int Fee = 50;
            if (Hero.MainHero.Gold < Fee)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"You need {Fee} gold to access your stash here.", Colors.Red));
                return false;
            }
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, Fee, true);
            return true;
        }

        public void DepositGold(Settlement settlement, int amount)
        {
            var player = Hero.MainHero;

            if (amount <= 0 || player.Gold < amount)
            {
                InformationManager.DisplayMessage(new InformationMessage("Invalid deposit amount."));
                return;
            }

            GiveGoldAction.ApplyForCharacterToSettlement(player, settlement, amount);
            GetOrCreateStash(settlement).StoredGold += amount;
            InformationManager.DisplayMessage(
                new InformationMessage($"Deposited {amount} gold into stash at {settlement.Name}.")
            );
        }

        public void WithdrawGold(Settlement settlement, int amount)
        {
            var stash = GetOrCreateStash(settlement);

            if (amount <= 0 || stash.StoredGold < amount)
            {
                InformationManager.DisplayMessage(new InformationMessage("Not enough gold in stash."));
                return;
            }

            stash.StoredGold -= amount;
            GiveGoldAction.ApplyForSettlementToCharacter(settlement, Hero.MainHero, amount);
            InformationManager.DisplayMessage(
                new InformationMessage($"Withdrew {amount} gold from stash at {settlement.Name}.")
            );
        }

        public void DepositItem(Settlement settlement, ItemObject item, int count)
        {
            MobileParty.MainParty.ItemRoster.AddToCounts(item, -count);
            var stash = GetOrCreateStash(settlement);
            var existing = stash.StoredItems.FirstOrDefault(i => i.ItemId == item.StringId);

            if (existing != null)
                existing.Count += count;
            else
                stash.StoredItems.Add(new StoredItem(item.StringId, count));

            InformationManager.DisplayMessage(
                new InformationMessage($"Stored {count}x {item.Name} in stash at {settlement.Name}.")
            );
        }

        public void WithdrawItem(Settlement settlement, string itemId, int count)
        {
            var stash = GetOrCreateStash(settlement);
            var stored = stash.StoredItems.FirstOrDefault(i => i.ItemId == itemId);

            if (stored == null || stored.Count < count)
            {
                InformationManager.DisplayMessage(new InformationMessage("Item not found or insufficient count."));
                return;
            }

            var item = Game.Current.ObjectManager.GetObject<ItemObject>(itemId);
            if (item == null) return;

            stored.Count -= count;
            if (stored.Count <= 0) stash.StoredItems.Remove(stored);

            MobileParty.MainParty.ItemRoster.AddToCounts(item, count);
            InformationManager.DisplayMessage(
                new InformationMessage($"Withdrew {count}x {item.Name} from stash at {settlement.Name}.")
            );
        }

        private void OnSettlementOwnerChanged(
            Settlement settlement,
            bool openToClaim,
            Hero newOwner,
            Hero oldOwner,
            Hero capturerHero,
            ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
        {
            if (!_stashes.ContainsKey(settlement.StringId)) return;

            bool playerLostIt = oldOwner?.Clan == Hero.MainHero?.Clan
                                 || PlayerOwnedWorkshopHere(settlement);
            if (!playerLostIt) return;

            var stash = _stashes[settlement.StringId];

            if (capturerHero != null && stash.StoredGold > 0)
                GiveGoldAction.ApplyForSettlementToCharacter(settlement, capturerHero, stash.StoredGold);

            if (capturerHero != null)
            {
                foreach (var stored in stash.StoredItems)
                {
                    var item = Game.Current.ObjectManager.GetObject<ItemObject>(stored.ItemId);
                    if (item != null)
                        capturerHero.PartyBelongedTo?.ItemRoster.AddToCounts(item, stored.Count);
                }
            }

            _stashes.Remove(settlement.StringId);
            InformationManager.DisplayMessage(
                new InformationMessage($"Your stash at {settlement.Name} has been lost!", Colors.Red)
            );
        }

        private bool PlayerOwnedWorkshopHere(Settlement settlement)
        {
            if (!settlement.IsTown) return false;
            return settlement.Town.Workshops.Any(w => w.Owner == Hero.MainHero);
        }

        public static StashBehavior Current =>
            Campaign.Current.GetCampaignBehavior<StashBehavior>();
    }
}


