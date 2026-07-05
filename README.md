# Art of the Trade — Feature Reference

## Overview
A Bannerlord mod that adds depth to the trade system: trade access restrictions,
certificates, haggling, caravan hands, and market intelligence.

Assembly: `ArtOfTheTrade.dll` | Namespace: `ArtOfTheTrade`

### MCM Configuration (`Settings/ArtOfTradeSettings.cs`)
All in-game options are exposed through MCM (`AttributeGlobalSettings<ArtOfTradeSettings>`).
Settings persist to `Documents/Mount and Blade II Bannerlord/Configs/ModSettings/Global/ArtOfTheTrade/ArtOfTheTrade_v1.json`.

> **Important:** the class **must** override `FormatType => "json2"`. Without a valid serializer format MCM shows the menu but silently fails to write the config file, so every setting resets to default on reload.

---

## Features

### 1. Trade Restrictions (`Behaviors/TradeRestrictionBehavior.cs`, `Patches/TradePatch.cs`)
Trading at a foreign town applies penalties unless the player has access.

**Access is granted when:**
- Player's faction owns the town (same MapFaction)
- Player owns a workshop in the town (toggle: `FreeTradeIfWorkshop`, default on)
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
Several tiers of certificates grant trading access for a duration.

| Tier | Source | Duration | Price |
|------|--------|----------|-------|
| 3-day pass | Town traders (GoodsTrader, Weaponsmith, etc.) | 3 days | 400–1000g (prosperity-scaled) |
| 6-month permit | Merchant notables | 42 days | Mid (prosperity-scaled) |
| 1-year certificate | Ruling clan member | 84 days | High (prosperity-scaled) |
| 1-year all-towns certificate | Ruling clan member (clan owns 2+ towns) | 84 days | Sum of each town's 1-year price − bulk discount |

**Certificate price formula:** `2000 + (prosperity / 8000) * 8000` gold. Merchant tier is 1.5x. 3-day pass: `400 + (prosperity / 8000) * 600`.

**All-towns certificate:** Offered by a ruling clan member only when their clan holds 2+ towns. Grants a 1-year certificate for **every** town the clan owns in one purchase. Price = sum of each town's individual 1-year price, reduced by the `AllTownsCertBulkDiscountPct` MCM setting (default 30%). Charged once; certificates are added via `TradeRestrictionBehavior.TryBuyCertificatesForTowns`.

**No certificate needed:** Towns where the player owns a workshop are freely tradeable (see Feature 1) and the certificate-purchase dialog options are hidden there.

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
`baseChance + skillBonus + repBonus + renownBonus + relationBonus + certBonus + handBonus`
- Skill bonus: `(trade + charm) / 2 / 300 * 25` (max +25)
- Rep bonus: `repScore / 5` (±10, from merchant memory)
- Renown bonus: `clan.Renown / 500 * 5` (0 to +5)
- Relation bonus: `relation / 10` (±5)
- **Certificate bonus:** +10 if player holds a valid trade certificate at the current settlement
- **Caravan hand bonus:** +2 per hired hand (max +10 at 5 hands)

**Roll outcomes:**
- `roll < chance * 0.6` → Full success (apply discount)
- `roll < chance` → Counter-offer (merchant offers half discount)
- Otherwise → Failure (1.25x buy penalty, 3-day cooldown, -5 rep)

**Counter-offer options:** Accept (half discount), Push Harder (halved odds, collapse risk), Walk Away (no penalty).

**Collapse:** 1.35x buy, 0.65x sell, -10 rep, 7-day cooldown.

**Cooldowns stored per-merchant** in `MerchantRecord.CooldownEndDay` (days since campaign start).

**isSelling semantics in TradePatch:**
`isSelling = true` means the PLAYER is selling → apply `SellModifier` (1 + discount, player earns more).
`isSelling = false` means the PLAYER is buying → apply `BuyModifier` (1 − discount, player pays less).
(This was previously inverted, which made a successful haggle *raise* buy prices instead of lowering them.)

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

### 6. Caravan Hands (`Behaviors/CaravanHandBehavior.cs`, `Dialogs/CaravanHandDialogBehavior.cs`, `Missions/PackAnimalMissionBehavior.cs`, `Patches/SpawnCamelPatch.cs`)
Hire animal handlers from the horse trader in towns. They follow the player in missions and increase carry capacity on the campaign map. The player and companions also spawn on camels in outdoor settlement scenes.

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

