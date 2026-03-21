using SandBox;
using SandBox.Missions.AgentBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;
using ArtOfTheTrade.Behaviors;
using ArtOfTheTrade.Models;

namespace ArtOfTheTrade.Missions
{
    public class PackAnimalMissionBehavior : MissionBehavior
    {
        private bool _spawned = false;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void AfterStart()
        {
            base.AfterStart();
            SpawnCaravanHands();
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

                string sceneName = Mission.Current?.SceneName ?? "";
                if (sceneName.IndexOf("tavern",    System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sceneName.IndexOf("lordshall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sceneName.IndexOf("arena",     System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

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
            }
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