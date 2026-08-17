using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SephiriaTogether
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.sephiriamods.sephiriatogether";
        public const string PluginName = "Sephiria Together";
        public const string PluginVersion = "3.5.1";

        private static ConfigEntry<int> scalingStartsAbove;
        private static ConfigEntry<float> healthPerExtraPlayer;
        private static ConfigEntry<float> maximumMultiplier;
        internal static ConfigEntry<bool> allowLowerProgressPlayers;
        internal static ConfigEntry<bool> allowMidRunJoin;
        internal static ConfigEntry<bool> allowUngroupedStageTransition;
        internal static ConfigEntry<bool> breathingHeal;
        internal static ConfigEntry<bool> friendlyFire;
        internal static ConfigEntry<float> catchUpExperienceRatio;
        internal static ConfigEntry<bool> scaleEnemyCount;
        internal static ConfigEntry<float> enemyCountPerExtraPlayer;
        internal static ConfigEntry<float> maximumEnemyCountMultiplier;
        internal static ConfigEntry<int> playerLimit;
        internal static ConfigEntry<KeyboardShortcut> menuShortcut;
        internal static ConfigEntry<KeyboardShortcut> rescueShortcut;
        internal static ConfigEntry<KeyboardShortcut> autoPilotShortcut;
        internal static ConfigEntry<int> autoChoiceStrategy;
        internal static ConfigEntry<string> autoChoicePresets;
        internal static ConfigEntry<string> autoWeaponPresets;
        internal static ConfigEntry<string> autoFloorPresets;
        internal static ConfigEntry<bool> autoArrangeInventory;
        internal static ConfigEntry<int> autoFullInventoryStrategy;
        internal static ConfigEntry<bool> autoDefend;
        private static ConfigEntry<bool> autoFloorDefaultsInitialized;
        internal static ConfigEntry<bool> autoReviveWhenClear;
        internal static ConfigEntry<bool> bossLifesteal;
        private Harmony harmony;
        private int lastShortcutFrame = -1;

        private void Awake()
        {
            playerLimit = Config.Bind(
                "Multiplayer",
                "PlayerLimit",
                16,
                new ConfigDescription(
                    "Maximum lobby and network player count.",
                    new AcceptableValueRange<int>(2, 250)));
            menuShortcut = Config.Bind(
                "Interface",
                "MenuShortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Shortcut used to open and close the Sephiria Together menu.");
            rescueShortcut = Config.Bind(
                "Interface",
                "RescueShortcut",
                new KeyboardShortcut(KeyCode.R),
                "Shortcut a downed player uses to request rescue from modded teammates.");
            autoPilotShortcut = Config.Bind(
                "Interface",
                "AutoPilotShortcut",
                new KeyboardShortcut(KeyCode.F9),
                "Shortcut used to enable or disable conservative AFK autopilot.");
            autoChoiceStrategy = Config.Bind(
                "Autopilot",
                "ChoiceStrategy",
                0,
                new ConfigDescription(
                    "0: prefer preset matches, 1: prefer heart-marked favorites, 2: always wait. Prefer modes fall back to a random highest-rarity reward.",
                    new AcceptableValueRange<int>(0, 2)));
            autoChoicePresets = Config.Bind(
                "Autopilot",
                "ChoicePresets",
                "FlameSword,Precision,WindSong",
                "Ordered reward item/category IDs preferred by autopilot. Configure this from the F8 menu.");
            autoWeaponPresets = Config.Bind(
                "Autopilot",
                "WeaponPresets",
                "",
                "Ordered weapon enhancement IDs preferred by autopilot. Configure this from the F8 menu.");
            autoFloorPresets = Config.Bind(
                "Autopilot",
                "FloorPresets",
                "floor:Anvil,floor:InventoryStorage,floor:Charm,floor:EXP",
                "Ordered next-floor event types preferred by autopilot. Configure this from the F8 menu.");
            autoFloorDefaultsInitialized = Config.Bind(
                "Autopilot",
                "FloorDefaultsInitialized",
                false,
                "Internal migration marker for the default next-floor priority.");
            if (!autoFloorDefaultsInitialized.Value)
            {
                if (string.IsNullOrWhiteSpace(autoFloorPresets.Value))
                    autoFloorPresets.Value = "floor:Anvil,floor:InventoryStorage,floor:Charm,floor:EXP";
                autoFloorDefaultsInitialized.Value = true;
                Config.Save();
            }
            autoArrangeInventory = Config.Bind(
                "Autopilot",
                "AutoArrangeInventory",
                false,
                "Automatically use the game's best charm-level inventory arranger while autopilot is enabled and out of combat.");
            autoFullInventoryStrategy = Config.Bind(
                "Autopilot",
                "FullInventoryStrategy",
                2,
                new ConfigDescription(
                    "0: never discard, 1: replace lower-rarity unfavorited Charms only, 2: also replace safe ordinary items.",
                    new AcceptableValueRange<int>(0, 2)));
            autoDefend = Config.Bind(
                "Autopilot",
                "AutoDefend",
                true,
                "Use supported vanilla weapon guard or parry inputs against predicted incoming attacks.");
            autoReviveWhenClear = Config.Bind(
                "Multiplayer",
                "AutoReviveWhenClear",
                false,
                "Automatically revive all downed players at 50% HP after no living hostile enemies remain.");
            bossLifesteal = Config.Bind(
                "Scaling",
                "BossLifesteal",
                true,
                "Allow Bosses and Minibosses to use the original hard-mode Blood Festival lifesteal after hitting players.");
            scalingStartsAbove = Config.Bind(
                "Scaling",
                "BaselinePlayers",
                4,
                new ConfigDescription(
                    "No extra scaling at or below this player count. Set to 0 to test scaling alone.",
                    new AcceptableValueRange<int>(0, 250)));
            healthPerExtraPlayer = Config.Bind(
                "Scaling",
                "HealthPerExtraPlayer",
                0.15f,
                new ConfigDescription(
                    "Extra final enemy health per player above BaselinePlayers. 0.15 means 15%.",
                    new AcceptableValueRange<float>(0f, 5f)));
            maximumMultiplier = Config.Bind(
                "Scaling",
                "MaximumExtraMultiplier",
                8f,
                new ConfigDescription(
                    "Maximum multiplier applied on top of the game's original health. Set to 0 for no cap.",
                    new AcceptableValueRange<float>(0f, 100f)));
            allowLowerProgressPlayers = Config.Bind(
                "Multiplayer",
                "AllowLowerProgressPlayers",
                true,
                "Allow players with lower quest progress to join a lobby hosted at a later chapter.");
            allowMidRunJoin = Config.Bind(
                "Multiplayer",
                "AllowMidRunJoin",
                true,
                "Allow new players to join after the host has started a dungeon. Existing players still use the normal reconnect path.");
            allowUngroupedStageTransition = Config.Bind(
                "Multiplayer",
                "AllowUngroupedStageTransition",
                false,
                "Allow the host to use a stage entrance without gathering every living player nearby.");
            breathingHeal = Config.Bind(
                "Multiplayer",
                "BreathingHeal",
                false,
                "Allow players to recover HP after leaving combat. Host-only; clients do not need the plugin.");
            friendlyFire = Config.Bind(
                "Multiplayer",
                "FriendlyFire",
                false,
                "Allow player attacks to damage other players. Damage is reduced to 1%, with 1 minimum and 5 maximum per hit.");
            catchUpExperienceRatio = Config.Bind(
                "Multiplayer",
                "CatchUpExperienceRatio",
                1f,
                new ConfigDescription(
                    "Fresh mid-run players catch up to this fraction of the other players' median cumulative experience.",
                    new AcceptableValueRange<float>(0f, 1f)));
            scaleEnemyCount = Config.Bind(
                "Scaling",
                "ScaleEnemyCount",
                true,
                "Increase enemy count beyond the baseline player count.");
            enemyCountPerExtraPlayer = Config.Bind(
                "Scaling",
                "EnemyCountPerExtraPlayer",
                0.08f,
                new ConfigDescription(
                    "Additional enemy-count multiplier per player above BaselinePlayers. 0.08 means 8%.",
                    new AcceptableValueRange<float>(0f, 1f)));
            maximumEnemyCountMultiplier = Config.Bind(
                "Scaling",
                "MaximumEnemyCountMultiplier",
                3f,
                new ConfigDescription(
                    "Maximum multiplier on top of the game's original enemy count.",
                    new AcceptableValueRange<float>(1f, 10f)));

            harmony = new Harmony(PluginGuid);
            MidRunJoin.Log = Logger;
            harmony.PatchAll();
            ApplyPlayerLimit();
            Logger.LogInfo(
                $"Enemy scaling loaded: +{Math.Max(0f, healthPerExtraPlayer.Value) * 100f:0.##}% original health " +
                $"per player above {Math.Max(0, scalingStartsAbove.Value)}. Host only.");
            Logger.LogInfo(
                $"Mid-run join={allowMidRunJoin.Value}, catch-up EXP={catchUpExperienceRatio.Value:P0}, " +
                $"lower-progress join={allowLowerProgressPlayers.Value}.");
        }

        internal static int BaselinePlayersValue => scalingStartsAbove.Value;
        internal static float HealthPerExtraPlayerValue => healthPerExtraPlayer.Value;
        internal static float MaximumMultiplierValue => maximumMultiplier.Value;
        internal static float EnemyCountPerExtraPlayerValue => enemyCountPerExtraPlayer.Value;
        internal static float MaximumEnemyCountMultiplierValue => maximumEnemyCountMultiplier.Value;
        internal static void SetBaselinePlayers(int value) => scalingStartsAbove.Value = Mathf.Clamp(value, 0, 250);
        internal static void SetHealthPerExtraPlayer(float value) => healthPerExtraPlayer.Value = Mathf.Clamp(value, 0f, 5f);
        internal static void SetMaximumMultiplier(float value) => maximumMultiplier.Value = Mathf.Clamp(value, 0f, 100f);
        internal static void SetEnemyCountPerExtraPlayer(float value) => enemyCountPerExtraPlayer.Value = Mathf.Clamp(value, 0f, 1f);
        internal static void SetMaximumEnemyCountMultiplier(float value) => maximumEnemyCountMultiplier.Value = Mathf.Clamp(value, 1f, 10f);
        internal static void SaveSettings() => Instance?.Config.Save();
        internal static void LogInfo(string message) => Instance?.Logger.LogInfo(message);

        internal static void ApplyPlayerLimit()
        {
            OptionsBinding instance = OptionsBinding.Instance;
            if (instance != null && instance.Options != null)
            {
                instance.Options.SetInt("AllowedMultiplayerMember", PlayerLimit.CurrentLimit);
                instance.Options.Save();
            }

            foreach (NetworkManager manager in Resources.FindObjectsOfTypeAll<NetworkManager>())
            {
                manager.maxConnections = PlayerLimit.CurrentLimit;
            }
        }

        private void OnDestroy()
        {
            CoopMenu.Close();
            CoopMenu.ResetClientCompensation();
            harmony?.UnpatchSelf();
            MidRunJoin.ClearConnections();
            BreathingHealPatch.Clear();
            AutoRevivePatch.Clear();
            CatchUpRewards.ClearClientState();
            CatchUpRewards.ClearServerState();
            AutoPilot.Clear();
        }

        private void OnGUI()
        {
            HandleGuiShortcuts();
            RescueAlerts.Draw();
            VersionReminder.Draw();
            AutoPilot.Draw();
            CoopMenu.Draw();
        }

        private void HandleGuiShortcuts()
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.KeyDown || lastShortcutFrame == Time.frameCount)
                return;
            if (current.keyCode != KeyCode.F8 && current.keyCode != KeyCode.F9) return;
            lastShortcutFrame = Time.frameCount;
            if (current.keyCode == KeyCode.F8)
            {
                LogInfo($"GUI shortcut pressed: menu, open={CoopMenu.IsOpen}.");
                CoopMenu.Toggle();
                current.Use();
                return;
            }
            if (!CoopMenu.IsCapturingShortcut && !CoopMenu.IsOpen)
            {
                LogInfo("GUI shortcut pressed: autoplay.");
                AutoPilot.Toggle();
                current.Use();
            }
        }

        private void Update()
        {
            bool menuPressed = menuShortcut.Value.IsDown() ||
                               WasFallbackFunctionKeyPressed(KeyCode.F8, Keyboard.current?.f8Key);
            bool autoPilotPressed = autoPilotShortcut.Value.IsDown() ||
                                    WasFallbackFunctionKeyPressed(KeyCode.F9, Keyboard.current?.f9Key);
            if (!CoopMenu.IsCapturingShortcut && menuPressed)
            {
                LogInfo($"Shortcut pressed: menu={menuShortcut.Value}, open={CoopMenu.IsOpen}.");
                CoopMenu.Toggle();
            }
            if (!CoopMenu.IsCapturingShortcut && !CoopMenu.IsOpen && autoPilotPressed)
            {
                LogInfo($"Shortcut pressed: autoplay={autoPilotShortcut.Value}.");
                AutoPilot.Toggle();
            }
            if (!CoopMenu.IsCapturingShortcut && !CoopMenu.IsOpen) RescueAlerts.Update();
            VersionReminder.Update();
        }

        private static bool WasFallbackFunctionKeyPressed(KeyCode legacyKey, KeyControl inputKey)
        {
            return Input.GetKeyDown(legacyKey) || inputKey != null && inputKey.wasPressedThisFrame;
        }

        internal static void ScheduleScale(UnitAvatar avatar)
        {
            Plugin instance = Instance;
            if (instance != null && instance.isActiveAndEnabled && NetworkServer.active && avatar != null && !(avatar is PlayerAvatar))
            {
                instance.StartCoroutine(ScaleAfterInitialization(avatar));
            }
        }

        private static IEnumerator ScaleAfterInitialization(UnitAvatar avatar)
        {
            // Spawners apply stage, difficulty and multiplayer health over several calls.
            yield return null;
            yield return new WaitForEndOfFrame();

            if (!NetworkServer.active || avatar == null || avatar is PlayerAvatar || avatar.monsterType == EMonsterType.Dummy)
            {
                yield break;
            }

            if (avatar.netId == 0 || avatar.GetComponent<EnemyScalingMarker>() != null || !IsHostileToPlayers(avatar))
            {
                yield break;
            }

            int playerCount = CountActivePlayers();
            int extraPlayers = Math.Max(0, playerCount - Math.Max(0, scalingStartsAbove.Value));
            if (extraPlayers == 0)
            {
                yield break;
            }

            float multiplier = 1f + Math.Max(0f, healthPerExtraPlayer.Value) * extraPlayers;
            if (maximumMultiplier.Value > 0f)
            {
                multiplier = Math.Min(multiplier, Math.Max(1f, maximumMultiplier.Value));
            }

            float newBaseMaxHp = avatar.maxHp * multiplier;
            if (!IsFinite(multiplier) || !IsFinite(newBaseMaxHp) || newBaseMaxHp <= 0f)
            {
                Instance.Logger.LogWarning($"Skipped invalid health scaling for {avatar.name}: x{multiplier}.");
                yield break;
            }

            float hpRatio = avatar.MaxHp > 0f ? Math.Max(0f, avatar.hp / avatar.MaxHp) : 1f;
            avatar.NetworkmaxHp = newBaseMaxHp;
            avatar.SetHp(avatar.MaxHp * hpRatio);
            avatar.gameObject.AddComponent<EnemyScalingMarker>();

            Instance.Logger.LogDebug(
                $"Scaled {avatar.name} for {playerCount} players: x{multiplier:0.##}, HP {avatar.hp:0}/{avatar.MaxHp:0}.");
        }

        private static bool IsHostileToPlayers(UnitAvatar avatar)
        {
            if (PlayerSpawner.MultiplayerList == null)
            {
                return false;
            }

            long hostileLayers = avatar.GetHostileFactionLayers(EDamageFromType.None);
            foreach (PlayerSpawner playerSpawner in PlayerSpawner.MultiplayerList)
            {
                PlayerAvatar player = playerSpawner != null ? playerSpawner.PlayerAvatar : null;
                if (player != null && CombatManager.ContainsAttackableFaction(hostileLayers, player.faction))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountActivePlayers()
        {
            int count = 0;
            if (PlayerSpawner.MultiplayerList != null)
            {
                foreach (PlayerSpawner playerSpawner in PlayerSpawner.MultiplayerList)
                {
                    if (playerSpawner != null && playerSpawner.PlayerAvatar != null)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static Plugin InstanceForPatches => Instance;

        private static Plugin Instance { get; set; }

        private void OnEnable()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    internal sealed class EnemyScalingMarker : MonoBehaviour
    {
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.OnStartServer))]
    internal static class UnitAvatarStartServerPatch
    {
        private static void Postfix(UnitAvatar __instance)
        {
            Plugin.ScheduleScale(__instance);
        }
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.ChangeFaction))]
    internal static class UnitAvatarChangeFactionPatch
    {
        private static void Postfix(UnitAvatar __instance)
        {
            Plugin.ScheduleScale(__instance);
        }
    }
}
