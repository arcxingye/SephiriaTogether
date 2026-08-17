using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SephiriaTogether
{
    internal static class CoopMenu
    {
        private static bool open;
        private static Rect window = new Rect(24f, 24f, 540f, 620f);
        private static GUIStyle title;
        private static GUIStyle section;
        private static GUIStyle windowStyle;
        private static GUIStyle card;
        private static GUIStyle playerCard;
        private static GUIStyle body;
        private static GUIStyle muted;
        private static GUIStyle button;
        private static GUIStyle primaryButton;
        private static GUIStyle dangerButton;
        private static GUIStyle input;
        private static GUIStyle badge;
        private static GUIStyle tagButton;
        private static GUIStyle toggleOn;
        private static GUIStyle toggleOff;
        private static Texture2D windowTexture;
        private static Texture2D cardTexture;
        private static Texture2D playerCardTexture;
        private static Texture2D buttonTexture;
        private static Texture2D buttonHoverTexture;
        private static Texture2D primaryTexture;
        private static Texture2D primaryHoverTexture;
        private static Texture2D dangerTexture;
        private static Texture2D inputTexture;
        private static bool previousCursorVisible;
        private static CursorLockMode previousCursorLockMode;
        private static PlayerInputController blockedController;
        private static bool previousInputBlock;
        private static string playerLimitText;
        private static Vector2 scroll;
        private static Vector2 rewardDropdownScroll;
        private static Vector2 weaponDropdownScroll;
        private static readonly List<PresetOption> RewardPresetOptions = new List<PresetOption>();
        private static readonly List<PresetOption> WeaponPresetOptions = new List<PresetOption>();
        private static readonly List<PresetOption> FloorPresetOptions = new List<PresetOption>();
        private static int selectedRewardPreset;
        private static int selectedWeaponPreset;
        private static int selectedFloorPreset;
        private static bool showRewardDropdown;
        private static bool showWeaponDropdown;
        private static bool showFloorDropdown;
        private static Vector2 floorDropdownScroll;
        private static string weaponPresetStatus;
        private static bool showAdvancedScaling;
        private static int selectedTab;
        private static int hostRulesTab;
        private static int autoPresetTab;
        private static int capturingShortcut;

        internal static bool IsCapturingShortcut => capturingShortcut != 0;
        internal static bool IsOpen => open;

        internal static void Toggle()
        {
            open = !open;
            if (open)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLockMode = Cursor.lockState;
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                blockedController = PlayerInputController.Instance;
                if (blockedController != null && blockedController.HasAvatar)
                {
                    previousInputBlock = blockedController.BlockAvatarInput;
                    blockedController.BlockAvatarInput = true;
                }
                playerLimitText = PlayerLimit.CurrentLimit.ToString();
                RefreshPresetOptions();
            }
            else
            {
                Cursor.visible = previousCursorVisible;
                Cursor.lockState = previousCursorLockMode;
                RestoreInput();
            }
        }

        internal static void Close()
        {
            if (open)
            {
                open = false;
                Cursor.visible = previousCursorVisible;
                Cursor.lockState = previousCursorLockMode;
                RestoreInput();
            }
        }

        internal static void Draw()
        {
            if (!open || !Application.isPlaying) return;
            CaptureShortcutInput();
            if ((blockedController == null || !blockedController.HasAvatar) &&
                PlayerInputController.Instance != null && PlayerInputController.Instance.HasAvatar)
            {
                blockedController = PlayerInputController.Instance;
                previousInputBlock = blockedController.BlockAvatarInput;
                blockedController.BlockAvatarInput = true;
            }
            window.width = Mathf.Min(540f, Mathf.Max(360f, Screen.width - 24f));
            window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, Screen.width - window.width));
            window.height = Mathf.Min(680f, Mathf.Max(360f, Screen.height - 24f));
            window.y = Mathf.Clamp(window.y, 0f, Mathf.Max(0f, Screen.height - window.height));

            EnsureStyles();
            window = GUI.Window(100100, window, DrawWindow, GUIContent.none, windowStyle);
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal(GUILayout.Height(62f));
            GUILayout.BeginVertical();
            GUILayout.Label(MenuText.Get("Title"), title);
            GUILayout.Label(MenuText.Get("Subtitle"), muted);
            GUILayout.Label(MenuText.Get("HostSettings"), muted);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", button, GUILayout.Width(36f), GUILayout.Height(32f))) Close();
            GUILayout.EndHorizontal();
            DrawDivider();
            DrawTabs();
            if (selectedTab == 1)
            {
                DrawAutoPilotPage();
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, window.width - 52f, 70f));
                return;
            }
            if (!NetworkServer.active)
            {
                DrawClientPage();
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, window.width - 52f, 54f));
                return;
            }
            if (selectedTab != 0)
            {
                DrawHostPage();
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, window.width - 52f, 54f));
                return;
            }
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Space(8f);
            GUILayout.Label(MenuText.Get("NextSpawn"), muted);
            DrawValue(MenuText.Get("MenuShortcut"), Plugin.menuShortcut.Value.ToString(), 180f);
            if (capturingShortcut == 1)
            {
                GUILayout.Label(MenuText.Get("PressNewShortcut"), muted);
                if (GUILayout.Button(MenuText.Get("CancelShortcut"), button, GUILayout.Height(30f)))
                {
                    capturingShortcut = 0;
                }
            }
            else if (GUILayout.Button(MenuText.Get("ChangeShortcut"), button, GUILayout.Height(30f)))
            {
                capturingShortcut = 1;
            }
            DrawValue(MenuText.Get("RescueShortcut"), Plugin.rescueShortcut.Value.ToString(), 180f);
            if (capturingShortcut == 2)
            {
                GUILayout.Label(MenuText.Get("PressNewRescueShortcut"), muted);
                if (GUILayout.Button(MenuText.Get("CancelShortcut"), button, GUILayout.Height(30f))) capturingShortcut = 0;
            }
            else if (GUILayout.Button(MenuText.Get("ChangeRescueShortcut"), button, GUILayout.Height(30f)))
            {
                capturingShortcut = 2;
            }
            DrawHostRuleTabs();

            if (hostRulesTab == 0)
            {
            BeginSection(MenuText.Get("Multiplayer"));
            if (JoinProgressBypass.CanCreateLobbyForCurrentRun())
            {
                GUILayout.Label(MenuText.Get("ResumeLobbyHelp"), muted);
                if (GUILayout.Button(MenuText.Get("ResumeLobby"), primaryButton, GUILayout.Height(38f)))
                    JoinProgressBypass.OpenLobbyCreationForCurrentRun();
                GUILayout.Space(8f);
            }
            GUILayout.Label(MenuText.Get("PlayerLimit"), body);
            GUILayout.BeginHorizontal();
            playerLimitText = GUILayout.TextField(playerLimitText ?? PlayerLimit.CurrentLimit.ToString(), 3, input, GUILayout.Height(34f));
            if (GUILayout.Button(MenuText.Get("Apply"), primaryButton, GUILayout.Width(170f), GUILayout.Height(34f)) && int.TryParse(playerLimitText, out int requestedLimit))
            {
                PlayerLimit.SetLimit(requestedLimit);
                playerLimitText = PlayerLimit.CurrentLimit.ToString();
            }
            GUILayout.EndHorizontal();
            DrawToggle(MenuText.Get("LowerProgress"), Plugin.allowLowerProgressPlayers);
            DrawToggle(MenuText.Get("MidRun"), Plugin.allowMidRunJoin);
            DrawToggle(MenuText.Get("UngroupedTransition"), Plugin.allowUngroupedStageTransition);
            GUILayout.Label(MenuText.Get("UngroupedTransitionHelp"), muted);
            DrawToggle(MenuText.Get("BreathingHeal"), Plugin.breathingHeal);
            GUILayout.Label(MenuText.Get("BreathingHealHelp"), muted);
            DrawToggle(MenuText.Get("AutoReviveWhenClear"), Plugin.autoReviveWhenClear);
            GUILayout.Label(MenuText.Get("AutoReviveWhenClearHelp"), muted);
            DrawToggle(MenuText.Get("FriendlyFire"), Plugin.friendlyFire);
            GUILayout.Label(MenuText.Get("FriendlyFireHelp"), muted);
            GUILayout.Space(8f);
            DrawToggle(MenuText.Get("Catchup"), Plugin.catchUpExperienceRatio.Value > 0.5f,
                () => Plugin.catchUpExperienceRatio.Value = Plugin.catchUpExperienceRatio.Value > 0.5f ? 0f : 1f);
            EndSection();
            }

            if (hostRulesTab == 1)
            {
            BeginSection(MenuText.Get("EnemyScaling"));
            GUILayout.Label(MenuText.Get("ScalingHelp"), muted);
            GUILayout.Space(6f);
            GUILayout.Label(MenuText.Get("VanillaScaling"), section);
            GUILayout.Label(CatchUpRewards.BuildOriginalScalingSummary(), body);
            GUILayout.Space(6f);
            DrawToggle(MenuText.Get("BossLifesteal"), Plugin.bossLifesteal);
            GUILayout.Label(MenuText.Get("BossLifestealHelp"), muted);
            GUILayout.Space(8f);
            int currentPreset = GetPreset();
            DrawValue(MenuText.Get("CurrentPreset"), GetPresetName(currentPreset), 150f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(MenuText.Get("PresetOriginal"), currentPreset == 0 ? primaryButton : button, GUILayout.Height(34f))) ApplyScalingPreset(0);
            if (GUILayout.Button(MenuText.Get("PresetLight"), currentPreset == 1 ? primaryButton : button, GUILayout.Height(34f))) ApplyScalingPreset(1);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(MenuText.Get("PresetStandard"), currentPreset == 2 ? primaryButton : button, GUILayout.Height(34f))) ApplyScalingPreset(2);
            if (GUILayout.Button(MenuText.Get("PresetHigh"), currentPreset == 3 ? primaryButton : button, GUILayout.Height(34f))) ApplyScalingPreset(3);
            GUILayout.EndHorizontal();
            int activePlayers = Mathf.Max(1, PlayerSpawner.MultiplayerList != null ? PlayerSpawner.MultiplayerList.Count : 1);
            int extraPlayers = Mathf.Max(0, activePlayers - Plugin.BaselinePlayersValue);
            float healthMultiplier = 1f + extraPlayers * Plugin.HealthPerExtraPlayerValue;
            if (Plugin.MaximumMultiplierValue > 0f) healthMultiplier = Mathf.Min(healthMultiplier, Plugin.MaximumMultiplierValue);
            float countMultiplier = Plugin.scaleEnemyCount.Value
                ? Mathf.Min(Plugin.MaximumEnemyCountMultiplierValue, 1f + extraPlayers * Plugin.EnemyCountPerExtraPlayerValue)
                : 1f;
            GUILayout.BeginVertical(playerCard);
            GUILayout.Label(string.Format(MenuText.Get("ScalingPreviewPlayers"), activePlayers), section);
            DrawValue(MenuText.Get("PreviewHealth"), healthMultiplier.ToString("0.00") + "x");
            DrawValue(MenuText.Get("PreviewCount"), countMultiplier.ToString("0.00") + "x");
            GUILayout.Label(MenuText.Get("ScalingTiming"), muted);
            GUILayout.EndVertical();
            GUILayout.Space(6f);
            if (GUILayout.Button(showAdvancedScaling ? MenuText.Get("HideAdvanced") : MenuText.Get("ShowAdvanced"), button, GUILayout.Height(32f)))
            {
                showAdvancedScaling = !showAdvancedScaling;
            }
            if (showAdvancedScaling)
            {
                GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label(MenuText.Get("Baseline"), body);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("-", button, GUILayout.Width(36f), GUILayout.Height(30f))) Plugin.SetBaselinePlayers(Plugin.BaselinePlayersValue - 1);
            GUILayout.Label(Plugin.BaselinePlayersValue.ToString(), badge, GUILayout.Width(54f), GUILayout.Height(30f));
            if (GUILayout.Button("+", button, GUILayout.Width(36f), GUILayout.Height(30f))) Plugin.SetBaselinePlayers(Plugin.BaselinePlayersValue + 1);
            GUILayout.EndHorizontal();
            DrawValue(MenuText.Get("ExtraHp"), (Plugin.HealthPerExtraPlayerValue * 100f).ToString("0") + "%");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-5%", button, GUILayout.Height(32f))) Plugin.SetHealthPerExtraPlayer(Plugin.HealthPerExtraPlayerValue - 0.05f);
            if (GUILayout.Button("+5%", button, GUILayout.Height(32f))) Plugin.SetHealthPerExtraPlayer(Plugin.HealthPerExtraPlayerValue + 0.05f);
            GUILayout.EndHorizontal();
            DrawValue(MenuText.Get("HpCap"), Plugin.MaximumMultiplierValue.ToString("0.##") + "x");
            if (GUILayout.Button(MenuText.Get("CycleHpCap"), button, GUILayout.Height(32f)))
            {
                float value = Plugin.MaximumMultiplierValue;
                Plugin.SetMaximumMultiplier(value < 4.1f ? 8f : value < 8.1f ? 12f : value < 12.1f ? 0f : 4f);
            }
            DrawToggle(MenuText.Get("EnemyCount"), Plugin.scaleEnemyCount);
            DrawValue(MenuText.Get("CountPerPlayer"), (Plugin.EnemyCountPerExtraPlayerValue * 100f).ToString("0") + "%");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-2%", button, GUILayout.Height(32f))) Plugin.SetEnemyCountPerExtraPlayer(Plugin.EnemyCountPerExtraPlayerValue - 0.02f);
            if (GUILayout.Button("+2%", button, GUILayout.Height(32f))) Plugin.SetEnemyCountPerExtraPlayer(Plugin.EnemyCountPerExtraPlayerValue + 0.02f);
            GUILayout.EndHorizontal();
            DrawValue(MenuText.Get("CountCap"), Plugin.MaximumEnemyCountMultiplierValue.ToString("0.##") + "x");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-0.5x", button, GUILayout.Height(32f))) Plugin.SetMaximumEnemyCountMultiplier(Plugin.MaximumEnemyCountMultiplierValue - 0.5f);
            if (GUILayout.Button("+0.5x", button, GUILayout.Height(32f))) Plugin.SetMaximumEnemyCountMultiplier(Plugin.MaximumEnemyCountMultiplierValue + 0.5f);
            GUILayout.EndHorizontal();
            }
            EndSection();
            }

            if (hostRulesTab == 2)
            {
                DrawPlayers();
            }
            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(MenuText.Get("Save"), primaryButton, GUILayout.Height(38f))) Plugin.SaveSettings();
            if (GUILayout.Button(MenuText.Get("Close"), button, GUILayout.Height(38f))) Toggle();
            GUILayout.EndHorizontal();
            GUILayout.Space(8f);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, window.width - 52f, 70f));
        }

        private static void DrawClientCompensation()
        {
            PlayerAvatar player = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
            if (player == null || player.spawner == null)
            {
                GUILayout.Space(10f);
                GUILayout.BeginVertical(card);
                GUILayout.Label(MenuText.Get("ClientWaiting"), body);
                GUILayout.EndVertical();
                return;
            }

            CatchUpRewards.SendHello();
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Space(8f);
            BeginSection(MenuText.Get("ClientCompensation"));
            GUILayout.Label(MenuText.Get("ClientAutoGranted"), muted);
            if (CatchUpRewards.ClientClaimPending)
            {
                GUILayout.Label(MenuText.Get("ClaimPending"), muted);
            }
            else if (CatchUpRewards.ClientLastResult == 1)
            {
                GUILayout.Label(MenuText.Get("ClaimSuccess"), body);
            }
            else if (CatchUpRewards.ClientLastResult == 2)
            {
                GUILayout.Label(MenuText.Get("ClaimRejected"), muted);
            }
            DrawValue(MenuText.Get("WeaponCredits"), CatchUpRewards.ClientWeaponCredits.ToString());
            if (CatchUpRewards.ClientWeaponCredits > 0)
            {
                GUILayout.Label(MenuText.Get("WeaponAnvilHelp"), muted);
            }

            GUILayout.Space(8f);
            DrawValue(MenuText.Get("EnchantCredits"), CatchUpRewards.ClientEnchantCredits.ToString());
            if (CatchUpRewards.ClientEnchantCredits > 0) GUILayout.Label(MenuText.Get("EnchantObjectHelp"), muted);
            GUILayout.Space(8f);
            DrawValue(MenuText.Get("MiracleCredits"), CatchUpRewards.ClientMiracleCredits.ToString());
            if (CatchUpRewards.ClientMiracleCredits > 0) GUILayout.Label(MenuText.Get("MiracleObjectHelp"), muted);
            GUILayout.Space(8f);
            DrawValue(MenuText.Get("CharmCredits"), CatchUpRewards.ClientCharmCredits.ToString());
            if (CatchUpRewards.ClientCharmCredits > 0) GUILayout.Label(MenuText.Get("SephiriteObjectHelp"), muted);
            GUILayout.Space(8f);
            DrawValue(MenuText.Get("TabletCredits"), CatchUpRewards.ClientTabletCredits.ToString());
            if (CatchUpRewards.ClientTabletCredits > 0) GUILayout.Label(MenuText.Get("SephiriteObjectHelp"), muted);
            GUILayout.Space(8f);
            DrawValue(MenuText.Get("FusionCredits"), CatchUpRewards.ClientFusionCredits.ToString());
            if (CatchUpRewards.ClientFusionCredits > 0) GUILayout.Label(MenuText.Get("FusionObjectHelp"), muted);
            GUILayout.Space(8f);
            DrawValue(MenuText.Get("BossCredits"), CatchUpRewards.ClientBossCredits.ToString());
            if (CatchUpRewards.ClientBossCredits > 0) GUILayout.Label(MenuText.Get("BossObjectHelp"), muted);
            GUILayout.Space(8f);
            GUILayout.Label(string.Format(
                MenuText.Get("ClaimHistory"),
                CatchUpRewards.ClientWeaponClaimed,
                CatchUpRewards.ClientEnchantClaimed,
                CatchUpRewards.ClientMiracleClaimed,
                CatchUpRewards.ClientTabletClaimed,
                CatchUpRewards.ClientBossClaimed,
                CatchUpRewards.ClientCharmClaimed,
                CatchUpRewards.ClientFusionClaimed), muted);
            GUILayout.Space(8f);
            GUILayout.Label(MenuText.Get("ClientMissingRewards"), muted);
            EndSection();
            if (GUILayout.Button(MenuText.Get("Close"), button, GUILayout.Height(38f))) Toggle();
            GUILayout.EndScrollView();
        }

        private static void DrawTabs()
        {
            string[] labels = { "TabRules", "TabAutoPilot", "TabCompensation", "TabDiagnostics", "TabHistory" };
            GUILayout.BeginHorizontal();
            for (int i = 0; i < labels.Length; i++)
            {
                if (GUILayout.Button(MenuText.Get(labels[i]), selectedTab == i ? primaryButton : button, GUILayout.Height(32f)))
                {
                    selectedTab = i;
                    scroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            DrawDivider();
        }

        private static void DrawHostRuleTabs()
        {
            string[] labels = { "HostMultiplayerTab", "HostScalingTab", "HostPlayersTab" };
            GUILayout.BeginHorizontal();
            for (int i = 0; i < labels.Length; i++)
            {
                if (GUILayout.Button(MenuText.Get(labels[i]), hostRulesTab == i ? primaryButton : button,
                    GUILayout.Height(30f)))
                {
                    hostRulesTab = i;
                    scroll = Vector2.zero;
                }
            }
            GUILayout.EndHorizontal();
            DrawDivider();
        }

        private static void CaptureShortcutInput()
        {
            if (capturingShortcut == 0 || Event.current == null || Event.current.type != EventType.KeyDown ||
                Event.current.keyCode == KeyCode.None)
            {
                return;
            }

            KeyCode[] modifiers = new List<KeyCode>
            {
                Event.current.control ? KeyCode.LeftControl : KeyCode.None,
                Event.current.shift ? KeyCode.LeftShift : KeyCode.None,
                Event.current.alt ? KeyCode.LeftAlt : KeyCode.None,
                Event.current.command ? KeyCode.LeftCommand : KeyCode.None
            }.Where(key => key != KeyCode.None).ToArray();
            if (modifiers.Length == 0 &&
                (Event.current.keyCode == KeyCode.LeftControl || Event.current.keyCode == KeyCode.RightControl ||
                 Event.current.keyCode == KeyCode.LeftShift || Event.current.keyCode == KeyCode.RightShift ||
                 Event.current.keyCode == KeyCode.LeftAlt || Event.current.keyCode == KeyCode.RightAlt ||
                 Event.current.keyCode == KeyCode.LeftCommand || Event.current.keyCode == KeyCode.RightCommand))
            {
                return;
            }

            BepInEx.Configuration.KeyboardShortcut shortcut =
                new BepInEx.Configuration.KeyboardShortcut(Event.current.keyCode, modifiers);
            if (capturingShortcut == 1) Plugin.menuShortcut.Value = shortcut;
            else if (capturingShortcut == 2) Plugin.rescueShortcut.Value = shortcut;
            else Plugin.autoPilotShortcut.Value = shortcut;
            Plugin.SaveSettings();
            capturingShortcut = 0;
            Event.current.Use();
        }

        private static void DrawClientPage()
        {
            CatchUpRewards.SendHello();
            if (selectedTab == 2)
            {
                DrawClientCompensation();
                return;
            }
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Space(8f);
            string heading = selectedTab == 0 ? MenuText.Get("TabRules")
                : selectedTab == 3 ? MenuText.Get("TabDiagnostics") : MenuText.Get("TabHistory");
            string content = selectedTab == 0 ? CatchUpRewards.ClientRules
                : selectedTab == 3 ? CatchUpRewards.ClientDiagnostics : CatchUpRewards.ClientHistory;
            BeginSection(heading);
            GUILayout.Label(string.IsNullOrEmpty(content) ? MenuText.Get("NoData") : content, body);
            if (selectedTab == 3)
            {
                DrawDownloadLinks();
            }
            EndSection();
            GUILayout.EndScrollView();
        }

        private static void DrawAutoPilotPage()
        {
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Space(8f);
            BeginSection(MenuText.Get("TabAutoPilot"));
            DrawLocalAutoPilotControls();
            DrawAutoChoiceSettings();
            EndSection();
            GUILayout.EndScrollView();
        }

        private static void DrawAutoChoiceSettings()
        {
            GUILayout.Space(8f);
            GUILayout.Label(MenuText.Get("AutoChoiceStrategy"), section);
            GUILayout.BeginHorizontal();
            string[] labels = { "AutoChoicePresetFirst", "AutoChoiceFavoriteFirst", "AutoChoiceWait" };
            for (int i = 0; i < labels.Length; i++)
                if (GUILayout.Button(MenuText.Get(labels[i]), Plugin.autoChoiceStrategy.Value == i ? primaryButton : button,
                        GUILayout.Height(32f)))
                {
                    Plugin.autoChoiceStrategy.Value = i;
                    if (i != 0 && autoPresetTab == 0) autoPresetTab = 1;
                    Plugin.SaveSettings();
                }
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);
            GUILayout.Label(MenuText.Get("FullInventoryStrategy"), body);
            GUILayout.BeginHorizontal();
            string[] fullLabels = { "FullInventoryWait", "FullInventoryCharm", "FullInventoryOrdinary" };
            for (int i = 0; i < fullLabels.Length; i++)
                if (GUILayout.Button(MenuText.Get(fullLabels[i]),
                        Plugin.autoFullInventoryStrategy.Value == i ? primaryButton : button, GUILayout.Height(30f)))
                {
                    Plugin.autoFullInventoryStrategy.Value = i;
                    Plugin.SaveSettings();
                }
            GUILayout.EndHorizontal();
            GUILayout.Label(MenuText.Get("FullInventoryHelp"), muted);
            GUILayout.Space(8f);
            GUILayout.BeginHorizontal();
            string[] tabs = { "RewardPresetTab", "WeaponPresetTab", "FloorPresetTab" };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool disabled = i == 0 && Plugin.autoChoiceStrategy.Value != 0;
                GUI.enabled = !disabled;
                if (GUILayout.Button(MenuText.Get(tabs[i]), disabled ? toggleOff : autoPresetTab == i ? primaryButton : button,
                        GUILayout.Height(30f)))
                {
                    autoPresetTab = i;
                    showRewardDropdown = false;
                    showWeaponDropdown = false;
                    showFloorDropdown = false;
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(card);
            if (autoPresetTab == 0)
                DrawPresetPicker(MenuText.Get("RewardPresets"), MenuText.Get("RewardPresetsHelp"),
                    RewardPresetOptions, Plugin.autoChoicePresets, ref selectedRewardPreset,
                    ref showRewardDropdown, ref rewardDropdownScroll);
            else if (autoPresetTab == 1)
            {
                if (!string.IsNullOrEmpty(weaponPresetStatus)) GUILayout.Label(weaponPresetStatus, muted);
                DrawPresetPicker(MenuText.Get("WeaponPresets"), MenuText.Get("WeaponPresetsHelp"),
                    WeaponPresetOptions, Plugin.autoWeaponPresets, ref selectedWeaponPreset,
                    ref showWeaponDropdown, ref weaponDropdownScroll);
            }
            else
                DrawPresetPicker(MenuText.Get("FloorPresets"), MenuText.Get("FloorPresetsHelp"),
                    FloorPresetOptions, Plugin.autoFloorPresets, ref selectedFloorPreset,
                    ref showFloorDropdown, ref floorDropdownScroll);
            GUILayout.EndVertical();
        }

        private static void DrawLocalAutoPilotControls()
        {
            GUILayout.Space(8f);
            GUILayout.Label(MenuText.Get("AutoPilotLocalSettings"), section);
            DrawValue(MenuText.Get("AutoPilotShortcut"), Plugin.autoPilotShortcut.Value.ToString(), 180f);
            if (capturingShortcut == 3)
            {
                GUILayout.Label(MenuText.Get("PressNewAutoPilotShortcut"), muted);
                if (GUILayout.Button(MenuText.Get("CancelShortcut"), button, GUILayout.Height(30f)))
                    capturingShortcut = 0;
            }
            else if (GUILayout.Button(MenuText.Get("ChangeAutoPilotShortcut"), button, GUILayout.Height(30f)))
            {
                capturingShortcut = 3;
            }
            GUILayout.Label(MenuText.Get("AutoPilotHelp"), muted);
            if (GUILayout.Button(MenuText.Get(AutoPilot.Enabled ? "DisableAutoPilot" : "EnableAutoPilot"),
                    AutoPilot.Enabled ? dangerButton : primaryButton, GUILayout.Height(34f)))
                AutoPilot.Toggle();
            DrawToggle(MenuText.Get("AutoArrangeInventory"), Plugin.autoArrangeInventory);
            GUILayout.Label(MenuText.Get("AutoArrangeInventoryHelp"), muted);
            DrawToggle(MenuText.Get("AutoDefend"), Plugin.autoDefend);
            GUILayout.Label(MenuText.Get("AutoDefendHelp"), muted);
        }

        private static void DrawPresetPicker(string heading, string help, List<PresetOption> options,
            BepInEx.Configuration.ConfigEntry<string> config, ref int selected, ref bool expanded, ref Vector2 dropdownScroll)
        {
            GUILayout.Label(heading, section);
            List<string> values = SplitPresetValues(config.Value);
            if (values.Count == 0) GUILayout.Label(MenuText.Get("NoPresetSelected"), muted);
            else DrawPresetTags(options, config, values);

            if (options.Count > 0)
            {
                selected = Mathf.Clamp(selected, 0, options.Count - 1);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(options[selected].Label + (expanded ? "  ▲" : "  ▼"), button, GUILayout.Height(32f)))
                    expanded = !expanded;
                if (GUILayout.Button(MenuText.Get("AddPreset"), primaryButton, GUILayout.Width(100f), GUILayout.Height(32f)) &&
                    !values.Contains(options[selected].Value))
                {
                    values.Add(options[selected].Value);
                    SavePresetValues(config, values);
                }
                GUILayout.EndHorizontal();
                if (expanded)
                {
                    dropdownScroll = GUILayout.BeginScrollView(dropdownScroll, GUILayout.Height(180f),
                        GUILayout.ExpandWidth(true));
                    for (int i = 0; i < options.Count; i++)
                        if (GUILayout.Button(options[i].Label, i == selected ? primaryButton : button, GUILayout.Height(28f)))
                        {
                            selected = i;
                            expanded = false;
                        }
                    GUILayout.EndScrollView();
                }
            }
            else if (config != Plugin.autoWeaponPresets || string.IsNullOrEmpty(weaponPresetStatus))
                GUILayout.Label(MenuText.Get("PresetDataUnavailable"), muted);
            GUILayout.Label(help, muted);
        }

        private static void DrawPresetTags(List<PresetOption> options,
            BepInEx.Configuration.ConfigEntry<string> config, List<string> values)
        {
            float available = Mathf.Max(260f, window.width - 70f);
            float used = 0f;
            GUILayout.BeginHorizontal();
            foreach (string value in values.ToArray())
            {
                string label = PresetLabel(options, value) + "  X";
                float width = Mathf.Clamp(tagButton.CalcSize(new GUIContent(label)).x + 10f, 78f, available);
                if (used > 0f && used + width + 5f > available)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    used = 0f;
                }
                if (GUILayout.Button(label, tagButton, GUILayout.Width(width), GUILayout.Height(28f)))
                {
                    values.Remove(value);
                    SavePresetValues(config, values);
                }
                used += width + 5f;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private static void RefreshPresetOptions()
        {
            RewardPresetOptions.Clear();
            WeaponPresetOptions.Clear();
            FloorPresetOptions.Clear();
            weaponPresetStatus = null;
            try
            {
                foreach (ItemCategoryEntity category in Resources.LoadAll<ItemCategoryEntity>("ItemCategory")
                             .Where(category => category != null && category.isEnabled &&
                                                IsValidDisplayName(category.Name, category.categoryName.key))
                             .OrderBy(category => category.Name))
                    RewardPresetOptions.Add(new PresetOption("category:" + category.id,
                        MenuText.Get("CategoryPrefix") + category.Name, category.id, category.Name,
                        category.categoryName.key));

                foreach (int id in ItemDatabase.GetAllItemID())
                {
                    ItemEntity item = ItemDatabase.FindItemById(id);
                    if (item != null && !item.cannotBeReward && IsValidDisplayName(item.Name, item.aName.key))
                        RewardPresetOptions.Add(new PresetOption("item:" + item.id, item.Name,
                            item.id.ToString(), item.Name, item.aName.key));
                }
                RewardPresetOptions.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.CurrentCulture));

                PlayerAvatar localPlayer = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
                WeaponControllerSimple weaponController = localPlayer != null
                    ? localPlayer.GetComponent<WeaponControllerSimple>()
                    : null;
                WeaponSimple currentWeapon = weaponController != null ? weaponController.currentWeapon : null;
                if (currentWeapon == null)
                {
                    weaponPresetStatus = MenuText.Get("WeaponPresetNoWeapon");
                }
                else
                {
                    List<EnhancementMetadata> enhancements = WeaponDatabase.GetWeaponEnhancements(currentWeapon.entityId);
                    if (enhancements == null || enhancements.Count == 0)
                    {
                        weaponPresetStatus = MenuText.Get("WeaponPresetMaxed");
                    }
                    else
                    {
                        AddReachableWeaponOptions(currentWeapon.entityId, 1, new HashSet<int>());
                        WeaponEntity equipped = WeaponDatabase.FindWeaponById(currentWeapon.entityId);
                        string currentName = equipped != null ? equipped.Name : currentWeapon.entityId.ToString();
                        weaponPresetStatus = string.Format(MenuText.Get("WeaponPresetCurrent"), currentName);
                    }
                }
                WeaponPresetOptions.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.CurrentCulture));

                foreach (EFloorMainEventType eventType in Enum.GetValues(typeof(EFloorMainEventType)))
                {
                    if (eventType == EFloorMainEventType.None || eventType == EFloorMainEventType.Unknown ||
                        eventType == EFloorMainEventType.RandomEncounter) continue;
                    string label = FloorEventLabel(eventType);
                    FloorPresetOptions.Add(new PresetOption("floor:" + eventType, label, eventType.ToString()));
                }
                FloorPresetOptions.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.CurrentCulture));
                MigrateLegacyPresets();
                RemoveInvalidStablePresets(Plugin.autoChoicePresets, RewardPresetOptions, "item:", "category:");
                if (currentWeapon != null)
                    RemoveInvalidStablePresets(Plugin.autoWeaponPresets, WeaponPresetOptions, "weapon:");
                RemoveInvalidStablePresets(Plugin.autoFloorPresets, FloorPresetOptions, "floor:");
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("Could not load autopilot preset options: " + exception.Message);
            }
        }

        private static List<string> SplitPresetValues(string value) => (value ?? "")
            .Split(new[] { ',', '，', ';', '；', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Trim()).Where(entry => entry.Length > 0).ToList();

        private static string PresetLabel(List<PresetOption> options, string value)
        {
            PresetOption option = options.FirstOrDefault(candidate =>
                string.Equals(candidate.Value, value, StringComparison.OrdinalIgnoreCase) || candidate.MatchesLegacy(value));
            return option != null ? option.Label : MenuText.Get("UnavailablePreset");
        }

        private static bool IsValidDisplayName(string value, string localizationKey)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            string clean = System.Text.RegularExpressions.Regex.Replace(value, "<[^>]+>", "").Trim();
            if (clean.Length < 2 || !clean.Any(char.IsLetterOrDigit)) return false;
            if (!string.IsNullOrEmpty(localizationKey) &&
                string.Equals(clean, localizationKey, StringComparison.OrdinalIgnoreCase)) return false;
            return !(clean.StartsWith("Item_", StringComparison.OrdinalIgnoreCase) &&
                     clean.EndsWith("_Name", StringComparison.OrdinalIgnoreCase));
        }

        private static void AddReachableWeaponOptions(int weaponId, int tier, HashSet<int> visited)
        {
            if (!visited.Add(weaponId)) return;
            foreach (EnhancementMetadata enhancement in
                     WeaponDatabase.GetWeaponEnhancements(weaponId) ?? new List<EnhancementMetadata>())
            {
                WeaponEntity weapon = enhancement?.enhanced;
                if (weapon == null || !enhancement.enabled) continue;
                if (IsValidDisplayName(weapon.Name, weapon.aName.key) &&
                    !WeaponPresetOptions.Any(option => option.Value == "weapon:" + weapon.id))
                    WeaponPresetOptions.Add(new PresetOption("weapon:" + weapon.id,
                        string.Format(MenuText.Get("WeaponPresetTier"), tier, weapon.Name),
                        weapon.id.ToString(), weapon.Name, weapon.aName.key));
                AddReachableWeaponOptions(weapon.id, tier + 1, visited);
            }
        }

        private static string FloorEventLabel(EFloorMainEventType eventType)
        {
            switch (eventType)
            {
                case EFloorMainEventType.Money: return MenuText.Get("RoomMoney");
                case EFloorMainEventType.EXP: return MenuText.Get("RoomExp");
                case EFloorMainEventType.HP: return MenuText.Get("RoomHeal");
                case EFloorMainEventType.Merchant: return MenuText.Get("RoomMerchant");
                case EFloorMainEventType.Miracle: return MenuText.Get("RoomMiracle");
                case EFloorMainEventType.Charm: return MenuText.Get("RoomCharm");
                case EFloorMainEventType.StoneTablet: return MenuText.Get("RoomTablet");
                case EFloorMainEventType.Enchant: return MenuText.Get("RoomEnchant");
                case EFloorMainEventType.Anvil: return MenuText.Get("RoomAnvil");
                case EFloorMainEventType.Dice: return MenuText.Get("RoomDice");
                case EFloorMainEventType.Sapphire: return MenuText.Get("RoomSapphire");
                case EFloorMainEventType.MaxHP: return MenuText.Get("RoomMaxHp");
                case EFloorMainEventType.InventoryStorage: return MenuText.Get("RoomInventory");
                default: return MenuText.Get("UnknownFloorEvent");
            }
        }

        private static void MigrateLegacyPresets()
        {
            List<string> legacy = SplitPresetValues(Plugin.autoChoicePresets.Value);
            if (!legacy.Any(value => value.IndexOf(':') < 0)) return;

            List<string> rewards = legacy.Where(value => value.IndexOf(':') >= 0).ToList();
            List<string> weapons = SplitPresetValues(Plugin.autoWeaponPresets.Value);
            foreach (string value in legacy.Where(value => value.IndexOf(':') < 0))
            {
                PresetOption reward = RewardPresetOptions.FirstOrDefault(option => option.MatchesLegacy(value));
                PresetOption weapon = WeaponPresetOptions.FirstOrDefault(option => option.MatchesLegacy(value));
                if (reward != null && !rewards.Contains(reward.Value)) rewards.Add(reward.Value);
                if (weapon != null && !weapons.Contains(weapon.Value)) weapons.Add(weapon.Value);
                if (reward == null && weapon == null) rewards.Add(value);
            }
            SavePresetValues(Plugin.autoChoicePresets, rewards);
            SavePresetValues(Plugin.autoWeaponPresets, weapons);
        }

        private static void SavePresetValues(BepInEx.Configuration.ConfigEntry<string> config, List<string> values)
        {
            config.Value = string.Join(",", values);
            Plugin.SaveSettings();
        }

        private static void RemoveInvalidStablePresets(BepInEx.Configuration.ConfigEntry<string> config,
            List<PresetOption> options, params string[] prefixes)
        {
            List<string> values = SplitPresetValues(config.Value);
            int removed = values.RemoveAll(value => prefixes.Any(prefix =>
                value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) &&
                !options.Any(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)));
            if (removed > 0) SavePresetValues(config, values);
        }

        private sealed class PresetOption
        {
            internal readonly string Value;
            internal readonly string Label;
            private readonly string[] aliases;

            internal PresetOption(string value, string label, params string[] aliases)
            {
                Value = value;
                Label = label;
                this.aliases = aliases ?? new string[0];
            }

            internal bool MatchesLegacy(string value) => !string.IsNullOrWhiteSpace(value) && aliases.Any(alias =>
                !string.IsNullOrEmpty(alias) && alias.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void DrawHostPage()
        {
            scroll = GUILayout.BeginScrollView(scroll, false, true);
            GUILayout.Space(8f);
            if (selectedTab == 2)
            {
                BeginSection(MenuText.Get("TabCompensation"));
                GUILayout.Label(MenuText.Get("HostCompensation"), body);
                EndSection();
                DrawPlayers();
            }
            else if (selectedTab == 3)
            {
                BeginSection(MenuText.Get("TabDiagnostics"));
                GUILayout.Label(CatchUpRewards.BuildHostDiagnostics(null), body);
                DrawDownloadLinks();
                EndSection();
            }
            else
            {
                BeginSection(MenuText.Get("TabHistory"));
                string history = CatchUpRewards.GetHostHistory();
                GUILayout.Label(string.IsNullOrEmpty(history) ? MenuText.Get("NoData") : history, body);
                EndSection();
            }
            GUILayout.EndScrollView();
        }

        private static void DrawDownloadLinks()
        {
            GUILayout.Space(10f);
            GUILayout.Label(MenuText.Get("DownloadHelp"), muted);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(MenuText.Get("OpenReleasePage"), button, GUILayout.Height(32f)))
                Application.OpenURL(CatchUpRewards.ReleasePageUrl);
            if (GUILayout.Button(MenuText.Get("OpenPluginDownload"), primaryButton, GUILayout.Height(32f)))
                Application.OpenURL(CatchUpRewards.PluginZipUrl);
            GUILayout.EndHorizontal();
        }

        internal static void ResetClientCompensation()
        {
            CatchUpRewards.ClearClientState();
        }

        private static void DrawPlayers()
        {
            int count = PlayerSpawner.MultiplayerList != null ? PlayerSpawner.MultiplayerList.Count : 0;
            GUILayout.Label(MenuText.Get("Players") + "  " + count, section);
            if (PlayerSpawner.MultiplayerList == null) return;
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList.ToArray())
            {
                if (player == null || player.PlayerAvatar == null) continue;
                LevelController level = player.GetComponent<LevelController>();
                GUILayout.BeginVertical(playerCard);
                GUILayout.BeginHorizontal();
                GUILayout.Label(player.PlayerAvatar.Name, section);
                GUILayout.FlexibleSpace();
                string state = player.isHost ? MenuText.Get("Host") : player.PlayerAvatar.IsDead
                    ? MenuText.Get("Dead") : string.IsNullOrEmpty(player.PlayerAvatar.currentFloorGuid)
                        ? MenuText.Get("Loading") : MenuText.Get("Connected");
                GUILayout.Label(state, badge, GUILayout.Width(90f), GUILayout.Height(26f));
                GUILayout.EndHorizontal();
                string details = MenuText.Get("Level") + " " + (level != null ? level.currentLevel : 0) +
                                  "     " + MenuText.Get("Health") + " " + player.PlayerAvatar.hp.ToString("0") + " / " + player.PlayerAvatar.MaxHp.ToString("0") +
                                  "     " + MenuText.Get("Floor") + " " + FloorDisplay.Format(player.PlayerAvatar.currentFloorGuid);
                GUILayout.Label(details, muted);
                if (!player.isHost && player.connectionToClient != null)
                {
                    if (GUILayout.Button(MenuText.Get("Kick"), dangerButton, GUILayout.Height(30f))) Kick(player);
                }
                GUILayout.EndVertical();
            }
        }

        private static void ApplyScalingPreset(int preset)
        {
            Plugin.SetBaselinePlayers(4);
            if (preset == 0)
            {
                Plugin.SetHealthPerExtraPlayer(0f);
                Plugin.SetMaximumMultiplier(1f);
                Plugin.scaleEnemyCount.Value = false;
                Plugin.SetEnemyCountPerExtraPlayer(0f);
                Plugin.SetMaximumEnemyCountMultiplier(1f);
            }
            else if (preset == 1)
            {
                Plugin.SetHealthPerExtraPlayer(0.1f);
                Plugin.SetMaximumMultiplier(4f);
                Plugin.scaleEnemyCount.Value = true;
                Plugin.SetEnemyCountPerExtraPlayer(0.04f);
                Plugin.SetMaximumEnemyCountMultiplier(2f);
            }
            else if (preset == 2)
            {
                Plugin.SetHealthPerExtraPlayer(0.15f);
                Plugin.SetMaximumMultiplier(8f);
                Plugin.scaleEnemyCount.Value = true;
                Plugin.SetEnemyCountPerExtraPlayer(0.08f);
                Plugin.SetMaximumEnemyCountMultiplier(3f);
            }
            else
            {
                Plugin.SetHealthPerExtraPlayer(0.25f);
                Plugin.SetMaximumMultiplier(12f);
                Plugin.scaleEnemyCount.Value = true;
                Plugin.SetEnemyCountPerExtraPlayer(0.15f);
                Plugin.SetMaximumEnemyCountMultiplier(4f);
            }
            Plugin.SaveSettings();
        }

        private static int GetPreset()
        {
            if (MatchesScaling(0f, 1f, false, 0f, 1f)) return 0;
            if (MatchesScaling(0.1f, 4f, true, 0.04f, 2f)) return 1;
            if (MatchesScaling(0.15f, 8f, true, 0.08f, 3f)) return 2;
            if (MatchesScaling(0.25f, 12f, true, 0.15f, 4f)) return 3;
            return -1;
        }

        private static string GetPresetName(int preset) => preset == 0 ? MenuText.Get("PresetOriginal")
            : preset == 1 ? MenuText.Get("PresetLight")
            : preset == 2 ? MenuText.Get("PresetStandard")
            : preset == 3 ? MenuText.Get("PresetHigh")
            : MenuText.Get("PresetCustom");

        private static bool MatchesScaling(float hp, float hpCap, bool countEnabled, float count, float countCap)
        {
            return Plugin.BaselinePlayersValue == 4 &&
                   Mathf.Approximately(Plugin.HealthPerExtraPlayerValue, hp) &&
                   Mathf.Approximately(Plugin.MaximumMultiplierValue, hpCap) &&
                   Plugin.scaleEnemyCount.Value == countEnabled &&
                   Mathf.Approximately(Plugin.EnemyCountPerExtraPlayerValue, count) &&
                   Mathf.Approximately(Plugin.MaximumEnemyCountMultiplierValue, countCap);
        }

        private static void BeginSection(string heading)
        {
            GUILayout.BeginVertical(card);
            GUILayout.Label(heading, section);
            GUILayout.Space(4f);
        }

        private static void EndSection()
        {
            GUILayout.EndVertical();
            GUILayout.Space(10f);
        }

        private static void DrawValue(string label, string value, float valueWidth = 76f)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, body);
            GUILayout.FlexibleSpace();
            GUILayout.Label(value, badge, GUILayout.Width(valueWidth), GUILayout.Height(28f));
            GUILayout.EndHorizontal();
        }

        private static void DrawToggle(string label, BepInEx.Configuration.ConfigEntry<bool> setting)
        {
            DrawToggle(label, setting.Value, () => setting.Value = !setting.Value);
        }

        private static void DrawToggle(string label, bool enabled, System.Action toggle)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, body);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(MenuText.Get(enabled ? "ToggleOn" : "ToggleOff"), enabled ? toggleOn : toggleOff,
                    GUILayout.Width(72f), GUILayout.Height(30f)))
            {
                toggle();
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawDivider()
        {
            Rect divider = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(divider, buttonTexture);
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void EnsureStyles()
        {
            if (windowStyle != null) return;

            windowTexture = MakeTexture(new Color(0.035f, 0.047f, 0.065f, 0.98f));
            cardTexture = MakeTexture(new Color(0.075f, 0.094f, 0.12f, 1f));
            playerCardTexture = MakeTexture(new Color(0.095f, 0.115f, 0.145f, 1f));
            buttonTexture = MakeTexture(new Color(0.16f, 0.19f, 0.23f, 1f));
            buttonHoverTexture = MakeTexture(new Color(0.22f, 0.26f, 0.31f, 1f));
            primaryTexture = MakeTexture(new Color(0.1f, 0.48f, 0.56f, 1f));
            primaryHoverTexture = MakeTexture(new Color(0.13f, 0.6f, 0.69f, 1f));
            dangerTexture = MakeTexture(new Color(0.55f, 0.18f, 0.2f, 1f));
            inputTexture = MakeTexture(new Color(0.025f, 0.032f, 0.045f, 1f));

            Color text = new Color(0.92f, 0.95f, 0.97f);
            Color dim = new Color(0.62f, 0.69f, 0.74f);
            windowStyle = new GUIStyle(GUI.skin.window) { padding = new RectOffset(18, 18, 14, 16) };
            windowStyle.normal.background = windowTexture;
            windowStyle.onNormal.background = windowTexture;
            windowStyle.hover.background = windowTexture;
            windowStyle.onHover.background = windowTexture;
            windowStyle.active.background = windowTexture;
            windowStyle.onActive.background = windowTexture;
            windowStyle.focused.background = windowTexture;
            windowStyle.onFocused.background = windowTexture;
            title = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, normal = { textColor = text } };
            section = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.55f, 0.9f, 0.95f) } };
            body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft, normal = { textColor = text } };
            muted = new GUIStyle(body) { fontSize = 12, normal = { textColor = dim } };
            card = new GUIStyle(GUI.skin.box) { padding = new RectOffset(13, 13, 11, 12), margin = new RectOffset(0, 0, 0, 0) };
            card.normal.background = cardTexture;
            playerCard = new GUIStyle(card) { margin = new RectOffset(0, 0, 3, 4) };
            playerCard.normal.background = playerCardTexture;
            button = CreateButtonStyle(buttonTexture, buttonHoverTexture, text);
            primaryButton = CreateButtonStyle(primaryTexture, primaryHoverTexture, Color.white);
            dangerButton = CreateButtonStyle(dangerTexture, buttonHoverTexture, Color.white);
            input = new GUIStyle(GUI.skin.textField) { fontSize = 14, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 5, 5) };
            input.normal.background = inputTexture;
            input.normal.textColor = text;
            input.focused.background = inputTexture;
            input.focused.textColor = Color.white;
            badge = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, padding = new RectOffset(7, 7, 3, 3) };
            badge.normal.background = buttonTexture;
            tagButton = CreateButtonStyle(buttonTexture, dangerTexture, text);
            tagButton.fontStyle = FontStyle.Normal;
            tagButton.padding = new RectOffset(9, 9, 4, 4);
            toggleOn = CreateButtonStyle(primaryTexture, primaryHoverTexture, Color.white);
            toggleOff = CreateButtonStyle(buttonTexture, buttonHoverTexture, dim);
        }

        private static GUIStyle CreateButtonStyle(Texture2D normal, Texture2D hover, Color textColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 6, 6)
            };
            style.normal.background = normal;
            style.normal.textColor = textColor;
            style.hover.background = hover;
            style.hover.textColor = Color.white;
            style.active.background = hover;
            style.active.textColor = Color.white;
            style.focused.background = normal;
            style.focused.textColor = textColor;
            return style;
        }

        private static void Kick(PlayerSpawner player)
        {
            if (!NetworkServer.active || player == null || player.connectionToClient == null) return;
            player.connectionToClient.Disconnect();
        }

        private static void RestoreInput()
        {
            if (blockedController != null && !previousInputBlock)
            {
                blockedController.BlockAvatarInput = false;
            }
            blockedController = null;
        }
    }
}