**Scene filtering (handlers and player/companion camel):**
Two conditions must both pass for spawning to occur:
1. Current scene name must not contain any of: `tavern`, `lordshall`, `arena`, `prison`, `dungeon`, `interior`, `keep` — checked via `CamelPatchHelper.IsInteriorScene()`
2. The previous mission's scene must also pass the same check — i.e. the player must be entering from the campaign map, not returning from a sub-location (tavern, keep, prison). Tracked via `CamelPatchHelper.LastEndedSceneName`, set by `SceneTrackingMissionBehavior.OnEndMission()` which is added to all settlement missions. Reset to `""` by a `CampaignEvents.TickEvent` listener (fires only on the campaign map), so Tab-out → map → re-enter correctly spawns the camel again.

**Player/companion camel:** `SpawnCamelPatch.cs` uses Harmony to patch `SandBoxHelpers+MissionHelper.SpawnPlayer` and `MissionAgentHandler.SpawnWanderingAgentWithInitialFrame`. Before spawning, it temporarily swaps the civilian equipment horse slot to a camel with `camel_saddle_a`, then restores the original after spawn. Both patches call `CamelPatchHelper.ShouldSpawnCamel()` which applies the same two-condition scene filter above.

**Dismiss / Recall (key `0`):**
Press `0` while handlers are following to send them to trade with notables:
- Each handler's `FollowAgentBehavior` target is switched from the player to a notable agent found in the scene (`settlement.Notables` matched against `Mission.Current.Agents`). Handlers cycle through notables if there are fewer notables than handlers.
- Press `0` again to recall — all handlers retarget the player.
- If no notables are found in the scene, dismiss is blocked with a message.

**On player capture:** `DismissAll()` is called by `CapturePenaltyBehavior` before inventory seizure. All animal items are removed from the roster first (so they are not transferred to the captor), and the `_hands` list is cleared. No gold refund.

**Haggle bonus:** Each hired hand contributes +2% to haggle success chance at any merchant (see Haggle System). Stacks with the certificate bonus.

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

### 8. Stash (`Dialogs/StashMenuBehavior.cs`, `Behaviors/StashBehavior.cs`, `Models/Stash.cs`)
Store gold and items safely at any town via the tavernkeeper. Both survive player capture.

**Access:** Talk to the tavernkeeper → "I'd like to store something safely."
- Available at **every town** (any settlement with a tavernkeeper)
- **Free** if player owns the town or has a workshop there (`IsFreeStashAt`)
- **50 gold access fee** per visit otherwise (`TryChargeAccessFee`)

**Gold operations (dialog submenu):**
- Deposit: 100 / 1,000 / 10,000 / All
- Withdraw: 100 / 1,000 / 10,000 / All

**Item operations (`MBInformationManager.ShowMultiSelectionInquiry`):**
- "Store items" — checkbox picker of player's full `ItemRoster`; each entry shows `Name x{count}` and total value hint; selected items deposited via `StashBehavior.DepositItem`
- "Retrieve items" — checkbox picker of `stash.StoredItems`; selected items returned to `ItemRoster` via `StashBehavior.WithdrawItem`

**Settlement lost:** `OnSettlementOwnerChangedEvent` — if player loses the settlement, stored gold and items are transferred to the new owner's party. Stash entry is deleted.

**Stash safety:** Stash data lives in `StashBehavior`'s own serialized `Dictionary<string, Stash>`, keyed by `settlement.StringId`. It is **not** in `Hero.MainHero.Gold` or `MobileParty.MainParty.ItemRoster` — capture penalty cannot touch it.

**Data model:** `Stash` → `StoredGold (int)` + `StoredItems (List<StoredItem>)`. `StoredItem` → `ItemId (string)` + `Count (int)`.

**Serialization:** Newtonsoft.Json via `IDataStore.SyncData`.

---

### 9. Capture Penalty & Negotiation (`Behaviors/CapturePenaltyBehavior.cs`, `Dialogs/RansomDialogBehavior.cs`)
When the player is captured, gold and inventory are seized. Two pre-battle encounter-menu options then allow negotiation — one to avoid capture before it happens, one to recover goods from a lord you later catch.

**On capture (`HeroPrisonerTaken` event) — full flow:**
1. Check grace period — if same lord captured us within 1 in-game day of a mercy release, release again with no penalty and return
2. `CaravanHandBehavior.DismissAll()` — animal items removed from roster before seizure (hands scatter, no refund)
3. **Clan tier ≤ 1 + captor has `DefaultTraits.Mercy > 0`** → lord releases player immediately via `EndCaptivityAction.ApplyByReleasedByChoice`; nothing taken; 1-day grace period set against this lord
4. **Clan tier ≤ 1, no Mercy** → lord takes 25% of gold only, leaves items; captor tracked for future recovery
5. **Normal tier / bandits** → full penalty: all gold taken, all `ItemRoster` items seized and added to captor's party roster; total item value recorded in `_itemValueTaken`
6. Stash gold **and** stash items are **unaffected** (not in `Hero.Gold` or `ItemRoster`)

