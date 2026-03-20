# Art of the Trade — Feature Reference

## Overview
A Bannerlord mod that adds depth to the trade system: trade access restrictions,
certificates, haggling, caravan hands, and market intelligence.

Assembly: `ArtOfTheTrade.dll` | Namespace: `ArtOfTheTrade`

---

## Features

### 1. Trade Restrictions (`Behaviors/TradeRestrictionBehavior.cs`, `Patches/TradePatch.cs`)
Trading at a foreign town applies penalties unless the player has access.

**Access is granted when:**
- Player's faction owns the town (same MapFaction)
- Player's kingdom has a trade agreement with the town's kingdom (vanilla diplomacy)
- Player holds a valid trade certificate for that town

**Penalties when no access:**
- Buying: 5x price
- Selling: 0.5x price
- Warning shown once per settlement via `InformationManager.DisplayMessage`

**Key patch:** `DefaultTradeItemPriceFactorModel.GetPrice` (HarmonyPostfix)
The patch checks `clientParty` and `merchant` to determine if the player is involved,
then applies modifiers to `ref int __result`.

---

### 2. Trade Certificates (`Dialogs/CertificateDialogBehavior.cs`, `Models/TradeCertificate.cs`)
Three tiers of certificates grant trading access for a duration.

| Tier | Source | Duration | Price |
|------|--------|----------|-------|
| 3-day pass | Town traders (GoodsTrader, Weaponsmith, etc.) | 3 days | 200–500g (prosperity-scaled) |
| 6-month permit | Merchant notables | 42 days | Mid (prosperity-scaled) |
| 1-year certificate | Ruling clan member | 84 days | High (prosperity-scaled) |

**Certificate price formula:** `2000 + (prosperity / 8000) * 8000` gold. Merchant tier is 1.5x.

**Revocation:** Certificate is automatically invalid if player's faction is at war with the town's faction.

**NPC dialog text** changes based on town prosperity (high/normal/low) — see `strings.xml`.

**Serialization:** Certificates saved as JSON via `IDataStore.SyncData` using `Newtonsoft.Json`.

---

### 3. Bandit Certificates (`Dialogs/CertificateDialogBehavior.cs`)
Gang leaders (`Occupation.GangLeader`) in towns offer black-market access via a persuasion check.

| Tier | Skill gate | Base chance | Duration | Price |
|------|-----------|-------------|----------|-------|
| Smooth | None | 65% | 3 days | 50% of trader pass price |
| Bold | Charm OR Roguery >= 60 | 35% | 7 days | 50% of trader pass price |

Chance formula: `baseChance + (charm + roguery) / 2 / 300 * 30`

Awards 20 Charm XP + 30 Roguery XP on success.

Settlement fallback: `npc.CurrentSettlement ?? npc.HomeSettlement` used when
`Settlement.CurrentSettlement` is null (gang leader found outside mission context).

---

### 4. Haggle System (`Behaviors/HaggleBehavior.cs`, `Dialogs/CertificateDialogBehavior.cs`)
Multi-round price negotiation with traders and village notables.

**Tiers:**
| Tier | Skill gate | Base chance | Discount on success |
|------|-----------|-------------|---------------------|
| Safe | None | 70% | 10% |
| Moderate | Trade >= 50 | 45% | 20% |
| Bold | Trade >= 100 | 25% | 35% |

**Success chance formula:**
`baseChance + skillBonus + repBonus + renownBonus + relationBonus`
- Skill bonus: `(trade + charm) / 2 / 300 * 25` (max +25)
- Rep bonus: `repScore / 5` (±10, from merchant memory)
- Renown bonus: `clan.Renown / 500 * 5` (0 to +5)
- Relation bonus: `relation / 10` (±5)

**Roll outcomes:**
- `roll < chance * 0.6` → Full success (apply discount)
- `roll < chance` → Counter-offer (merchant offers half discount)
- Otherwise → Failure (1.25x buy penalty, 3-day cooldown, -5 rep)

**Counter-offer options:** Accept (half discount), Push Harder (halved odds, collapse risk), Walk Away (no penalty).

**Collapse:** 1.35x buy, 0.65x sell, -10 rep, 7-day cooldown.

**Cooldowns stored per-merchant** in `MerchantRecord.CooldownEndDay` (days since campaign start).

**isSelling semantics in TradePatch:**
`isSelling = true` means the MERCHANT is selling (player is BUYING) → apply `BuyModifier`.
`isSelling = false` means the merchant is buying (player is SELLING) → apply `SellModifier`.

