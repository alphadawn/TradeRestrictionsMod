using System.Collections.Generic;
using System.Linq;
using SandBox;
using SandBox.Missions.AgentBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using ArtOfTheTrade.Behaviors;
using ArtOfTheTrade.Models;
using ArtOfTheTrade.Patches;
using ArtOfTheTrade.Settings;

namespace ArtOfTheTrade.Missions
{
    public class PackAnimalMissionBehavior : MissionBehavior
    {
        private bool _spawned = false;
        private bool _dismissed = false;
        private readonly List<(Agent agent, FollowAgentBehavior follow)> _handAgents
            = new List<(Agent, FollowAgentBehavior)>();

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void AfterStart()
        {
            base.AfterStart();
            SpawnCaravanHands();
        }

        public override void OnMissionTick(float dt)
        {
            if (!_spawned || _handAgents.Count == 0) return;
            if (!Input.IsKeyPressed(InputKey.D0)) return;

            if (_dismissed) RecallHandlers();
            else DismissHandlers();
        }

        // Each outfit set: head, body, leg, cape (null = none)
        private static readonly string[][] Outfits = new[]
        {
            new[] { "turban",              "long_woolen_tunic", "khuzait_curved_boots", "southern_shawl" },
            new[] { "tuareg",              "layered_robe",       "folded_town_boots",   null              },
            new[] { "aserai_civil_hscarf_a","aserai_civil_b",   "steppe_leather_boots", null             },
        };

        // Spawn offsets relative to player: right, behind-right, behind-left, far-right, far-left
        private static Vec3 GetSpawnOffset(int index, MatrixFrame frame)
        {
            return index switch
            {
                0 => frame.rotation.s * 3.5f,
                1 => frame.rotation.f * -2f + frame.rotation.s * 1.5f,
                2 => frame.rotation.f * -2f + frame.rotation.s * -1.5f,
                3 => frame.rotation.f * -4f + frame.rotation.s * 3f,
                4 => frame.rotation.f * -4f + frame.rotation.s * -3f,
                _ => frame.rotation.s * 3.5f
            };
        }

        private void SpawnCaravanHands()
        {
            try
            {
                if (_spawned) return;
                _spawned = true;

                var settings = ArtOfTradeSettings.Instance;
                if (!(settings?.EnableCaravanHands ?? true)) return;
                if (!(settings?.SpawnHandlersInSettlements ?? true)) return;

                // Only spawn in the outdoor town/castle centre.
                // Interior sub-locations (keep, prison, tavern, arena, lord's hall) all have
                // scene names containing recognisable keywords — skip them.
                string sceneName = Mission.Current?.SceneName ?? "";
                if (CamelPatchHelper.IsInteriorScene(sceneName)) return;

                // Only spawn when entering from the campaign map, not when returning
                // from a sub-location (tavern, keep, prison) within the same settlement.
                if (CamelPatchHelper.IsInteriorScene(CamelPatchHelper.LastEndedSceneName)) return;

                var player = Agent.Main;
                if (player == null) return;

                var b = CaravanHandBehavior.Current;
                if (b == null || b.TotalHands == 0) return;

                var frame = player.Frame;
                int i = 0;
                foreach (var hand in b.Hands)
                {
                    if (i >= 5) break;
                    Vec3 spawnPos = frame.origin + GetSpawnOffset(i, frame);
                    SpawnHandAgent(player, hand, spawnPos, frame.rotation.f.AsVec2);
                    i++;
                }

                InformationManager.DisplayMessage(new InformationMessage(
                    $"Your caravan is with you. ({b.TotalHands} handler{(b.TotalHands > 1 ? "s" : "")})",
                    Colors.Green));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Caravan spawn error: {ex.Message}", Colors.Red));
            }
        }