---

**Scenario 1 — Offer safe passage (avoid capture, pre-battle)**

Appears in the `"encounter"` game menu when the encountered party leader is a lord (non-minor-faction clan) and the player has ≥100 gold.

Three offer tiers:
| Offer | Base accept chance |
|-------|--------------------|
| 25% of gold | 30% |
| 50% of gold | 55% |
| 75% of gold | 75% |

**Skill bonus:** `(Charm + Trade) / 2 / 300 * 20%` (max +20%)

On accept: gold paid via `GiveGoldAction`, `PlayerEncounter.Finish()` — player goes free.
On reject: menu returns to `"encounter"` for normal battle.

---

**Scenario 2 — Demand return of goods (recovery, pre-battle)**

Appears in the `"encounter"` game menu when the encountered party leader is the lord who previously seized the player's belongings (`HasPendingClaimAgainst`).

Lord makes a single offer calculated from:
- Base 30%
- `+5%` per level of `DefaultTraits.Honor`
- `+5%` per level of `DefaultTraits.Generosity`
- `±10%` from relation (`relation / 100 * 10%`)
- `+10%` max from player renown (`renown / 1000 * 10%`)
- Clamped to 10–80%

Player options: **Accept** (receive gold equivalent via `ApplyRecovery`, claim cleared, `PlayerEncounter.Finish()`) or **Refuse** (return to `"encounter"` for battle).

---

**State tracked (serialized):**
- `_lastCaptorId` — StringId of the capturing hero (used for recovery claim check)
- `_goldTaken` — gold taken at capture
- `_itemValueTaken` — total `item.Value * count` of seized items (used for recovery offer calculation)
- `_gracePeriodCaptorId` — StringId of the lord who just gave a mercy/low-tier release
- `_gracePeriodEndsDay` — campaign day when the grace period expires (set to `today + 1.0`)

---

### 10. External Save System (`Save/ModSaveData.cs`, `Save/ModSaveManager.cs`, `Behaviors/ModDataBehavior.cs`)
All mod data is stored in a per-campaign JSON file on disk rather than inside the game's native `.sav` file. This eliminates save corruption from Newtonsoft.Json serialisation errors inside `IDataStore`.

**Files saved to:**
`Documents/Mount and Blade II Bannerlord/ArtOfTradeSaves/aot_{guid}.json`

**GUID lifecycle:**
| Situation | Result |
|-----------|--------|
| New campaign | GUID generated once via `Guid.NewGuid()`, stored in `.sav`, JSON created |
| Same campaign, any save slot | Same GUID written to every slot → same JSON overwritten |
| Autosave | Identical to manual save — fires same `SyncData(IsSaving)` path |
| Mod added to existing save (no GUID present) | New GUID generated → new JSON, as if a fresh start |
| Second campaign | Different `.sav` → different GUID → different JSON, both files coexist |

**Important:** All save slots of the same playthrough share one GUID and one JSON. If you reload an older slot, the JSON it reads reflects the **most recent** session, not the state when that slot was created. This is a known tradeoff of external saves — rolling back a slot does not roll back mod data.

**Architecture:**
- `ModDataBehavior` is registered **first** in `SubModule`. Its `SyncData` is the only place `IDataStore` is touched (read/write of the GUID string). All other behaviors have empty `SyncData`.
- All other behaviors access their persistent data via `ModSaveManager.Data.*` properties directly — mutations are live in memory immediately and flushed to JSON on the next save event.
- `ModSaveManager.WriteToFile()` can also be called at any point for an immediate flush (e.g. after a significant state change).

**Data contained in `ModSaveData`:**
| Field | Owner |
|-------|-------|
| `CaravanHands` | `CaravanHandBehavior` |
| `MerchantData` | `HaggleBehavior` |
| `LastCaptorId`, `GoldTaken`, `ItemValueTaken`, `GracePeriodCaptorId`, `GracePeriodEndsDay` | `CapturePenaltyBehavior` |
| `Certificates` | `TradeRestrictionBehavior` |
| `Stashes` | `StashBehavior` |
| `LastIntelDay` | `MarketIntelDialogBehavior` |

---

## Key APIs Reference

### Scene Names
All unique settlement scene names (from `SandBox/ModuleData/settlements.xml`) are saved to:
`Documents/TradeRestrictionsMod/bannerlord_scene_names.json`

To regenerate, run: `powershell.exe -ExecutionPolicy Bypass -File "C:/Users/Ibrahim/Documents/TradeRestrictionsMod/extract_scenes.ps1"`

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
