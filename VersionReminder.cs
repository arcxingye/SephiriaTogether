using System;
using System.Collections.Generic;
using HeathenEngineering.SteamworksIntegration;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class VersionReminder
    {
        private static string warningText = "";
        private static float warningExpiresAt;
        private static bool warningSurvivesNoLobby;
        private static string warningScope = "";
        private static string observedLobbySignature = "";
        private static float observedLobbySince;
        private static readonly HashSet<string> ShownScopes = new HashSet<string>();
        private static GUIStyle boxStyle;
        private static Texture2D boxTexture;

        internal static void Update()
        {
            GameObject steamManager = SingletonObject.Find("SteamManager");
            LobbyManager manager = null;
            bool hasLobby = steamManager != null &&
                            steamManager.TryGetComponent(out manager) && manager.HasLobby;
            if (!hasLobby)
            {
                observedLobbySignature = "";
                ShownScopes.RemoveWhere(scope => scope.StartsWith("steam:", StringComparison.Ordinal));
                if (!warningSurvivesNoLobby) ClearWarning();
                return;
            }

            LobbyData lobby = manager.Lobby;
            if (lobby.IsOwner) return;
            string gameVersion = lobby["z_heathenGameVersion"] ?? "";
            string modVersion = lobby["SephiriaTogether"] ?? "";
            string signature = gameVersion + "\n" + modVersion;
            if (signature != observedLobbySignature)
            {
                observedLobbySignature = signature;
                observedLobbySince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - observedLobbySince < 1.5f) return;

            bool gameMismatch = !string.IsNullOrEmpty(gameVersion) &&
                                !string.Equals(gameVersion, Application.version, StringComparison.OrdinalIgnoreCase);
            bool modMismatch = !string.IsNullOrEmpty(modVersion) &&
                               !string.Equals(modVersion, Plugin.PluginVersion,
                                   StringComparison.OrdinalIgnoreCase);
            if (gameMismatch || modMismatch)
            {
                ShowTemporary(string.Format(MenuText.Get("VersionMismatchWarning"),
                    Application.version,
                    string.IsNullOrEmpty(gameVersion) ? MenuText.Get("VersionNotInstalled") : gameVersion,
                    Plugin.PluginVersion,
                    string.IsNullOrEmpty(modVersion) ? MenuText.Get("VersionNotInstalled") : modVersion),
                    "steam:" + manager.Lobby.ToString());
            }
        }

        internal static void ShowTemporary(string message, string scope = null, bool survivesNoLobby = false)
        {
            message = message ?? "";
            if (string.IsNullOrEmpty(message)) return;
            string key = string.IsNullOrEmpty(scope) ? message : scope;
            if (!ShownScopes.Add(key))
            {
                if (string.Equals(warningScope, key, StringComparison.Ordinal) &&
                    Time.unscaledTime < warningExpiresAt)
                    warningText = message;
                return;
            }
            warningScope = key;
            warningText = message;
            warningExpiresAt = Time.unscaledTime + 20f;
            warningSurvivesNoLobby = survivesNoLobby;
        }

        internal static void Draw()
        {
            if (string.IsNullOrEmpty(warningText) || Time.unscaledTime >= warningExpiresAt) return;
            EnsureStyles();
            Rect area = new Rect(Mathf.Max(12f, Screen.width * 0.5f - 360f), 132f,
                Mathf.Min(720f, Screen.width - 24f), 132f);
            GUILayout.BeginArea(area, boxStyle);
            int seconds = Mathf.Max(0, Mathf.CeilToInt(warningExpiresAt - Time.unscaledTime));
            GUILayout.Label(warningText + "\n" +
                            string.Format(MenuText.Get("VersionWarningCountdown"), seconds), boxStyle);
            GUILayout.EndArea();
        }

        internal static void Clear()
        {
            ClearWarning();
            ShownScopes.Clear();
        }

        private static void ClearWarning()
        {
            warningText = "";
            warningExpiresAt = 0f;
            warningSurvivesNoLobby = false;
            warningScope = "";
            observedLobbySignature = "";
            observedLobbySince = 0f;
        }

        private static void EnsureStyles()
        {
            if (boxStyle != null) return;
            boxTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            boxTexture.SetPixel(0, 0, new Color(0.55f, 0.25f, 0.02f, 0.98f));
            boxTexture.Apply();
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(14, 14, 10, 10)
            };
            boxStyle.normal.background = boxTexture;
            boxStyle.normal.textColor = Color.white;
        }
    }
}
