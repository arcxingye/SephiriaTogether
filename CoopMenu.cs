using UnityEngine;
using Mirror;

namespace SephiriaTogether
{
    internal static class CoopMenu
    {
        private static bool open;
        private static Rect window = new Rect(24f, 24f, 430f, 420f);
        private static GUIStyle title;
        private static GUIStyle section;
        private static bool previousCursorVisible;
        private static CursorLockMode previousCursorLockMode;
        private static PlayerInputController blockedController;
        private static bool previousInputBlock;
        private static string playerLimitText;
        private static Vector2 scroll;

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
            if ((blockedController == null || !blockedController.HasAvatar) &&
                PlayerInputController.Instance != null && PlayerInputController.Instance.HasAvatar)
            {
                blockedController = PlayerInputController.Instance;
                previousInputBlock = blockedController.BlockAvatarInput;
                blockedController.BlockAvatarInput = true;
            }
            window.x = Mathf.Clamp(window.x, 0f, Mathf.Max(0f, Screen.width - window.width));
            window.height = Mathf.Min(620f, Mathf.Max(300f, Screen.height - 48f));
            window.y = Mathf.Clamp(window.y, 0f, Mathf.Max(0f, Screen.height - window.height));

            title ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            section ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            window = GUI.Window(100100, window, DrawWindow, MenuText.Get("Title"));
        }

