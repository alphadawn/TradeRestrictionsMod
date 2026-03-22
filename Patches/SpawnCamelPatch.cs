using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace ArtOfTheTrade.Patches
{
    internal static class CamelPatchHelper
    {
        /// <summary>Scene name of the mission that just ended. Set by SceneTrackingMissionBehavior.</summary>
        internal static string LastEndedSceneName = "";

        internal static bool IsInteriorScene(string sceneName) =>
            sceneName.IndexOf("tavern",    StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("lordshall", StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("arena",     StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("prison",    StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("dungeon",   StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("interior",  StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("keep",      StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool ShouldSpawnCamel()
        {
            var sceneName = Mission.Current?.SceneName ?? "";

            // Don't spawn in interior scenes
            if (IsInteriorScene(sceneName)) return false;

            // Don't spawn if we just came from an interior (player exited tavern/keep/prison)
            if (IsInteriorScene(LastEndedSceneName)) return false;

            var settlement = Settlement.CurrentSettlement;
            return settlement != null && (settlement.IsTown || settlement.IsCastle || settlement.IsVillage);
        }
    }

    /// <summary>
    /// Lightweight behavior added to all settlement missions. Records the scene name
    /// when the mission ends so the camel patch knows whether the player came from an interior.
    /// </summary>
    public class SceneTrackingMissionBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

        protected override void OnEndMission()
        {
            CamelPatchHelper.LastEndedSceneName = Mission.Current?.SceneName ?? "";
        }
    }

    // Patches SpawnPlayer to give the player a camel with heavy load saddle
    [HarmonyPatch]
    public static class SpawnPlayerCamelPatch
    {
        private static EquipmentElement _savedHorse;
        private static EquipmentElement _savedHarness;
        private static bool _active = false;

        [HarmonyTargetMethod]
        static System.Reflection.MethodBase TargetMethod()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.FullName == "SandBox.SandBoxHelpers+MissionHelper");
            return type?.GetMethod("SpawnPlayer", new Type[]
            {
                typeof(GameEntity), typeof(bool), typeof(bool), typeof(bool), typeof(bool)
            });
        }

        [HarmonyPrefix]
        static void Prefix(ref bool noHorses, bool civilianEquipment)
        {
            _active = false;
            if (!civilianEquipment || !CamelPatchHelper.ShouldSpawnCamel()) return;

            var hero = Hero.MainHero;
            if (hero == null) return;

            var camel = MBObjectManager.Instance.GetObject<ItemObject>("camel");
            if (camel == null) return;
            var saddle = MBObjectManager.Instance.GetObject<ItemObject>("camel_saddle_a");

            _savedHorse = hero.CivilianEquipment[EquipmentIndex.Horse];
            _savedHarness = hero.CivilianEquipment[EquipmentIndex.HorseHarness];

            hero.CivilianEquipment[EquipmentIndex.Horse] = new EquipmentElement(camel);
            hero.CivilianEquipment[EquipmentIndex.HorseHarness] = saddle != null
                ? new EquipmentElement(saddle) : EquipmentElement.Invalid;

            noHorses = false;
            _active = true;
        }

        [HarmonyPostfix]
        static void Postfix()
        {
            if (!_active) return;
            _active = false;

            var hero = Hero.MainHero;
            if (hero == null) return;

            hero.CivilianEquipment[EquipmentIndex.Horse] = _savedHorse;
            hero.CivilianEquipment[EquipmentIndex.HorseHarness] = _savedHarness;
        }
    }

    // Patches SpawnWanderingAgentWithInitialFrame to give companions a camel with heavy load saddle
    [HarmonyPatch(typeof(MissionAgentHandler), "SpawnWanderingAgentWithInitialFrame")]
    public static class SpawnCompanionCamelPatch
    {
        private static readonly Stack<(Hero hero, EquipmentElement horse, EquipmentElement harness)> _patchStack
            = new Stack<(Hero, EquipmentElement, EquipmentElement)>();

        static void Prefix(LocationCharacter locationCharacter, ref bool noHorses)
        {
            if (!CamelPatchHelper.ShouldSpawnCamel()) return;
            if (!locationCharacter.UseCivilianEquipment) return;

            var hero = locationCharacter.Character?.HeroObject;
            if (hero == null || !hero.IsPlayerCompanion) return;

            var camel = MBObjectManager.Instance.GetObject<ItemObject>("camel");
            if (camel == null) return;
            var saddle = MBObjectManager.Instance.GetObject<ItemObject>("camel_saddle_a");

            var savedHorse = hero.CivilianEquipment[EquipmentIndex.Horse];
            var savedHarness = hero.CivilianEquipment[EquipmentIndex.HorseHarness];

            hero.CivilianEquipment[EquipmentIndex.Horse] = new EquipmentElement(camel);
            hero.CivilianEquipment[EquipmentIndex.HorseHarness] = saddle != null
                ? new EquipmentElement(saddle) : EquipmentElement.Invalid;

            noHorses = false;
            _patchStack.Push((hero, savedHorse, savedHarness));
        }

        static void Postfix()
        {
            if (_patchStack.Count == 0) return;
            var (hero, savedHorse, savedHarness) = _patchStack.Pop();
            hero.CivilianEquipment[EquipmentIndex.Horse] = savedHorse;
            hero.CivilianEquipment[EquipmentIndex.HorseHarness] = savedHarness;
        }
    }
}