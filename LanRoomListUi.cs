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
    }

    internal static class LanRoomListUi
    {
        private static UI_MultiplayerPanel activePanel;

        internal static void Open(UI_MultiplayerPanel panel)
        {
            activePanel = panel;
            Clear(panel);
            if (panel != null && panel.searchLobbyGroup.activeSelf) LanRoomDiscovery.Refresh();
        }

        internal static void Close() => activePanel = null;

        internal static void NotifyRoomsChanged()
        {
            if (activePanel == null) return;
            Clear(activePanel);
            foreach (LanRoomDiscovery.Room room in LanRoomDiscovery.Snapshot()
                         .OrderBy(candidate => candidate.Name).ThenBy(candidate => candidate.Address))
            {
                UI_MultiplayerLobbyElement element =
                    UnityEngine.Object.Instantiate(activePanel.lobbyElementPrefab, activePanel.lobbyListZone);
                LanRoomElement tag = element.gameObject.AddComponent<LanRoomElement>();
                tag.Address = room.Address;
                tag.Port = room.Port;
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

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.RefreshLobbyList))]
    internal static class LanRoomRefreshPatch
    {
        private static void Postfix() => LanRoomDiscovery.Refresh();
    }

    [HarmonyPatch(typeof(UI_MultiplayerLobbyElement), nameof(UI_MultiplayerLobbyElement.OnClick))]
    internal static class LanRoomElementClickPatch
    {
        private static bool Prefix(UI_MultiplayerLobbyElement __instance)
        {
            LanRoomElement room = __instance.GetComponent<LanRoomElement>();
            if (room == null) return true;
            IpTransport.JoinRoom(room.Address, room.Port);
            return false;
        }
    }
}