        private static void DrawWindow(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(MenuText.Get("HostSettings"), title);
            if (!NetworkServer.active)
            {
                GUILayout.Label(MenuText.Get("HostOnly"));
                if (GUILayout.Button(MenuText.Get("Close"))) Close();
                GUILayout.EndVertical();
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
                return;
            }
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(MenuText.Get("NextSpawn"));
            GUILayout.Space(6f);
            GUILayout.Label(MenuText.Get("Multiplayer"), section);
            GUILayout.Label(MenuText.Get("PlayerLimit"));
            playerLimitText = GUILayout.TextField(playerLimitText ?? PlayerLimit.CurrentLimit.ToString(), 3);
            if (GUILayout.Button(MenuText.Get("Apply")) && int.TryParse(playerLimitText, out int requestedLimit))
            {
                PlayerLimit.SetLimit(requestedLimit);
                playerLimitText = PlayerLimit.CurrentLimit.ToString();
            }
            Plugin.allowLowerProgressPlayers.Value = GUILayout.Toggle(Plugin.allowLowerProgressPlayers.Value, MenuText.Get("LowerProgress"));
            Plugin.allowMidRunJoin.Value = GUILayout.Toggle(Plugin.allowMidRunJoin.Value, MenuText.Get("MidRun"));
            GUILayout.Label(MenuText.Get("StageTransition"), section);
            GUILayout.Label(StageTransition.CanForce
                ? MenuText.Get("PendingStage") + ": " + StageTransition.PendingStageName
                : MenuText.Get("NoPendingStage"));
            GUI.enabled = StageTransition.CanForce;
            if (GUILayout.Button(MenuText.Get("ForceNextStage")))
            {
                StageTransition.ForcePendingStage();
                Toggle();
            }
            GUI.enabled = true;
            GUILayout.Label(MenuText.Get("Catchup") + ": " + (Plugin.catchUpExperienceRatio.Value * 100f).ToString("0") + "%");
            if (GUILayout.Button(MenuText.Get("CycleCatchup")))
            {
                float value = Plugin.catchUpExperienceRatio.Value;
                Plugin.catchUpExperienceRatio.Value = value < 0.01f ? 0.5f : value < 0.51f ? 0.75f : value < 0.76f ? 1f : 0f;
            }
            GUILayout.Space(6f);
            GUILayout.Label(MenuText.Get("EnemyScaling"), section);
            GUILayout.BeginHorizontal();
            GUILayout.Label(MenuText.Get("Baseline") + ": " + Plugin.BaselinePlayersValue, GUILayout.Width(190f));
            if (GUILayout.Button("-", GUILayout.Width(30f))) Plugin.SetBaselinePlayers(Plugin.BaselinePlayersValue - 1);
            if (GUILayout.Button("+", GUILayout.Width(30f))) Plugin.SetBaselinePlayers(Plugin.BaselinePlayersValue + 1);
            GUILayout.EndHorizontal();
            GUILayout.Label(MenuText.Get("ExtraHp") + ": " + (Plugin.HealthPerExtraPlayerValue * 100f).ToString("0") + "%");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-5%")) Plugin.SetHealthPerExtraPlayer(Plugin.HealthPerExtraPlayerValue - 0.05f);
            if (GUILayout.Button("+5%")) Plugin.SetHealthPerExtraPlayer(Plugin.HealthPerExtraPlayerValue + 0.05f);
            GUILayout.EndHorizontal();
            GUILayout.Label(MenuText.Get("HpCap") + ": " + Plugin.MaximumMultiplierValue.ToString("0.##") + "x");
            if (GUILayout.Button("Cycle cap 4x / 8x / 12x / uncapped"))
            {
                float value = Plugin.MaximumMultiplierValue;
                Plugin.SetMaximumMultiplier(value < 4.1f ? 8f : value < 8.1f ? 12f : value < 12.1f ? 0f : 4f);
            }
            Plugin.scaleEnemyCount.Value = GUILayout.Toggle(Plugin.scaleEnemyCount.Value, MenuText.Get("EnemyCount"));
            GUILayout.Label(MenuText.Get("CountPerPlayer") + ": " + (Plugin.EnemyCountPerExtraPlayerValue * 100f).ToString("0") + "%");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-2%")) Plugin.SetEnemyCountPerExtraPlayer(Plugin.EnemyCountPerExtraPlayerValue - 0.02f);
            if (GUILayout.Button("+2%")) Plugin.SetEnemyCountPerExtraPlayer(Plugin.EnemyCountPerExtraPlayerValue + 0.02f);
            GUILayout.EndHorizontal();
            GUILayout.Label(MenuText.Get("CountCap") + ": " + Plugin.MaximumEnemyCountMultiplierValue.ToString("0.##") + "x");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-0.5x")) Plugin.SetMaximumEnemyCountMultiplier(Plugin.MaximumEnemyCountMultiplierValue - 0.5f);
            if (GUILayout.Button("+0.5x")) Plugin.SetMaximumEnemyCountMultiplier(Plugin.MaximumEnemyCountMultiplierValue + 0.5f);
            GUILayout.EndHorizontal();
            DrawPlayers();
            GUILayout.Space(8f);
            if (GUILayout.Button(MenuText.Get("Save"))) Plugin.SaveSettings();
            if (GUILayout.Button(MenuText.Get("Close"))) Toggle();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 22f));
        }

        private static void DrawPlayers()
        {
            GUILayout.Space(8f);
            int count = PlayerSpawner.MultiplayerList != null ? PlayerSpawner.MultiplayerList.Count : 0;
            GUILayout.Label(MenuText.Get("Players") + " (" + count + ")", section);
            if (PlayerSpawner.MultiplayerList == null) return;
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList.ToArray())
            {
                if (player == null || player.PlayerAvatar == null) continue;
                LevelController level = player.GetComponent<LevelController>();
                string status = player.PlayerAvatar.Name + " | " + MenuText.Get("Level") + " " + (level != null ? level.currentLevel : 0) +
                                " | HP " + player.PlayerAvatar.hp.ToString("0") + "/" + player.PlayerAvatar.MaxHp.ToString("0") +
                                " | " + MenuText.Get("Floor") + " " + (string.IsNullOrEmpty(player.PlayerAvatar.currentFloorGuid) ? "-" : player.PlayerAvatar.currentFloorGuid);
                if (player.isHost) status += " | " + MenuText.Get("Host");
                if (player.PlayerAvatar.IsDead) status += " | " + MenuText.Get("Dead");
                if (string.IsNullOrEmpty(player.PlayerAvatar.currentFloorGuid)) status += " | " + MenuText.Get("Loading");
                GUILayout.Label(status);
                if (!player.isHost && player.connectionToClient != null)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(MenuText.Get("Kick"))) Kick(player);
                    GUILayout.EndHorizontal();
                }
            }
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
