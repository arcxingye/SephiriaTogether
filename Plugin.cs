using System;
using System.Collections;
using System.Linq;
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
        public const string PluginVersion = "3.8.0";

        private static ConfigEntry<int> scalingStartsAbove;
        private static ConfigEntry<float> baseEnemyMultiplier;
        private static ConfigEntry<float> healthPerExtraPlayer;
        private static ConfigEntry<float> maximumMultiplier;
        internal static ConfigEntry<bool> allowLowerProgressPlayers;
        internal static ConfigEntry<bool> allowMidRunJoin;
        internal static ConfigEntry<bool> allowUngroupedStageTransition;
        internal static ConfigEntry<bool> breathingHeal;
        internal static ConfigEntry<bool> friendlyFire;
        internal static ConfigEntry<bool> scaleEnemyCount;
        internal static ConfigEntry<float> enemyCountPerExtraPlayer;
        internal static ConfigEntry<float> maximumEnemyCountMultiplier;
        internal static ConfigEntry<int> playerLimit;
        internal static ConfigEntry<KeyboardShortcut> menuShortcut;
        internal static ConfigEntry<KeyboardShortcut> rescueShortcut;
        internal static ConfigEntry<bool> reviveWhenClear;
        internal static ConfigEntry<bool> bossLifesteal;
        internal static ConfigEntry<bool> directModeEnabled;
        internal static ConfigEntry<int> directPort;
        private Harmony harmony;

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
                "Required shortcut used to open and close the Sephiria Together menu.");
            if (!IsShortcutBound(menuShortcut.Value))
                menuShortcut.Value = new KeyboardShortcut(KeyCode.F8);
            rescueShortcut = Config.Bind(
                "Interface",
                "RescueShortcut",
                new KeyboardShortcut(KeyCode.R),
                "Optional shortcut a downed player uses to request rescue from modded teammates.");
            reviveWhenClear = Config.Bind(
                "Multiplayer",
                "AutoReviveWhenClear",
                false,
                "Automatically revive all downed players at 50% HP after no living hostile enemies remain.");
            directModeEnabled = Config.Bind(
                "DirectConnect",
                "Enabled",
                false,
                "Use the TCP/IP transport before a network session starts. Offline environments enable it automatically.");
            directPort = Config.Bind(
                    "DirectConnect",
                    "Port",
                    7777,
                new ConfigDescription(
                    "TCP port used by the IP transport before a network session starts.",
                    new AcceptableValueRange<int>(1, 65535)));
            CleanRemovedSettings();
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
            baseEnemyMultiplier = Config.Bind(
                "Scaling",
                "BaseEnemyMultiplier",
                1f,
                new ConfigDescription(
                    "Base multiplier applied to original enemy health and wave size before extra-player scaling.",
                    new AcceptableValueRange<float>(0.05f, 4f)));
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
                $"Enemy scaling loaded: base x{BaseEnemyMultiplierValue:0.##}, " +
                $"+{Math.Max(0f, healthPerExtraPlayer.Value) * 100f:0.##}% original health " +
                $"per player above {Math.Max(0, scalingStartsAbove.Value)}. Host only.");
            Logger.LogInfo(
                $"Mid-run join={allowMidRunJoin.Value}, catch-up EXP=100%, " +
                $"lower-progress join={allowLowerProgressPlayers.Value}.");
        }

        internal static int BaselinePlayersValue => scalingStartsAbove.Value;
        internal static float BaseEnemyMultiplierValue => baseEnemyMultiplier.Value;
        internal static float HealthPerExtraPlayerValue => healthPerExtraPlayer.Value;
        internal static float MaximumMultiplierValue => maximumMultiplier.Value;
        internal static float EnemyCountPerExtraPlayerValue => enemyCountPerExtraPlayer.Value;
        internal static float MaximumEnemyCountMultiplierValue => maximumEnemyCountMultiplier.Value;
        internal static int PlayerCount => Math.Max(1,
            PlayerSpawner.MultiplayerList?.Count(player => player?.PlayerAvatar != null) ?? 1);
        internal static void SetBaselinePlayers(int value) => scalingStartsAbove.Value = Mathf.Clamp(value, 0, 250);
        internal static void SetBaseEnemyMultiplier(float value) => baseEnemyMultiplier.Value = Mathf.Clamp(value, 0.05f, 4f);
        internal static void SetHealthPerExtraPlayer(float value) => healthPerExtraPlayer.Value = Mathf.Clamp(value, 0f, 5f);
        internal static void SetMaximumMultiplier(float value) => maximumMultiplier.Value = Mathf.Clamp(value, 0f, 100f);
        internal static void SetEnemyCountPerExtraPlayer(float value) => enemyCountPerExtraPlayer.Value = Mathf.Clamp(value, 0f, 1f);
        internal static void SetMaximumEnemyCountMultiplier(float value) => maximumEnemyCountMultiplier.Value = Mathf.Clamp(value, 1f, 10f);
        internal static void SaveSettings() => Instance?.Config.Save();
        internal static bool IsShortcutBound(KeyboardShortcut shortcut) => shortcut.MainKey != KeyCode.None;
        internal static string FormatShortcut(KeyboardShortcut shortcut) =>
            IsShortcutBound(shortcut) ? shortcut.ToString() : MenuText.Get("ShortcutUnbound");
        internal static void LogInfo(string message) =>
            Instance?.Logger.LogInfo($"[{DateTime.Now:HH:mm:ss.fff}] {message}");

        private void CleanRemovedSettings()
        {
            bool saveOnSet = Config.SaveOnConfigSet;
            Config.SaveOnConfigSet = false;
            try
            {
                RemoveSetting("Interface", "AutoPilotShortcut", KeyboardShortcut.Empty);
                RemoveSetting("Autopilot", "ChoiceStrategy", 0);
                RemoveSetting("Autopilot", "ChoicePresets", "");
                RemoveSetting("Autopilot", "WeaponPresets", "");
                RemoveSetting("Autopilot", "MiraclePresets", "");
                RemoveSetting("Autopilot", "MiracleDefaultsInitialized", false);
                RemoveSetting("Autopilot", "FloorPresets", "");
                RemoveSetting("Autopilot", "FloorDefaultsInitialized", false);
                RemoveSetting("Autopilot", "AutoArrangeInventory", false);
                RemoveSetting("Autopilot", "FullInventoryStrategy", 0);
                RemoveSetting("Autopilot", "AutoDefend", false);
                RemoveSetting("Autopilot", "AttackMode", 0);
                RemoveSetting("Multiplayer", "CatchUpExperienceRatio", 1f);
                Config.Save();
            }
            finally
            {
                Config.SaveOnConfigSet = saveOnSet;
            }
        }

        private void RemoveSetting<T>(string section, string key, T defaultValue)
        {
            ConfigDefinition definition = new ConfigDefinition(section, key);
            Config.Bind(definition, defaultValue);
            Config.Remove(definition);
        }

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
            LanRoomDiscovery.StopListening();
            harmony?.UnpatchSelf();
            MidRunJoin.ClearConnections();
            BreathingHealPatch.Clear();
            AutoRevivePatch.Clear();
            CatchUpRewards.ClearClientState();
            VersionReminder.Clear();
            CatchUpRewards.ClearServerConnectionState();
        }

        private void OnGUI()
        {
            HandleMenuShortcutEvent();
            RescueAlerts.Draw();
            VersionReminder.Draw();
            CoopMenu.Draw();
        }

        private void Update()
        {
            if (!CoopMenu.IsCapturingShortcut && !CoopMenu.IsOpen) RescueAlerts.Update();
            VersionReminder.Update();
            MoneyTransfer.Tick();
            StartProgressSelection.Tick();
            LanRoomDiscovery.Tick();
            IpTransport.EnsureInstalled();
        }

        private static void HandleMenuShortcutEvent()
        {
            Event current = Event.current;
            if (CoopMenu.IsCapturingShortcut || current == null || current.type != EventType.KeyDown ||
                !MatchesShortcut(menuShortcut.Value, current)) return;
            current.Use();
            LogInfo($"Shortcut pressed: menu={menuShortcut.Value}, open={CoopMenu.IsOpen}.");
            CoopMenu.Toggle();
        }

        private static bool MatchesShortcut(KeyboardShortcut shortcut, Event current)
        {
            if (shortcut.MainKey == KeyCode.None || current.keyCode != shortcut.MainKey) return false;
            bool control = false;
            bool shift = false;
            bool alt = false;
            bool command = false;
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (modifier == KeyCode.LeftControl || modifier == KeyCode.RightControl) control = true;
                else if (modifier == KeyCode.LeftShift || modifier == KeyCode.RightShift) shift = true;
                else if (modifier == KeyCode.LeftAlt || modifier == KeyCode.RightAlt) alt = true;
                else if (modifier == KeyCode.LeftCommand || modifier == KeyCode.RightCommand ||
                         modifier == KeyCode.LeftWindows || modifier == KeyCode.RightWindows) command = true;
                else return false;
            }
            return current.control == control && current.shift == shift && current.alt == alt &&
                   current.command == command;
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

            int playerCount = PlayerCount;
            int extraPlayers = Math.Max(0, playerCount - Math.Max(0, scalingStartsAbove.Value));
            float multiplier = Math.Max(0.05f, baseEnemyMultiplier.Value) *
                               (1f + Math.Max(0f, healthPerExtraPlayer.Value) * extraPlayers);
            if (maximumMultiplier.Value > 0f)
            {
                multiplier = Math.Min(multiplier, Math.Max(0.05f, maximumMultiplier.Value));
            }
            if (Mathf.Approximately(multiplier, 1f))
            {
                yield break;
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static Plugin InstanceForPatches => Instance;

        private static Plugin Instance { get; set; }

        private void OnEnable()
        {
            Instance = this;
            LanRoomDiscovery.StartListening();
            StartCoroutine(InstallIpTransportWhenReady());
        }

        private IEnumerator InstallIpTransportWhenReady()
        {
            for (int frame = 0; frame < 180; frame++)
            {
                if (NetworkManager.singleton is HorayNetworkManager manager)
                {
                    IpTransport.Install(manager);
                    yield break;
                }
                yield return null;
            }
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