        private void SpawnHandAgent(Agent player, CaravanHand hand, Vec3 spawnPos, Vec2 dirVec2)
        {
            var riderCharacter = MBObjectManager.Instance.GetObject<CharacterObject>("townsman_aserai");
            if (riderCharacter == null) return;

            string mountId = hand.AnimalType switch
            {
                CaravanAnimalType.SumpterHorse => "sumpter_horse",
                CaravanAnimalType.Mule         => "mule",
                CaravanAnimalType.Camel        => "camel",
                _                              => "mule"
            };

            string harnessId = hand.AnimalType switch
            {
                CaravanAnimalType.Mule  => MBRandom.RandomInt(0, 2) == 0 ? "mule_load_b" : "mule_load_c",
                CaravanAnimalType.Camel => "camel_saddle_a",
                _                      => null
            };

            var mountItem = MBObjectManager.Instance.GetObject<ItemObject>(mountId);
            if (mountItem == null) return;

            int outfitIdx = MBMath.ClampInt(hand.OutfitIndex, 0, Outfits.Length - 1);
            string[] outfit = Outfits[outfitIdx];

            var equipment = new Equipment();

            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse, new EquipmentElement(mountItem));

            if (harnessId != null)
            {
                var harness = MBObjectManager.Instance.GetObject<ItemObject>(harnessId);
                if (harness != null)
                    equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness, new EquipmentElement(harness));
            }

            AddClothingSlot(equipment, EquipmentIndex.Head,  outfit[0]);
            AddClothingSlot(equipment, EquipmentIndex.Body,  outfit[1]);
            AddClothingSlot(equipment, EquipmentIndex.Leg,   outfit[2]);
            AddClothingSlot(equipment, EquipmentIndex.Cape,  outfit[3]);

            var buildData = new AgentBuildData(riderCharacter)
                .InitialPosition(in spawnPos)
                .InitialDirection(in dirVec2)
                .Equipment(equipment)
                .NoHorses(false);

            var agent = Mission.Current.SpawnAgent(buildData);
            if (agent?.MountAgent == null) return;

            var campaignComponent = agent.GetComponent<CampaignAgentComponent>();
            if (campaignComponent == null) return;

            if (campaignComponent.AgentNavigator == null)
                campaignComponent.CreateAgentNavigator();

            if (campaignComponent.AgentNavigator == null) return;

            campaignComponent.AgentNavigator.AddBehaviorGroup<DailyBehaviorGroup>();
            campaignComponent.AgentNavigator.AddBehaviorGroup<InterruptingBehaviorGroup>();

            var dailyGroup = campaignComponent.AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>();
            if (dailyGroup != null)
            {
                var follow = dailyGroup.AddBehavior<FollowAgentBehavior>();
                follow.SetTargetAgent(player);
                dailyGroup.SetScriptedBehavior<FollowAgentBehavior>();
                _handAgents.Add((agent, follow));
            }
        }

        private void DismissHandlers()
        {
            var notables = FindNotableAgents();
            if (notables.Count == 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "No notables found nearby to send handlers to.", Colors.Red));
                return;
            }

            _dismissed = true;
            for (int i = 0; i < _handAgents.Count; i++)
            {
                var (agent, follow) = _handAgents[i];
                if (!agent.IsActive()) continue;
                follow.SetTargetAgent(notables[i % notables.Count]);
            }
            MBInformationManager.AddQuickInformation(
                new TextObject("Caravan handlers sent to trade."), 0, null);
        }

        private void RecallHandlers()
        {
            _dismissed = false;
            var player = Agent.Main;
            if (player == null) return;

            foreach (var (agent, follow) in _handAgents)
            {
                if (!agent.IsActive()) continue;
                follow.SetTargetAgent(player);
            }
            MBInformationManager.AddQuickInformation(
                new TextObject("Caravan handlers recalled."), 0, null);
        }

        private List<Agent> FindNotableAgents()
        {
            var result = new List<Agent>();
            var settlement = Settlement.CurrentSettlement;
            if (settlement == null) return result;

            var notableChars = new HashSet<CharacterObject>(
                settlement.Notables.Select(n => n.CharacterObject));

            foreach (var a in Mission.Current.Agents)
            {
                if (!a.IsActive() || a == Agent.Main) continue;
                if (notableChars.Contains(a.Character as CharacterObject))
                    result.Add(a);
            }
            return result;
        }

        private static void AddClothingSlot(Equipment equipment, EquipmentIndex slot, string itemId)
        {
            if (itemId == null) return;
            var item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
            if (item != null)
                equipment.AddEquipmentToSlotWithoutAgent(slot, new EquipmentElement(item));
        }
    }
}