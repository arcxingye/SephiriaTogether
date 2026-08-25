using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SephiriaTogether
{
    internal sealed class LanRoomElement : MonoBehaviour
    {
        internal string Address;
        internal ushort Port;
        internal string GameVersion;
        internal string ModVersion;
    }

    internal static class LanRoomListUi
    {
        private static UI_MultiplayerPanel activePanel;

        internal static void Open(UI_MultiplayerPanel panel)
        {
            activePanel = panel;
            Clear(panel);
        }

        internal static void ActivateIpMode(UI_MultiplayerPanel panel)
        {
            activePanel = panel;
            if (panel == null) return;
            if (panel.lobbyElements != null)
            {
                foreach (UI_MultiplayerLobbyElement element in panel.lobbyElements.ToArray())
                    if (element != null) UnityEngine.Object.Destroy(element.gameObject);
                panel.lobbyElements.Clear();
            }
            Clear(panel);
        }

        internal static void DeactivateIpMode(UI_MultiplayerPanel panel)
        {
            if (panel == null) return;
            if (panel.lobbyElements != null)
            {
                foreach (UI_MultiplayerLobbyElement element in panel.lobbyElements.ToArray())
                    if (element != null) UnityEngine.Object.Destroy(element.gameObject);
                panel.lobbyElements.Clear();
            }
            Clear(panel);
            activePanel = null;
        }

        internal static void Close() => activePanel = null;

        internal static void NotifyRoomsChanged()
        {
            if (activePanel == null || !IpTransport.IsActive || activePanel.lobbyListZone == null ||
                activePanel.searchLobbyGroup == null || !activePanel.searchLobbyGroup.activeSelf) return;
            Clear(activePanel);
            foreach (LanRoomDiscovery.Room room in LanRoomDiscovery.Snapshot()
                         .OrderBy(candidate => candidate.Name).ThenBy(candidate => candidate.Address))
            {
                UI_MultiplayerLobbyElement element =
                    UnityEngine.Object.Instantiate(activePanel.lobbyElementPrefab, activePanel.lobbyListZone);
                LanRoomElement tag = element.gameObject.AddComponent<LanRoomElement>();
                tag.Address = room.Address;
                tag.Port = room.Port;
                tag.GameVersion = room.GameVersion;
                tag.ModVersion = room.ModVersion;
                if (element.lobbyNameText != null) element.lobbyNameText.text = room.Name;
                if (element.lobbyMemberText != null)
                    element.lobbyMemberText.text = room.Players + " / " + room.MaxPlayers;
                if (element.chapterText != null) element.chapterText.text = "LAN " + room.Chapter;
            }
        }

        private static void Clear(UI_MultiplayerPanel panel)
        {
            if (panel?.lobbyListZone == null) return;
            for (int i = panel.lobbyListZone.childCount - 1; i >= 0; i--)
            {
                Transform child = panel.lobbyListZone.GetChild(i);
                if (child != null && child.GetComponent<LanRoomElement>() != null)
                    UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnOpened))]
    internal static class LanRoomPanelOpenPatch
    {
        private static void Postfix(UI_MultiplayerPanel __instance) => LanRoomListUi.Open(__instance);
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnClosed))]
    internal static class LanRoomPanelClosePatch
    {
        private static void Postfix() => LanRoomListUi.Close();
    }

    [HarmonyPatch(typeof(UI_MultiplayerLobbyElement), nameof(UI_MultiplayerLobbyElement.OnClick))]
    internal static class LanRoomElementClickPatch
    {
        private static bool Prefix(UI_MultiplayerLobbyElement __instance)
        {
            LanRoomElement room = __instance.GetComponent<LanRoomElement>();
            if (room == null) return true;
            VersionCompatibility.WarnLanRoom(room.GameVersion, room.ModVersion, room.Address, room.Port);
            IpTransport.JoinRoom(room.Address, room.Port);
            return false;
        }
    }
}
