using SandBox;
using SandBox.Missions.AgentBehaviors;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace ArtOfTheTrade.Missions
{
    public class PackAnimalMissionBehavior : MissionBehavior
    {
        private bool _spawned = false;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void AfterStart()
        {
            base.AfterStart();
            SpawnPackAnimal();
        }

        private void SpawnPackAnimal()
        {
            try
            {
                if (_spawned) return;
                _spawned = true;

                // Don't spawn in tavern, keep, or arena
                string sceneName = Mission.Current?.SceneName ?? "";
                if (sceneName.IndexOf("tavern", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sceneName.IndexOf("lordshall", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    sceneName.IndexOf("arena", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                var player = Agent.Main;
                if (player == null) return;

                var playerFrame = player.Frame;

                // NPC 1 - Turban + Shawl + Long Woolen Tunic + Curved Boots, on camel (right side)
                SpawnFollowerWithMount(
                    player,
                    playerFrame.origin + playerFrame.rotation.s * 3.5f,
                    playerFrame.rotation.f.AsVec2,
                    "townsman_aserai", "camel", "camel_saddle_a",
                    "turban", "long_woolen_tunic", "khuzait_curved_boots", "southern_shawl"
                );

                // NPC 2 - Tribal Turban + Layered Robe + Folded Town Boots, on mule (behind right)
                SpawnFollowerWithMount(
                    player,
                    playerFrame.origin + playerFrame.rotation.f * -2f + playerFrame.rotation.s * 1f,
                    playerFrame.rotation.f.AsVec2,
                    "townsman_aserai", "mule", "mule_load_b",
                    "tuareg", "layered_robe", "folded_town_boots", null
                );

                // NPC 3 - Keffiyeh with Brown Band + Kaftan with Leather Belt + Sturdy Leather Boots, on mule (behind left)
                SpawnFollowerWithMount(
                    player,
                    playerFrame.origin + playerFrame.rotation.f * -2f + playerFrame.rotation.s * -1f,
                    playerFrame.rotation.f.AsVec2,
                    "townsman_aserai", "mule", "mule_load_c",
                    "aserai_civil_hscarf_a", "aserai_civil_b", "steppe_leather_boots", null
                );

                InformationManager.DisplayMessage(new InformationMessage(
                    "Your caravan is with you.", Colors.Green));
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Pack animal error: {ex.Message}", Colors.Red));
            }
        }

        private void SpawnFollowerWithMount(Agent player, Vec3 spawnPos, Vec2 dirVec2,
            string characterId, string mountId, string harnessId,
            string headItemId, string bodyItemId, string legItemId, string capeItemId)
        {
            var riderCharacter = MBObjectManager.Instance.GetObject<CharacterObject>(characterId);
            var mountItem = MBObjectManager.Instance.GetObject<ItemObject>(mountId);
            if (riderCharacter == null || mountItem == null) return;

            var equipment = new Equipment();

            // Mount
            equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Horse,
                new EquipmentElement(mountItem));
            var harness = MBObjectManager.Instance.GetObject<ItemObject>(harnessId);
            if (harness != null)
                equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness,
                    new EquipmentElement(harness));

            // Clothing
            if (headItemId != null)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(headItemId);
                if (item != null) equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Head, new EquipmentElement(item));
            }
            if (bodyItemId != null)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(bodyItemId);
                if (item != null) equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Body, new EquipmentElement(item));
            }
            if (legItemId != null)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(legItemId);
                if (item != null) equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Leg, new EquipmentElement(item));
            }
            if (capeItemId != null)
            {
                var item = MBObjectManager.Instance.GetObject<ItemObject>(capeItemId);
                if (item != null) equipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.Cape, new EquipmentElement(item));
            }

            var agentBuildData = new AgentBuildData(riderCharacter)
                .InitialPosition(in spawnPos)
                .InitialDirection(in dirVec2)
                .Equipment(equipment)
                .NoHorses(false);

            var riderAgent = Mission.Current.SpawnAgent(agentBuildData);

            if (riderAgent != null && riderAgent.MountAgent != null)
            {
                var campaignComponent = riderAgent.GetComponent<CampaignAgentComponent>();
                if (campaignComponent != null && campaignComponent.AgentNavigator == null)
                    campaignComponent.CreateAgentNavigator();

                if (campaignComponent?.AgentNavigator != null)
                {
                    campaignComponent.AgentNavigator.AddBehaviorGroup<DailyBehaviorGroup>();
                    campaignComponent.AgentNavigator.AddBehaviorGroup<InterruptingBehaviorGroup>();

                    var behaviorGroup = campaignComponent.AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>();
                    if (behaviorGroup != null)
                    {
                        var followBehavior = behaviorGroup.AddBehavior<FollowAgentBehavior>();
                        followBehavior.SetTargetAgent(player);
                        behaviorGroup.SetScriptedBehavior<FollowAgentBehavior>();
                    }
                }
            }
        }
    }
}