**XP awards:**
- Attempting: +15 Trade, +5 Charm
- Full success: +25 Trade, +15 Charm
- Accept counter: +15 Trade, +8 Charm
- Push harder win: +35 Trade, +20 Charm

---

### 5. Village Trade & Haggle (`Dialogs/CertificateDialogBehavior.cs`)
Two dialog options added for `Occupation.RuralNotable` in villages:
- Haggle (same system as town traders, no certificate needed)
- "Trade with the village" → opens trade screen via `InventoryScreenHelper.ActivateTradeWithCurrentSettlement()` (namespace: `Helpers`)

---

### 6. Caravan Hands (`Behaviors/CaravanHandBehavior.cs`, `Dialogs/CaravanHandDialogBehavior.cs`, `Missions/PackAnimalMissionBehavior.cs`)
Hire animal handlers from the horse trader in towns. They follow the player in missions and increase carry capacity on the campaign map.

**Animal types:**
| Type | Hire cost | Capacity bonus | Upkeep |
|------|-----------|---------------|--------|
| Sumpter horse | 50g | +30 | 3g/day |
| Mule | 100g | +50 | 5g/day |
| Camel | 300g | +100 | 10g/day |

**Cap:** `floor(partySize / 10)`, max 5 total.

**Capacity:** Animals added to `MobileParty.MainParty.ItemRoster` — the game natively counts
pack animals in the roster toward carry capacity.

**Upkeep:** Daily tick deducts total upkeep. If insufficient gold, most expensive handler leaves first.

**Roster sync:** Daily tick compares `_hands` list against item roster counts. If items were
sold manually, corresponding hands are removed.

**Mission spawning:** `PackAnimalMissionBehavior.AfterStart()` spawns one agent per hand.
Outfits randomized on hire (index 0–2 stored in `CaravanHand.OutfitIndex`).
Spawn positions: right, behind-right, behind-left, far-right, far-left relative to player frame.

**Skipped scenes:** tavern, lordshall, arena (checked via `Mission.Current.SceneName`).

**On player capture:** `DismissAll()` is called by `CapturePenaltyBehavior` before inventory seizure. All animal items are removed from the roster first (so they are not transferred to the captor), and the `_hands` list is cleared. No gold refund.

---

### 7. Market Intelligence (`Dialogs/MarketIntelDialogBehavior.cs`)
Pay the tavernkeeper (`Occupation.Tavernkeeper`) 50 gold for a tip about nearby market prices.

**How tips are generated:**
1. Scan all town settlements within 150 map units (`Settlement.GetPosition2D`)
2. For each town, iterate `settlement.ItemRoster` filtering to `item.IsTradeGood == true`
3. Fetch current price via `Town.GetItemPrice(new EquipmentElement(item), MobileParty.MainParty, false)`
4. Compute ratio: `price / item.Value` (Value = base price)
5. Find highest ratio above 1.2 — that town+item becomes the tip

**Tip text** scales with premium: mild (<40%), notable (40–80%), urgent (80%+).

**Cooldown:** 3 days per settlement (tracked in `Dictionary<string, float>` keyed by `settlement.StringId`).

---

### 8. Gold Stash (`Dialogs/StashMenuBehavior.cs`, `Behaviors/StashBehavior.cs`, `Models/Stash.cs`)
Deposit and withdraw gold at any town where the player owns a workshop, or any castle the player owns.

**Access condition:** `StashBehavior.CanPlayerStashAt(settlement)` — true if player owns the settlement or has a workshop there.

**Menu entry:** "Manage stash" appears in the `town` and `castle` game menus (position 5).

**Deposit options:** 100 / 1,000 / 10,000 / All
**Withdraw options:** 100 / 1,000 / 10,000 / All

**Stash safety:** Stashed gold is stored in `StashBehavior`'s own serialized data — it is **not** in `Hero.MainHero.Gold` or `ItemRoster`. Capture penalty cannot touch it.

**Serialization:** Per-settlement stash data saved via `IDataStore.SyncData` using Newtonsoft.Json.

---

### 9. Capture Penalty & Ransom (`Behaviors/CapturePenaltyBehavior.cs`, `Dialogs/RansomDialogBehavior.cs`)
When the player is captured, gold and inventory are seized. A ransom negotiation dialog is then available when talking to the captor lord.

