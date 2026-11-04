using System;
using System.Collections;
using System.Linq;
#if WORKSHOP
using SephiriaTogether.WorkshopSupport;
#else
using BepInEx;
using BepInEx.Configuration;
#endif
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
#if WORKSHOP
    public sealed class Plugin : MonoBehaviour
#else
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
#endif
    {
        public const string PluginGuid = "com.sephiriamods.sephiriatogether";
        public const string PluginName = "Sephiria Together";
        public const string PluginVersion = "3.9.9";

        private static ConfigEntry<int> scalingStartsAbove;
        private static ConfigEntry<float> baseEnemyMultiplier;
        private static ConfigEntry<float> healthPerExtraPlayer;
        private static ConfigEntry<float> maximumMultiplier;
        internal static ConfigEntry<bool> allowLowerProgressPlayers;
        internal static ConfigEntry<bool> allowMidRunJoin;
        internal static ConfigEntry<bool> allowUngroupedStageTransition;
        internal static ConfigEntry<bool> breathingHeal;
        internal static ConfigEntry<bool> friendlyFire;
        internal static ConfigEntry<bool> allowAttackingMerchants;
        internal static ConfigEntry<bool> scaleEnemyCount;
        internal static ConfigEntry<float> enemyCountPerExtraPlayer;
        internal static ConfigEntry<float> maximumEnemyCountMultiplier;
        internal static ConfigEntry<bool> offlineDirectModeEnabled;
        internal static ConfigEntry<int> playerLimit;
        internal static ConfigEntry<KeyboardShortcut> menuShortcut;
        internal static ConfigEntry<KeyboardShortcut> rescueShortcut;
        internal static ConfigEntry<bool> reviveWhenClear;
        internal static ConfigEntry<bool> bossLifesteal;
        internal static ConfigEntry<bool> antiCheat;
        internal static ConfigEntry<bool> directModeEnabled;
        internal static ConfigEntry<int> directPort;
        private Harmony harmony;
        private bool initialized;
        private bool ownsRuntime;
        private const string RuntimeSlot = PluginGuid + ".Runtime";

#if WORKSHOP
        internal ConfigFile Config { get; private set; }
        internal ModLogger Logger { get; } = new ModLogger();

        internal void InitializeForWorkshop(ModOptions options)
        {
            Config = new ConfigFile(options);
            InitializeRuntime();
        }
#endif

        private void Awake()
        {
#if !WORKSHOP
            try { InitializeRuntime(); }
            catch
            {
                enabled = false;
                throw;
            }
#endif
        }

        private void InitializeRuntime()
        {
            if (initialized) return;
            // The two builds have different assembly names but share one runtime
            // and Harmony owner. Also catches already-patched older BepInEx builds.
            lock (AppDomain.CurrentDomain)
            {
                object owner = AppDomain.CurrentDomain.GetData(RuntimeSlot);
                bool legacyRuntime = Resources.FindObjectsOfTypeAll<MonoBehaviour>().Any(component =>
                    component != null && component != this && component.isActiveAndEnabled &&
                    component.gameObject.scene.IsValid() && component.GetType().FullName == typeof(Plugin).FullName);
                if (owner is UnityEngine.Object live && live != null || legacyRuntime || Harmony.HasAnyPatches(PluginGuid))
                    throw new InvalidOperationException("Sephiria Together is already running. Enable only one of the BepInEx or Workshop builds and restart the game.");
                AppDomain.CurrentDomain.SetData(RuntimeSlot, this);
                ownsRuntime = true;
                Instance = this;
            }
            try
            {
                BindSettings();
                harmony = new Harmony(PluginGuid);
                MidRunJoin.Log = Logger;
                harmony.PatchAll(typeof(Plugin).Assembly);
                ApplyPlayerLimit();
                initialized = true;
                Logger.LogInfo($"Sephiria Together {PluginVersion} initialized from {GetType().Assembly.GetName().Name}, game={Application.version}.");
                Logger.LogInfo(
                    $"Enemy scaling loaded: base x{BaseEnemyMultiplierValue:0.##}, " +
                    $"+{Math.Max(0f, healthPerExtraPlayer.Value) * 100f:0.##}% original health " +
                    $"per player above {Math.Max(0, scalingStartsAbove.Value)}. Host only.");
                Logger.LogInfo(
                    $"Mid-run join={allowMidRunJoin.Value}, catch-up EXP=100%, " +
                    $"lower-progress join={allowLowerProgressPlayers.Value}, merchant attacks={allowAttackingMerchants.Value}, " +
                    $"anti-cheat={antiCheat.Value}.");
            }
            catch
            {
                ShutdownRuntime();
                throw;
            }
        }

        private void BindSettings()
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
                "Use the TCP/IP transport before a network session starts while Steam is online.");
            offlineDirectModeEnabled = Config.Bind(
                "DirectConnect",
                "OfflineEnabled",
                true,
                "Use the TCP/IP transport before a network session starts when Steam is offline.");
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
            allowAttackingMerchants = Config.Bind(
                "Multiplayer",
                "AllowAttackingMerchants",
                false,
                "Allow players and their followers to damage merchants. Default: false.");
            antiCheat = Config.Bind(
                "Multiplayer",
                "EnableAntiCheat",
                false,
                "Enable the basic host-side filter for direct remote money changes and direct item writes. Default: false.");
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
        internal static bool IsShortcutDown(KeyboardShortcut shortcut) =>
            ShortcutInput.IsDown(shortcut.MainKey, shortcut.Modifiers);
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

        private void OnDestroy() => ShutdownRuntime();

        internal void ShutdownRuntime()
        {
            if (!ownsRuntime) return;
            initialized = false;
            try
            {
                StopAllCoroutines();
                LanRoomDiscovery.StopListening();
#if WORKSHOP
                harmony?.UnpatchAll(PluginGuid);
#else
                harmony?.UnpatchSelf();
#endif
                UnregisterMessages();
                CoopMenu.Close();
                CoopMenu.ResetClientCompensation();
                MidRunJoin.ClearConnections();
                MidRunJoin.Log = null;
                BreathingHealPatch.Clear();
                AutoRevivePatch.Clear();
                AntiCheat.Clear();
                CatchUpRewards.ClearClientState();
                CatchUpRewards.ClearServerConnectionState();
                VersionCompatibility.ClearConnections();
                VersionCompatibility.AbortClientJoin();
                StartProgressSelection.Clear();
                RescueAlerts.ClearClient();
                MoneyTransfer.ClearClient();
                ThirdPartyCompatibility.Clear();
                PlayerLimit.ClearServerCapacity();
                IpTransport.Shutdown();
            }
            finally
            {
                lock (AppDomain.CurrentDomain)
                {
                    if (ReferenceEquals(AppDomain.CurrentDomain.GetData(RuntimeSlot), this))
                        AppDomain.CurrentDomain.SetData(RuntimeSlot, null);
                }
                if (Instance == this) Instance = null;
                ownsRuntime = false;
            }
        }

        private static void UnregisterMessages()
        {
            NetworkServer.UnregisterHandler<VersionCompatibilityHelloMessage>();
            NetworkServer.UnregisterHandler<CatchUpHelloMessage>();
            NetworkServer.UnregisterHandler<CatchUpClaimMessage>();
            NetworkServer.UnregisterHandler<FreshPocketItemsMessage>();
            NetworkServer.UnregisterHandler<RescueRequestMessage>();
            NetworkServer.UnregisterHandler<MoneyTransferRequest>();
            NetworkServer.UnregisterHandler<StartProgressReportMessage>();
            NetworkClient.UnregisterHandler<VersionCompatibilityNoticeMessage>();
            NetworkClient.UnregisterHandler<CatchUpOfferMessage>();
            NetworkClient.UnregisterHandler<RescueAlertMessage>();
            NetworkClient.UnregisterHandler<MoneyTransferResult>();
        }

        private void OnGUI()
        {
            if (!initialized || Instance != this) return;
            RescueAlerts.Draw();
            VersionReminder.Draw();
            CoopMenu.Draw();
        }

        private void Update()
        {
            if (!initialized || Instance != this) return;
            ThirdPartyCompatibility.Tick();
            HandleMenuShortcut();
            if (!CoopMenu.IsCapturingShortcut && !CoopMenu.IsOpen) RescueAlerts.Update();
            VersionReminder.Update();
            MoneyTransfer.Tick();
            StartProgressSelection.Tick();
            LanRoomDiscovery.Tick();
            IpTransport.EnsureInstalled();
        }

        private static void HandleMenuShortcut()
        {
            // A native mod can activate after our OnEnable in the same load
            // plan. Recheck before using the saved key, not one poll later.
            if (!CoopMenu.IsCapturingShortcut && IsShortcutDown(menuShortcut.Value))
                ThirdPartyCompatibility.Tick(force: true);
            KeyboardShortcut effective = ThirdPartyCompatibility.EffectiveMenuShortcut;
            if (CoopMenu.IsCapturingShortcut || !IsShortcutDown(effective)) return;
            LogInfo($"Shortcut pressed: menu={effective}, configured={menuShortcut.Value}, open={CoopMenu.IsOpen}.");
            CoopMenu.Toggle();
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
            if (!initialized || !ownsRuntime) return;
            Instance = this;
            ThirdPartyCompatibility.Tick(force: true);
            LanRoomDiscovery.StartListening();
            StartCoroutine(InstallIpTransportWhenReady());
            // Workshop DLL activation can follow NetworkManager startup. Install
            // handlers for an already-active session as well as future callbacks.
            if (NetworkServer.active)
            {
                VersionCompatibility.RegisterServerMessages();
                CatchUpRewards.RegisterServerMessages();
            }
            if (NetworkClient.active)
            {
                CatchUpRewards.RegisterClientMessages();
                VersionCompatibility.RegisterClientMessages();
            }
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
                StopAllCoroutines();
                LanRoomDiscovery.StopListening();
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
