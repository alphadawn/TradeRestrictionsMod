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
        private Agent _packAnimal = null;
        private Agent _riderAgent = null;
        private float _debugTimer = 0f;

        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        public override void AfterStart()
        {
            base.AfterStart();
            SpawnPackAnimal();
        }

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);

            if (_riderAgent == null || !_riderAgent.IsActive()) return;

            var player = Agent.Main;
            if (player == null) return;

            _debugTimer += dt;

            var distToPlayer = (_riderAgent.Position - player.Position).Length;

            if (distToPlayer > 3f)
                _riderAgent.GoToAgent(player);
            else
                _riderAgent.SetMoveIdle();

            if (_debugTimer >= 2f)
            {
                _debugTimer = 0f;
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Dist: {distToPlayer:F1} Active: {_riderAgent.IsActive()}", Colors.Yellow));
            }
        }

        private void SpawnPackAnimal()
        {
            try
            {
                if (_spawned) return;
                _spawned = true;

                var player = Agent.Main;
                if (player == null) return;

                var muleItem = MBObjectManager.Instance.GetObject<ItemObject>("mule");
                if (muleItem == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Pack animal: mule not found!", Colors.Red));
                    return;
                }

                var riderCharacter = MBObjectManager.Instance.GetObject<CharacterObject>("townsman_empire");
                if (riderCharacter == null)
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Pack animal: rider not found!", Colors.Red));
                    return;
                }

                var playerFrame = player.Frame;
                var offset = playerFrame.rotation.f * -2f + playerFrame.rotation.s * 1f;
                var spawnPos = playerFrame.origin + offset;
                var dirVec2 = playerFrame.rotation.f.AsVec2;

                var mountEquipment = new Equipment();
                mountEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.ArmorItemEndSlot,
                    new EquipmentElement(muleItem));

                var muleLoad = MBObjectManager.Instance.GetObject<ItemObject>("mule_load_b");
                if (muleLoad != null)
                    mountEquipment.AddEquipmentToSlotWithoutAgent(EquipmentIndex.HorseHarness,
                        new EquipmentElement(muleLoad));

                var agentBuildData = new AgentBuildData(riderCharacter)
                    .InitialPosition(in spawnPos)
                    .InitialDirection(in dirVec2)
                    .Equipment(mountEquipment)
                    .NoHorses(false);

                var riderAgent = Mission.Current.SpawnAgent(agentBuildData);

                if (riderAgent != null && riderAgent.MountAgent != null)
                {
                    _packAnimal = riderAgent.MountAgent;
                    _riderAgent = riderAgent;

                    // Use built-in following system
                    Mission.Current.AddAgentFollowing(player, riderAgent);

                    InformationManager.DisplayMessage(new InformationMessage(
                        "Your pack animal is with you.", Colors.Green));
                }
                else
                {
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Pack animal spawned but no mount!", Colors.Red));
                }
            }
            catch (System.Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    $"Pack animal error: {ex.Message}", Colors.Red));
            }
        }
    }
}