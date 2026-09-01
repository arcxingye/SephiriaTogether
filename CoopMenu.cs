using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace SephiriaTogether
{
    internal static class CoopMenu
    {
        private static bool open;
        private static Rect window = new Rect(24f, 24f, 600f, 700f);
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
        private static GUIStyle tabButton;
        private static GUIStyle activeTabButton;
        private static GUIStyle input;
        private static GUIStyle pathInput;
        private static GUIStyle badge;
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
        private static bool showAdvancedScaling;
        private static int selectedTab;
        private static int hostRulesTab;
        private static int capturingShortcut;
        private static string pendingSaveActivation;
        private static string transferAmountText = "100";
        private static string directPortText;
        private static uint pendingTransferTarget;
        private static int pendingTransferAmount;

        internal static bool IsCapturingShortcut => capturingShortcut != 0;
        internal static bool IsOpen => open;
        private static bool IsNarrow => window.width < 470f;

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
                SaveManagement.Refresh();
            }
            else
            {
                capturingShortcut = 0;
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
                capturingShortcut = 0;
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
            window.width = Mathf.Min(620f, Mathf.Max(1f, Screen.width - 16f));
            window.x = Mathf.Clamp(window.x, 8f, Mathf.Max(8f, Screen.width - window.width - 8f));
            window.height = Mathf.Min(720f, Mathf.Max(1f, Screen.height - 16f));
            window.y = Mathf.Clamp(window.y, 8f, Mathf.Max(8f, Screen.height - window.height - 8f));

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
            if (NetworkServer.active && selectedTab == 0) DrawHostRuleTabs();

            scroll = GUILayout.BeginScrollView(scroll, false, false);
            GUILayout.Space(8f);
            DrawSelectedPage();
            GUILayout.EndScrollView();
            DrawFooter();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, window.width - 52f, 66f));
        }

        private static void DrawSelectedPage()
        {
            if (selectedTab == 4)
            {
                DrawSaveManagerPage();
                return;
            }
            if (selectedTab == 5)
            {
                DrawTransferPage();
                return;
            }
            if (!NetworkServer.active)
            {
                if (selectedTab == 1) DrawClientCompensation();
                else DrawClientPage();
                return;
            }
            if (selectedTab == 0) DrawHostRulesPage();
            else DrawHostPage();
        }

        private static void DrawHostRulesPage()
        {
            if (hostRulesTab != 2) GUILayout.Label(MenuText.Get("NextSpawn"), muted);
            if (hostRulesTab == 0)
            {
                BeginSection(MenuText.Get("LocalShortcuts"));
                DrawLocalShortcutSettings();
                EndSection();
                if (IpTransport.CanChangeSettings)
                    DrawDirectConnectControls();
                BeginSection(MenuText.Get("Multiplayer"));
                if (JoinProgressBypass.CanCreateLobbyForCurrentRun())
                {
                    GUILayout.Label(MenuText.Get("ResumeLobbyHelp"), muted);
                    if (GUILayout.Button(MenuText.Get("ResumeLobby"), primaryButton, GUILayout.Height(38f)))
                        JoinProgressBypass.OpenLobbyCreationForCurrentRun();
                    GUILayout.Space(8f);
                }
                GUILayout.Label(MenuText.Get("PlayerLimit"), body);
                bool applyLimit;
                if (IsNarrow)
                {
                    playerLimitText = GUILayout.TextField(playerLimitText ?? PlayerLimit.CurrentLimit.ToString(), 3, input,
                        GUILayout.Height(34f));
                    applyLimit = GUILayout.Button(MenuText.Get("Apply"), primaryButton, GUILayout.Height(34f));
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    playerLimitText = GUILayout.TextField(playerLimitText ?? PlayerLimit.CurrentLimit.ToString(), 3, input,
                        GUILayout.Height(34f));
                    applyLimit = GUILayout.Button(MenuText.Get("Apply"), primaryButton, GUILayout.Width(150f), GUILayout.Height(34f));
                    GUILayout.EndHorizontal();
                }
                if (applyLimit && int.TryParse(playerLimitText, out int requestedLimit))
                {
                    PlayerLimit.SetLimit(requestedLimit);
                    playerLimitText = PlayerLimit.CurrentLimit.ToString();
                }
                DrawToggle(MenuText.Get("LowerProgress"), Plugin.allowLowerProgressPlayers);
                DrawToggle(MenuText.Get("MidRun"), Plugin.allowMidRunJoin);
                DrawToggle(MenuText.Get("UngroupedTransition"), Plugin.allowUngroupedStageTransition);
                GUILayout.Label(MenuText.Get("UngroupedTransitionHelp"), muted);
                DrawToggle(MenuText.Get("BreathingHeal"), Plugin.breathingHeal);
                GUILayout.Label(MenuText.Get("BreathingHealHelp"), muted);
                DrawToggle(MenuText.Get("AutoReviveWhenClear"), Plugin.reviveWhenClear);
                GUILayout.Label(MenuText.Get("AutoReviveWhenClearHelp"), muted);
                DrawToggle(MenuText.Get("FriendlyFire"), Plugin.friendlyFire);
                GUILayout.Label(MenuText.Get("FriendlyFireHelp"), muted);
                DrawToggle(MenuText.Get("AllowAttackingMerchants"), Plugin.allowAttackingMerchants);
                GUILayout.Label(MenuText.Get("AllowAttackingMerchantsHelp"), muted);
                DrawToggle(MenuText.Get("AntiCheat"), Plugin.antiCheat);
                GUILayout.Label(MenuText.Get("AntiCheatHelp"), muted);
                EndSection();

                if (!IpTransport.CanChangeSettings)
                    DrawDirectConnectControls();

                BeginSection(MenuText.Get("StartProgress"));
                GUILayout.Label(MenuText.Get("StartProgressHelp"), muted);
                GUILayout.Label(MenuText.Get("StartProgressManual"), section);
                List<RaceEntity> manualOptions = StartProgressSelection.GetManualOptions();
                List<string> manualLabels = new List<string> { MenuText.Get("StartProgressManualUsePlayer") };
                manualLabels.AddRange(manualOptions.Select(StartProgressSelection.DescribeManual));
                int manualSelection = StartProgressSelection.SelectedManualRaceId == int.MinValue
                    ? 0 : Math.Max(0, manualOptions.FindIndex(race => race.id == StartProgressSelection.SelectedManualRaceId) + 1);
                string manualLabel = manualSelection == 0
                    ? StartProgressSelection.HasSelectedPlayer
                        ? MenuText.Get("StartProgressManualUsePlayer")
                        : MenuText.Get("StartProgressManualNone")
                    : manualLabels[manualSelection];
                GUI.enabled = StartProgressSelection.CanSelect;
                if (GUILayout.Button(manualLabel, button, GUILayout.Height(IsNarrow ? 42f : 32f)))
                {
                    UI_MessageBox_List list = UIManager.Instance?.GetElement<UI_MessageBox_List>();
                    if (list != null)
                    {
                        list.Open(MenuText.Get("StartProgressManual"), index =>
                        {
                            if (index <= 0) StartProgressSelection.ClearManualSelection();
                            else if (index - 1 < manualOptions.Count)
                                StartProgressSelection.SelectManual(manualOptions[index - 1].id);
                        }, manualLabels);
                    }
                }
                GUI.enabled = true;
                List<PlayerSpawner> progressPlayers = StartProgressSelection.GetCandidates();
                if (progressPlayers.Count == 0)
                {
                    GUILayout.Label(MenuText.Get("StartProgressNoPlayers"), muted);
                }
                else
                {
                    foreach (PlayerSpawner progressPlayer in progressPlayers)
                    {
                        GUI.enabled = StartProgressSelection.CanSelectPlayer(progressPlayer);
                        if (GUILayout.Button(StartProgressSelection.Describe(progressPlayer),
                                StartProgressSelection.IsSelected(progressPlayer) ? primaryButton : button,
                                GUILayout.Height(IsNarrow ? 42f : 32f)))
                            StartProgressSelection.Select(progressPlayer);
                        GUI.enabled = true;
                    }
                }
                GUI.enabled = StartProgressSelection.CanApplySelected;
                if (GUILayout.Button(MenuText.Get("StartProgressApply"), primaryButton, GUILayout.Height(36f)))
                {
                    if (StartProgressSelection.ApplySelected()) Close();
                }
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(StartProgressSelection.Status))
                    GUILayout.Label(StartProgressSelection.Status, muted);
                EndSection();
                return;
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
                string[] presetLabels =
                {
                    MenuText.Get("PresetCasual"), MenuText.Get("PresetOriginal"),
                    MenuText.Get("PresetStandard"), MenuText.Get("PresetHigh")
                };
                int[] presetValues = { 1, 0, 2, 3 };
                int activePreset = Array.IndexOf(presetValues, currentPreset);
                int clickedPreset = DrawButtonGrid(presetLabels, activePreset, 2, 36f);
                if (clickedPreset >= 0) ApplyScalingPreset(presetValues[clickedPreset]);

                int activePlayers = Plugin.PlayerCount;
                int extraPlayers = Mathf.Max(0, activePlayers - Plugin.BaselinePlayersValue);
                float healthMultiplier = Plugin.BaseEnemyMultiplierValue *
                                         (1f + extraPlayers * Plugin.HealthPerExtraPlayerValue);
                if (Plugin.MaximumMultiplierValue > 0f)
                    healthMultiplier = Mathf.Min(healthMultiplier, Plugin.MaximumMultiplierValue);
                float countMultiplier = Plugin.BaseEnemyMultiplierValue * (Plugin.scaleEnemyCount.Value
                    ? 1f + extraPlayers * Plugin.EnemyCountPerExtraPlayerValue
                    : 1f);
                countMultiplier = Mathf.Min(Plugin.MaximumEnemyCountMultiplierValue, countMultiplier);
                GUILayout.BeginVertical(playerCard);
                GUILayout.Label(string.Format(MenuText.Get("ScalingPreviewPlayers"), activePlayers), section);
                DrawValue(MenuText.Get("PreviewHealth"), healthMultiplier.ToString("0.00") + "x");
                DrawValue(MenuText.Get("PreviewCount"), countMultiplier.ToString("0.00") + "x");
                GUILayout.Label(MenuText.Get("ScalingTiming"), muted);
                GUILayout.EndVertical();
                GUILayout.Space(6f);
                if (GUILayout.Button(showAdvancedScaling ? MenuText.Get("HideAdvanced") : MenuText.Get("ShowAdvanced"),
                        button, GUILayout.Height(32f)))
                    showAdvancedScaling = !showAdvancedScaling;
                if (showAdvancedScaling)
                {
                    GUILayout.Space(8f);
                    DrawStepper(MenuText.Get("Baseline"), Plugin.BaselinePlayersValue.ToString(),
                        () => Plugin.SetBaselinePlayers(Plugin.BaselinePlayersValue - 1),
                        () => Plugin.SetBaselinePlayers(Plugin.BaselinePlayersValue + 1));
                    DrawValue(MenuText.Get("ExtraHp"), (Plugin.HealthPerExtraPlayerValue * 100f).ToString("0") + "%");
                    int hpStep = DrawButtonGrid(new[] { "-5%", "+5%" }, -1, 2, 32f);
                    if (hpStep == 0) Plugin.SetHealthPerExtraPlayer(Plugin.HealthPerExtraPlayerValue - 0.05f);
                    else if (hpStep == 1) Plugin.SetHealthPerExtraPlayer(Plugin.HealthPerExtraPlayerValue + 0.05f);
                    DrawValue(MenuText.Get("HpCap"), Plugin.MaximumMultiplierValue.ToString("0.##") + "x");
                    if (GUILayout.Button(MenuText.Get("CycleHpCap"), button, GUILayout.Height(32f)))
                    {
                        float value = Plugin.MaximumMultiplierValue;
                        Plugin.SetMaximumMultiplier(value < 4.1f ? 8f : value < 8.1f ? 12f : value < 12.1f ? 0f : 4f);
                    }
                    DrawToggle(MenuText.Get("EnemyCount"), Plugin.scaleEnemyCount);
                    DrawValue(MenuText.Get("CountPerPlayer"),
                        (Plugin.EnemyCountPerExtraPlayerValue * 100f).ToString("0") + "%");
                    int countStep = DrawButtonGrid(new[] { "-2%", "+2%" }, -1, 2, 32f);
                    if (countStep == 0) Plugin.SetEnemyCountPerExtraPlayer(Plugin.EnemyCountPerExtraPlayerValue - 0.02f);
                    else if (countStep == 1) Plugin.SetEnemyCountPerExtraPlayer(Plugin.EnemyCountPerExtraPlayerValue + 0.02f);
                    DrawValue(MenuText.Get("CountCap"), Plugin.MaximumEnemyCountMultiplierValue.ToString("0.##") + "x");
                    int capStep = DrawButtonGrid(new[] { "-0.5x", "+0.5x" }, -1, 2, 32f);
                    if (capStep == 0) Plugin.SetMaximumEnemyCountMultiplier(Plugin.MaximumEnemyCountMultiplierValue - 0.5f);
                    else if (capStep == 1) Plugin.SetMaximumEnemyCountMultiplier(Plugin.MaximumEnemyCountMultiplierValue + 0.5f);
                }
                EndSection();
                return;
            }

            DrawPlayers();
        }

        private static void DrawFooter()
        {
            GUILayout.Space(4f);
            DrawDivider();
            GUILayout.Space(6f);
            if (NetworkServer.active && selectedTab == 0)
            {
                int action = DrawButtonGrid(new[] { MenuText.Get("Save"), MenuText.Get("Close") }, 0, 2, 38f);
                if (action == 0) Plugin.SaveSettings();
                else if (action == 1) Close();
            }
            else if (DrawButtonGrid(new[] { MenuText.Get("Close") }, -1, 1, 38f) == 0)
            {
                Close();
            }
            GUILayout.Space(2f);
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
        }

        private static void DrawTabs()
        {
            string[] labels = { "TabRules", "TabCompensation", "TabDiagnostics", "TabHistory", "TabSaves", "TabTransfer" };
            string[] localized = labels.Select(MenuText.Get).ToArray();
            int columns = IsNarrow ? 2 : 3;
            int selected = DrawButtonGrid(localized, selectedTab, columns, IsNarrow ? 40f : 34f, useTabStyles: true);
            if (selected >= 0 && selected != selectedTab)
            {
                selectedTab = selected;
                scroll = Vector2.zero;
                pendingTransferTarget = 0;
                pendingTransferAmount = 0;
            }
            DrawDivider();
        }

        private static void DrawTransferPage()
        {
            CatchUpRewards.SendHello();
            MoneyTransfer.Tick();
            PlayerAvatar local = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayer : null;
            BeginSection(MenuText.Get("LeafTransfer"));
            GUILayout.Label(MenuText.Get("LeafTransferHelp"), muted);
            DrawValue(MenuText.Get("TransferBalance"), (local?.Money ?? 0).ToString(), 130f);
            if (IsNarrow)
            {
                GUILayout.Label(MenuText.Get("TransferAmount"), body);
                transferAmountText = GUILayout.TextField(transferAmountText ?? "", input, GUILayout.Height(30f));
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(MenuText.Get("TransferAmount"), body, GUILayout.Width(150f));
                transferAmountText = GUILayout.TextField(transferAmountText ?? "", input, GUILayout.Height(30f));
                GUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(MoneyTransfer.Status)) GUILayout.Label(MoneyTransfer.Status, muted);
            EndSection();

            List<PlayerAvatar> recipients = PlayerSpawner.MultiplayerList?
                .Where(spawner => spawner?.PlayerAvatar != null &&
                                  spawner.PlayerAvatar != local)
                .Select(spawner => spawner.PlayerAvatar)
                .ToList() ?? new List<PlayerAvatar>();
            if (recipients.Count == 0)
                GUILayout.Label(MenuText.Get("TransferNoRecipients"), muted);

            foreach (PlayerAvatar recipient in recipients)
            {
                GUILayout.BeginVertical(playerCard);
                DrawCardHeader(recipient.Name, MenuText.Get(recipient.IsDead ? "Dead" : "Connected"));
                GUILayout.Label(MenuText.Get("TransferRecipientBalance") + " " + recipient.Money, muted);

                bool confirming = pendingTransferTarget == recipient.netId;
                if (!confirming)
                {
                    bool validAmount = int.TryParse(transferAmountText, out int amount) && amount > 0;
                    GUI.enabled = validAmount && !MoneyTransfer.IsPending && MoneyTransfer.IsAvailable;
                    if (GUILayout.Button(MenuText.Get("TransferSend"), primaryButton, GUILayout.Height(30f)))
                    {
                        pendingTransferTarget = recipient.netId;
                        pendingTransferAmount = amount;
                    }
                    GUI.enabled = true;
                }
                else
                {
                    GUILayout.Label(string.Format(MenuText.Get("TransferConfirmHelp"),
                        recipient.Name, pendingTransferAmount), muted);
                    GUI.enabled = !MoneyTransfer.IsPending;
                    int action = DrawButtonGrid(new[]
                    {
                        MenuText.Get("TransferConfirm"), MenuText.Get("TransferCancel")
                    }, -1, IsNarrow ? 1 : 2, 30f, firstDanger: true);
                    if (action == 0)
                    {
                        MoneyTransfer.TrySend(recipient, pendingTransferAmount);
                        pendingTransferTarget = 0;
                        pendingTransferAmount = 0;
                    }
                    GUI.enabled = true;
                    if (action == 1)
                    {
                        pendingTransferTarget = 0;
                        pendingTransferAmount = 0;
                    }
                }
                GUILayout.EndVertical();
                GUILayout.Space(1f);
            }
        }

        private static void DrawSaveManagerPage()
        {
            BeginSection(MenuText.Get("SaveManager"));
            GUILayout.Label(MenuText.Get("SaveManagerHelp"), muted);
            GUILayout.Label(MenuText.Get("SaveManagerDirectory"), body);
            GUILayout.TextField(SaveManagement.SaveDirectory, pathInput, GUILayout.Height(IsNarrow ? 42f : 28f));
            DrawValue(MenuText.Get("SaveManagerCurrent"), SaveManagement.SelectedSlot, 110f);
            if (!string.IsNullOrEmpty(SaveManagement.Status)) GUILayout.Label(SaveManagement.Status, muted);
            GUI.enabled = !SaveManagement.IsBusy;
            int saveAction = DrawButtonGrid(new[]
            {
                MenuText.Get("SaveManagerBackupNow"), MenuText.Get("SaveManagerRefresh")
            }, -1, IsNarrow ? 1 : 2, 32f);
            if (saveAction == 0)
                SaveManagement.BackupCurrent();
            if (saveAction == 1)
            {
                pendingSaveActivation = null;
                SaveManagement.Refresh();
            }
            GUI.enabled = true;
            EndSection();

            foreach (ManagedSaveEntry entry in SaveManagement.Saves)
            {
                GUILayout.BeginVertical(playerCard);
                string kind = entry.IsBackup
                    ? entry.IsManagedBackup ? MenuText.Get("SaveManagerModBackup") : MenuText.Get("SaveManagerGameBackup")
                    : string.Equals(entry.Slot, SaveManagement.SelectedSlot, StringComparison.OrdinalIgnoreCase)
                        ? MenuText.Get("SaveManagerActiveSave")
                        : MenuText.Get("SaveManagerOtherSave");
                bool confirming = pendingSaveActivation == entry.Id;
                if (!IsNarrow && !confirming)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical();
                    GUILayout.Label($"{entry.Slot} · {kind}", section);
                    GUILayout.Label(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + " · " +
                                    (entry.CanResumeDungeon ? MenuText.Get("SaveManagerWithRun") : MenuText.Get("SaveManagerMainOnly")),
                        muted);
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();
                    GUI.enabled = !SaveManagement.IsBusy;
                    if (GUILayout.Button(MenuText.Get("SaveManagerUse"), button,
                            GUILayout.Width(112f), GUILayout.Height(32f)))
                        pendingSaveActivation = entry.Id;
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label($"{entry.Slot} · {kind}", section);
                    GUILayout.Label(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + " · " +
                                    (entry.CanResumeDungeon ? MenuText.Get("SaveManagerWithRun") : MenuText.Get("SaveManagerMainOnly")),
                        muted);
                    GUI.enabled = !SaveManagement.IsBusy;
                    if (!confirming && GUILayout.Button(MenuText.Get("SaveManagerUse"), button,
                            GUILayout.Height(30f)))
                        pendingSaveActivation = entry.Id;
                    GUI.enabled = true;
                }
                if (confirming)
                {
                    GUILayout.Label(MenuText.Get("SaveManagerConfirmHelp"), muted);
                    int confirmAction = DrawButtonGrid(new[]
                    {
                        MenuText.Get("SaveManagerConfirmUse"), MenuText.Get("SaveManagerCancel")
                    }, -1, IsNarrow ? 1 : 2, 30f, firstDanger: true);
                    if (confirmAction == 0)
                    {
                        pendingSaveActivation = null;
                        SaveManagement.Activate(entry);
                    }
                    if (confirmAction == 1)
                        pendingSaveActivation = null;
                }
                GUILayout.EndVertical();
                GUILayout.Space(1f);
            }
        }

        private static void DrawHostRuleTabs()
        {
            string[] labels = { "HostMultiplayerTab", "HostScalingTab", "HostPlayersTab" };
            int selected = DrawButtonGrid(labels.Select(MenuText.Get).ToArray(), hostRulesTab, 3,
                IsNarrow ? 42f : 32f, useTabStyles: true);
            if (selected >= 0 && selected != hostRulesTab)
            {
                hostRulesTab = selected;
                scroll = Vector2.zero;
            }
            DrawDivider();
        }

        private static void CaptureShortcutInput()
        {
            if (capturingShortcut == 0 || Event.current == null || Event.current.type != EventType.KeyDown ||
                Event.current.keyCode == KeyCode.None)
            {
                return;
            }
            if (Event.current.keyCode == KeyCode.Escape)
            {
                capturingShortcut = 0;
                Event.current.Use();
                return;
            }
            if (IsModifierKey(Event.current.keyCode))
            {
                Event.current.Use();
                return;
            }

            KeyCode[] modifiers = new List<KeyCode>
            {
                PressedModifier(Event.current.control, KeyCode.LeftControl, KeyCode.RightControl),
                PressedModifier(Event.current.shift, KeyCode.LeftShift, KeyCode.RightShift),
                PressedModifier(Event.current.alt, KeyCode.LeftAlt, KeyCode.RightAlt),
                PressedModifier(Event.current.command, KeyCode.LeftCommand, KeyCode.RightCommand)
            }.Where(key => key != KeyCode.None).ToArray();
            BepInEx.Configuration.KeyboardShortcut shortcut =
                new BepInEx.Configuration.KeyboardShortcut(Event.current.keyCode, modifiers);
            if (capturingShortcut == 1) Plugin.menuShortcut.Value = shortcut;
            else if (capturingShortcut == 2) Plugin.rescueShortcut.Value = shortcut;
            else return;
            Plugin.SaveSettings();
            capturingShortcut = 0;
            Event.current.Use();
        }

        private static bool IsModifierKey(KeyCode key) =>
            key == KeyCode.LeftControl || key == KeyCode.RightControl ||
            key == KeyCode.LeftShift || key == KeyCode.RightShift ||
            key == KeyCode.LeftAlt || key == KeyCode.RightAlt ||
            key == KeyCode.LeftCommand || key == KeyCode.RightCommand;

        private static KeyCode PressedModifier(bool active, KeyCode left, KeyCode right)
        {
            if (!active) return KeyCode.None;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (right == KeyCode.RightControl && keyboard.rightCtrlKey.isPressed) return right;
                if (right == KeyCode.RightShift && keyboard.rightShiftKey.isPressed) return right;
                if (right == KeyCode.RightAlt && keyboard.rightAltKey.isPressed) return right;
            }
            return left;
        }

        private static void DrawLocalShortcutSettings()
        {
            DrawValue(MenuText.Get("MenuShortcut"), Plugin.FormatShortcut(Plugin.menuShortcut.Value), 180f);
            if (capturingShortcut == 1)
            {
                GUILayout.Label(MenuText.Get("PressNewShortcut"), muted);
                if (GUILayout.Button(MenuText.Get("CancelShortcut"), button, GUILayout.Height(30f)))
                    capturingShortcut = 0;
                return;
            }
            if (GUILayout.Button(MenuText.Get("ChangeShortcut"), button, GUILayout.Height(30f)))
            {
                capturingShortcut = 1;
            }

            DrawValue(MenuText.Get("RescueShortcut"), Plugin.FormatShortcut(Plugin.rescueShortcut.Value), 180f);
            if (capturingShortcut == 2)
            {
                GUILayout.Label(MenuText.Get("PressNewRescueShortcut"), muted);
                if (GUILayout.Button(MenuText.Get("CancelShortcut"), button, GUILayout.Height(30f)))
                    capturingShortcut = 0;
                return;
            }

            int shortcutAction = DrawButtonGrid(new[]
            {
                MenuText.Get("ChangeRescueShortcut"), MenuText.Get("ClearShortcut")
            }, -1, IsNarrow ? 1 : 2, 30f);
            if (shortcutAction == 0)
                capturingShortcut = 2;
            if (shortcutAction == 1 && Plugin.IsShortcutBound(Plugin.rescueShortcut.Value))
            {
                Plugin.rescueShortcut.Value = BepInEx.Configuration.KeyboardShortcut.Empty;
                Plugin.SaveSettings();
            }
        }

        private static void DrawClientPage()
        {
            CatchUpRewards.SendHello();
            string heading = selectedTab == 0 ? MenuText.Get("TabRules")
                : selectedTab == 2 ? MenuText.Get("TabDiagnostics") : MenuText.Get("TabHistory");
            string content = selectedTab == 0 ? CatchUpRewards.ClientRules
                : selectedTab == 2 ? CatchUpRewards.ClientDiagnostics : CatchUpRewards.ClientHistory;
            if (selectedTab == 0)
            {
                BeginSection(MenuText.Get("LocalShortcuts"));
                DrawLocalShortcutSettings();
                EndSection();
                if (IpTransport.CanChangeSettings)
                    DrawDirectConnectControls();
            }
            BeginSection(heading);
            GUILayout.Label(string.IsNullOrEmpty(content) ? MenuText.Get("NoData") : content, body);
            if (selectedTab == 2)
            {
                DrawDownloadLinks();
            }
            EndSection();
        }

        private static void DrawDirectConnectControls()
        {
            if (string.IsNullOrEmpty(directPortText)) directPortText = IpTransport.ConfiguredPort.ToString();
            string status = IpTransport.IsActive ? MenuText.Get("IpActiveShort") :
                Plugin.directModeEnabled != null && Plugin.directModeEnabled.Value
                    ? MenuText.Get("IpPendingShort") : MenuText.Get("IpOffShort");
            BeginSection(MenuText.Get("DirectConnect"));
            GUILayout.BeginHorizontal();
            GUILayout.Label(status, muted);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Label(MenuText.Get("DirectConnectHelp"), muted);
            if (IpTransport.IsOfflineEnvironment)
            {
                GUI.enabled = false;
                GUILayout.Label(MenuText.Get("IpOfflineAutomatic"), body);
                GUI.enabled = true;
            }
            else
            {
                GUI.enabled = IpTransport.CanChangeSettings;
                DrawToggle(MenuText.Get("DirectMode"), Plugin.directModeEnabled.Value,
                    () =>
                    {
                        Plugin.directModeEnabled.Value = !Plugin.directModeEnabled.Value;
                        Plugin.SaveSettings();
                        IpTransport.ApplySettingsFromMenu();
                    });
                GUI.enabled = true;
            }
            GUILayout.Label(MenuText.Get("DirectPort"), body);
            GUI.enabled = IpTransport.CanChangeSettings;
            directPortText = GUILayout.TextField(directPortText ?? "", input, GUILayout.Height(30f));
            if (int.TryParse(directPortText, out int parsedPort) && parsedPort >= 1 && parsedPort <= 65535)
            {
                if (Plugin.directPort.Value != parsedPort)
                {
                    Plugin.directPort.Value = parsedPort;
                    Plugin.SaveSettings();
                    IpTransport.ApplySettingsFromMenu();
                }
            }
            GUI.enabled = true;
            if (!IpTransport.CanChangeSettings)
                GUILayout.Label(MenuText.Get("IpLockedInSession"), muted);
            GUILayout.Label(MenuText.Get("IpRestartNotice"), muted);
            EndSection();
        }

        private static void DrawHostPage()
        {
            if (selectedTab == 1)
            {
                BeginSection(MenuText.Get("TabCompensation"));
                GUILayout.Label(MenuText.Get("HostCompensation"), body);
                EndSection();
                DrawPlayers();
            }
            else if (selectedTab == 2)
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
        }

        private static void DrawDownloadLinks()
        {
            GUILayout.Space(10f);
            GUILayout.Label(MenuText.Get("DownloadHelp"), muted);
            int action = DrawButtonGrid(new[]
            {
                MenuText.Get("OpenReleasePage"), MenuText.Get("OpenPluginDownload")
            }, -1, IsNarrow ? 1 : 2, 34f);
            if (action == 0)
                Application.OpenURL(CatchUpRewards.ReleasePageUrl);
            if (action == 1)
                Application.OpenURL(CatchUpRewards.PluginZipUrl);
        }

        internal static void ResetClientCompensation()
        {
            CatchUpRewards.ClearClientState();
        }

        private static void DrawPlayers()
        {
            int count = Plugin.PlayerCount;
            GUILayout.Label(MenuText.Get("Players") + "  " + count, section);
            if (PlayerSpawner.MultiplayerList == null) return;
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList.ToArray())
            {
                if (player == null || player.PlayerAvatar == null) continue;
                LevelController level = player.GetComponent<LevelController>();
                GUILayout.BeginVertical(playerCard);
                string state = player.isHost ? MenuText.Get("Host") : player.PlayerAvatar.IsDead
                    ? MenuText.Get("Dead") : string.IsNullOrEmpty(player.PlayerAvatar.currentFloorGuid)
                        ? MenuText.Get("Loading") : MenuText.Get("Connected");
                DrawCardHeader(player.PlayerAvatar.Name, state);
                if (IsNarrow)
                {
                    GUILayout.Label(MenuText.Get("Level") + "  " + (level != null ? level.currentLevel : 0), muted);
                    GUILayout.Label(MenuText.Get("Health") + "  " + player.PlayerAvatar.hp.ToString("0") + " / " +
                                    player.PlayerAvatar.MaxHp.ToString("0"), muted);
                    GUILayout.Label(MenuText.Get("Floor") + "  " +
                                    FloorDisplay.Format(player.PlayerAvatar.currentFloorGuid), muted);
                }
                else
                {
                    string details = MenuText.Get("Level") + " " + (level != null ? level.currentLevel : 0) +
                                     "     " + MenuText.Get("Health") + " " + player.PlayerAvatar.hp.ToString("0") +
                                     " / " + player.PlayerAvatar.MaxHp.ToString("0") +
                                     "     " + MenuText.Get("Floor") + " " +
                                     FloorDisplay.Format(player.PlayerAvatar.currentFloorGuid);
                    GUILayout.Label(details, muted);
                }
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
                Plugin.SetBaseEnemyMultiplier(1f);
                Plugin.SetHealthPerExtraPlayer(0f);
                Plugin.SetMaximumMultiplier(1f);
                Plugin.scaleEnemyCount.Value = false;
                Plugin.SetEnemyCountPerExtraPlayer(0f);
                Plugin.SetMaximumEnemyCountMultiplier(1f);
            }
            else if (preset == 1)
            {
                Plugin.SetBaseEnemyMultiplier(0.25f);
                Plugin.SetHealthPerExtraPlayer(0f);
                Plugin.SetMaximumMultiplier(1f);
                Plugin.scaleEnemyCount.Value = false;
                Plugin.SetEnemyCountPerExtraPlayer(0f);
                Plugin.SetMaximumEnemyCountMultiplier(1f);
            }
            else if (preset == 2)
            {
                Plugin.SetBaseEnemyMultiplier(1f);
                Plugin.SetHealthPerExtraPlayer(0.15f);
                Plugin.SetMaximumMultiplier(8f);
                Plugin.scaleEnemyCount.Value = true;
                Plugin.SetEnemyCountPerExtraPlayer(0.08f);
                Plugin.SetMaximumEnemyCountMultiplier(3f);
            }
            else
            {
                Plugin.SetBaseEnemyMultiplier(1f);
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
            if (MatchesScaling(1f, 0f, 1f, false, 0f, 1f)) return 0;
            if (MatchesScaling(0.25f, 0f, 1f, false, 0f, 1f)) return 1;
            if (MatchesScaling(1f, 0.15f, 8f, true, 0.08f, 3f)) return 2;
            if (MatchesScaling(1f, 0.25f, 12f, true, 0.15f, 4f)) return 3;
            return -1;
        }

        private static string GetPresetName(int preset) => preset == 0 ? MenuText.Get("PresetOriginal")
            : preset == 1 ? MenuText.Get("PresetCasual")
            : preset == 2 ? MenuText.Get("PresetStandard")
            : preset == 3 ? MenuText.Get("PresetHigh")
            : MenuText.Get("PresetCustom");

        private static bool MatchesScaling(float baseMultiplier, float hp, float hpCap, bool countEnabled,
            float count, float countCap)
        {
            return Plugin.BaselinePlayersValue == 4 &&
                   Mathf.Approximately(Plugin.BaseEnemyMultiplierValue, baseMultiplier) &&
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
            if (IsNarrow)
            {
                GUILayout.Label(label, body);
                GUILayout.Label(value, badge, GUILayout.Height(28f));
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(label, body);
                GUILayout.FlexibleSpace();
                GUILayout.Label(value, badge, GUILayout.Width(valueWidth), GUILayout.Height(28f));
                GUILayout.EndHorizontal();
            }
        }

        private static void DrawToggle(string label, BepInEx.Configuration.ConfigEntry<bool> setting)
        {
            DrawToggle(label, setting.Value, () => setting.Value = !setting.Value);
        }

        private static void DrawToggle(string label, bool enabled, System.Action toggle)
        {
            if (IsNarrow)
            {
                GUILayout.Label(label, body);
                if (GUILayout.Button(MenuText.Get(enabled ? "ToggleOn" : "ToggleOff"),
                        enabled ? toggleOn : toggleOff, GUILayout.Height(30f))) toggle();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(label, body);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(MenuText.Get(enabled ? "ToggleOn" : "ToggleOff"),
                        enabled ? toggleOn : toggleOff, GUILayout.Width(72f), GUILayout.Height(30f))) toggle();
                GUILayout.EndHorizontal();
            }
        }

        private static int DrawButtonGrid(string[] labels, int selected, int columns, float height,
            bool useTabStyles = false, bool firstDanger = false)
        {
            int clicked = -1;
            columns = Mathf.Max(1, columns);
            const float gap = 4f;
            for (int row = 0; row < Mathf.CeilToInt(labels.Length / (float)columns); row++)
            {
                Rect rowRect = GUILayoutUtility.GetRect(1f, height, GUILayout.ExpandWidth(true));
                float cellWidth = Mathf.Max(1f, (rowRect.width - gap * (columns - 1)) / columns);
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (index >= labels.Length) continue;
                    GUIStyle style = useTabStyles
                        ? index == selected ? activeTabButton : tabButton
                        : firstDanger && index == 0 ? dangerButton
                            : index == selected ? primaryButton : button;
                    Rect cell = new Rect(rowRect.x + column * (cellWidth + gap), rowRect.y, cellWidth, rowRect.height);
                    if (GUI.Button(cell, labels[index], style))
                        clicked = index;
                }
                GUILayout.Space(gap);
            }
            return clicked;
        }

        private static void DrawCardHeader(string name, string state)
        {
            if (IsNarrow)
            {
                GUILayout.Label(name, section);
                GUILayout.Label(state, badge, GUILayout.Height(26f));
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, section);
            GUILayout.FlexibleSpace();
            GUILayout.Label(state, badge, GUILayout.Width(90f), GUILayout.Height(26f));
            GUILayout.EndHorizontal();
        }

        private static void DrawStepper(string label, string value, System.Action decrease, System.Action increase)
        {
            if (IsNarrow) GUILayout.Label(label, body);
            GUILayout.BeginHorizontal();
            if (!IsNarrow)
            {
                GUILayout.Label(label, body);
                GUILayout.FlexibleSpace();
            }
            if (GUILayout.Button("-", button, GUILayout.Width(44f), GUILayout.Height(30f))) decrease();
            GUILayout.Label(value, badge, GUILayout.Width(64f), GUILayout.Height(30f));
            if (GUILayout.Button("+", button, GUILayout.Width(44f), GUILayout.Height(30f))) increase();
            GUILayout.EndHorizontal();
        }

        private static void DrawDivider()
        {
            GUILayout.Space(4f);
            Rect divider = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
            GUI.DrawTexture(divider, buttonTexture);
            GUILayout.Space(4f);
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
            pathInput = new GUIStyle(input)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            badge = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, padding = new RectOffset(7, 7, 3, 3) };
            badge.wordWrap = false;
            badge.normal.background = buttonTexture;
            tabButton = CreateButtonStyle(buttonTexture, buttonHoverTexture, text);
            tabButton.fontSize = 12;
            tabButton.wordWrap = true;
            tabButton.alignment = TextAnchor.MiddleCenter;
            tabButton.padding = new RectOffset(5, 5, 5, 5);
            activeTabButton = CreateButtonStyle(primaryTexture, primaryHoverTexture, Color.white);
            activeTabButton.fontSize = 12;
            activeTabButton.wordWrap = true;
            activeTabButton.alignment = TextAnchor.MiddleCenter;
            activeTabButton.padding = new RectOffset(5, 5, 5, 5);
            toggleOn = CreateButtonStyle(primaryTexture, primaryHoverTexture, Color.white);
            toggleOff = CreateButtonStyle(buttonTexture, buttonHoverTexture, dim);
        }

        private static GUIStyle CreateButtonStyle(Texture2D normal, Texture2D hover, Color textColor)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(10, 10, 6, 6),
                margin = new RectOffset(2, 2, 2, 2),
                alignment = TextAnchor.MiddleCenter
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
            if (!NetworkServer.active || player == null ||
                player.connectionToClient == null) return;
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
