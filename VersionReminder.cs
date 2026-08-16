using System;
using HeathenEngineering.SteamworksIntegration;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class VersionReminder
    {
        private static string hostVersion = "";
        private static int comparison;
        private static GUIStyle boxStyle;
        private static GUIStyle buttonStyle;
        private static Texture2D boxTexture;

        internal static void Update()
        {
            GameObject steamManager = SingletonObject.Find("SteamManager");
            if (steamManager == null || !steamManager.TryGetComponent(out LobbyManager manager) || !manager.HasLobby)
            {
                hostVersion = "";
                comparison = 0;
                return;
            }
            string value = manager.Lobby["SephiriaTogether"] ?? "";
            if (value == hostVersion) return;
            hostVersion = value;
            comparison = Compare(Plugin.PluginVersion, hostVersion);
        }

        internal static void Draw()
        {
            if (comparison == 0 || string.IsNullOrEmpty(hostVersion)) return;
            EnsureStyles();
            Rect area = new Rect(Mathf.Max(12f, Screen.width * 0.5f - 320f), 132f,
                Mathf.Min(640f, Screen.width - 24f), 104f);
            GUILayout.BeginArea(area, boxStyle);
            string message = comparison < 0
                ? string.Format(MenuText.Get("ClientModOutdated"), Plugin.PluginVersion, hostVersion)
                : string.Format(MenuText.Get("HostModOutdated"), Plugin.PluginVersion, hostVersion);
            GUILayout.Label(message, boxStyle);
            if (comparison < 0 && GUILayout.Button(MenuText.Get("OpenPluginDownload"), buttonStyle, GUILayout.Height(30f)))
                Application.OpenURL(CatchUpRewards.PluginZipUrl);
            GUILayout.EndArea();
        }

        internal static void Clear()
        {
            hostVersion = "";
            comparison = 0;
        }

        private static int Compare(string client, string host)
        {
            if (string.Equals(client, host, StringComparison.OrdinalIgnoreCase)) return 0;
            return Version.TryParse(client, out Version clientVersion) && Version.TryParse(host, out Version hostVersionValue)
                ? clientVersion.CompareTo(hostVersionValue)
                : string.Compare(client, host, StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureStyles()
        {
            if (boxStyle != null) return;
            boxTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            boxTexture.SetPixel(0, 0, new Color(0.55f, 0.25f, 0.02f, 0.98f));
            boxTexture.Apply();
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(14, 14, 10, 10)
            };
            boxStyle.normal.background = boxTexture;
            boxStyle.normal.textColor = Color.white;
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
        }
    }
}