**On capture (`HeroPrisonerTaken` event):**
1. `CaravanHandBehavior.DismissAll()` fires first — animal items removed from roster before seizure (hands scatter, no refund)
2. Gold transferred to captor lord (`GiveGoldAction`), or lost if captured by bandits
3. All `ItemRoster` items transferred to captor's party
4. Stash gold is unaffected (not in ItemRoster or Hero.Gold)

**Ransom dialog** — shown in `hero_main_options` when talking to the lord who captured you:
- Condition: `Hero.MainHero.IsPrisoner` && `CapturePenaltyBehavior.CanNegotiateRansom(conversationHero)`
- Bandits do not offer ransom negotiation

**Two-attempt persuasion:**
| Attempt | Base chance | Gold returned on success |
|---------|------------|--------------------------|
| 1st | 45% | 40% of gold taken |
| 2nd | 25% | 20% of gold taken |

**Skill bonus (both attempts):** `(Charm + Trade) / 2 / 300 * 30%` (max +30%)

**State tracked (serialized):**
- `_lastCaptorId` — StringId of the capturing hero
- `_goldTaken` — gold taken at capture (used to compute return amount)
- `_attemptsLeft` — decrements on each attempt; 0 = dialog hidden

---

## Key APIs Reference

### Dialog System
```csharp
// Add a player dialog line
starter.AddPlayerLine(id, fromToken, toToken, text, conditionFunc, consequenceFunc);

// Add an NPC dialog line
starter.AddDialogLine(id, fromToken, toToken, text, conditionFunc, consequenceFunc);

// Set a text variable for use in dialog with {VAR_NAME}
MBTextManager.SetTextVariable("VAR_NAME", "value");

// Current conversation character (may not be a Hero)
CharacterObject.OneToOneConversationCharacter

// Current conversation hero (null if non-hero NPC)
Hero.OneToOneConversationHero
```

### Harmony Patching
```csharp
// Prefix: return false to skip original method
// Postfix: __result is the return value (ref for modification)
[HarmonyPatch(typeof(TargetClass), "MethodName", new Type[] { typeof(Arg1), ... })]
public static void Postfix(Arg1 arg, ref int __result) { }
```

### Campaign Behaviors
```csharp
// Register for events
CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener(this, OnMissionStarted);

// Save/load data
dataStore.SyncData("KeyName", ref value); // works for string, int, float, bool

// Get another behavior
Campaign.Current.GetCampaignBehavior<SomeBehavior>()
```

### Gold Actions
```csharp
// Remove gold from player (null receiver = gold removed from game)
GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, amount, disableNotification: true);

// Pay to a settlement
GiveGoldAction.ApplyForCharacterToSettlement(hero, settlement, amount);
```

### Economy
```csharp
// Current price of an item at a town (isSelling = settlement selling to player)
int price = town.GetItemPrice(new EquipmentElement(item), MobileParty.MainParty, isSelling: false);

// Base/reference price of an item
int basePrice = item.Value;

// Whether an item is a trade good (not weapon/armor/horse)
bool isGood = item.IsTradeGood;

// Add/remove items from party inventory (also affects carry capacity)
MobileParty.MainParty.ItemRoster.AddToCounts(item, count); // negative to remove
int count = roster.GetItemNumber(item);
```

### Settlement
```csharp
// Map position for distance calculation
Vec2 pos = settlement.GetPosition2D;
float dist = (posA - posB).Length;

// Current settlement during missions/dialog
Settlement.CurrentSettlement

// Trade agreement between kingdoms
var agreements = Campaign.Current.GetCampaignBehavior<ITradeAgreementsCampaignBehavior>();
bool hasDeal = agreements.HasTradeAgreement(kingdomA, kingdomB);
```

### Mission NPC Spawning
```csharp
// Build agent data
var buildData = new AgentBuildData(characterObject)
    .InitialPosition(in spawnPos)
    .InitialDirection(in dirVec2)
    .Equipment(equipment)
    .NoHorses(false);

var agent = Mission.Current.SpawnAgent(buildData);

// Add follow behavior
var campaignComp = agent.GetComponent<CampaignAgentComponent>();
campaignComp.CreateAgentNavigator();
campaignComp.AgentNavigator.AddBehaviorGroup<DailyBehaviorGroup>();
var group = campaignComp.AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>();
var follow = group.AddBehavior<FollowAgentBehavior>();
follow.SetTargetAgent(targetAgent);
group.SetScriptedBehavior<FollowAgentBehavior>();
```

### Opening Trade Screen from Dialog
```csharp
// Namespace: Helpers (TaleWorlds.CampaignSystem.dll)
InventoryScreenHelper.ActivateTradeWithCurrentSettlement();
```