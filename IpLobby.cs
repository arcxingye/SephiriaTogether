using System;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class IpLobby
    {
        private static bool created;
        private static string roomName = "IP Room";
        private static int maxPlayers = 4;

        internal static bool IsCreated => created && IpTransport.IsActive && NetworkServer.active;
        internal static string RoomName => roomName;
        internal static int MaxPlayers => maxPlayers;

        internal static void ConfirmCreate(UI_MultiplayerPanel panel)
        {
            if (panel == null || !IpTransport.IsActive || !NetworkServer.active)
            {
                UIManager.Instance?.GetElement<UI_SystemMessage>()?.Open(MenuText.Get("IpRestartRequired"), 4f);
                return;
            }

            string requestedName = panel.lobbyNameInput != null ? panel.lobbyNameInput.text : "";
            requestedName = (requestedName ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (requestedName.Length == 0 || string.Equals(requestedName, "ERR", StringComparison.OrdinalIgnoreCase))
            {
                string playerName = SaveManager.Current != null ? SaveManager.Current.GetString("PlayerName", "") : "";
                requestedName = string.IsNullOrWhiteSpace(playerName) ? "IP Room" : playerName.Trim() + "'s World";
            }
            roomName = requestedName;
            maxPlayers = panel.memberBox != null ? panel.memberBox.CurrentSelection + 2 : PlayerLimit.CurrentLimit;
            created = true;
            HorayNetworkAuthenticator.allowConnection = true;
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.NetworklobbyCreatedPhase = 1;
                DungeonManager.Instance.NetworklobbyCreatedSteamId = 0;
            }

            ShowEnteredRoom(panel, roomName, "IP:" + IpTransport.ConfiguredPort);

            PlayerAvatar player = Traverse.Create(panel).Field("playerAvatar").GetValue<PlayerAvatar>();
            FloorData lobbyFloor = DungeonManager.Instance?.FindFloorByName("MultiZone");
            if (player != null && lobbyFloor != null && !DungeonManager.Instance.isRunStarted &&
                player.currentFloorGuid != lobbyFloor.guid)
            {
                DungeonManager.Instance.MoveFloor(player, lobbyFloor.guid, "FLOORSTARTING", 0,
                    recordHistory: false, allowSave: false, keepPrevFloor: false, randomPosition: true);
            }
            UIManager.Instance?.GetElement<UI_SystemMessage>()?.Open(
                string.Format(MenuText.Get("IpHostReady"), IpTransport.ConfiguredPort), 4f);
            Plugin.LogInfo($"IP lobby created: name={roomName}, port={IpTransport.ConfiguredPort}, max={maxPlayers}.");
        }

        internal static void Leave(UI_MultiplayerPanel panel)
        {
            PlayerAvatar player = panel != null
                ? Traverse.Create(panel).Field("playerAvatar").GetValue<PlayerAvatar>()
                : null;
            if (panel == null || player == null || player.isInDungeon > 0) return;
            UIManager.Instance.GetElement<UI_MessageBoxHolder>().OpenYesNo(
                panel.leaveLobbyMessageString.ToString(), () => LeaveConfirmed(panel, player), null);
        }

        internal static void Reset()
        {
            created = false;
            roomName = "IP Room";
            maxPlayers = 4;
        }

        internal static void ApplyPanelState(UI_MultiplayerPanel panel)
        {
            if (panel == null || !IpTransport.IsActive) return;
            if (IsCreated)
            {
                ShowEnteredRoom(panel, roomName, "IP:" + IpTransport.ConfiguredPort);
            }
            else if (NetworkClient.active && !NetworkServer.active)
            {
                string address = NetworkManager.singleton != null ? NetworkManager.singleton.networkAddress : "IP";
                ShowEnteredRoom(panel, MenuText.Get("IpJoinedRoom"), address + ":" + IpTransport.ConfiguredPort);
            }
        }

        private static void ShowEnteredRoom(UI_MultiplayerPanel panel, string name, string code)
        {
            panel.searchLobbyGroup.SetActive(false);
            panel.createLobbyGroup.SetActive(false);
            panel.enteredLobbyGroup.SetActive(true);
            if (panel.rejoinGroup != null) panel.rejoinGroup.SetActive(false);
            if (panel.roomNameText != null) panel.roomNameText.text = name;
            Traverse.Create(panel).Field("roomCode").SetValue(code);
            Traverse.Create(panel).Field("isRoomCodeHide").SetValue(false);
            panel.UpdateRoomCode(false);
            if (panel.enterMultizoneButton != null) panel.enterMultizoneButton.SetActive(false);
            if (panel.enterMultiZoneButton != null) panel.enterMultiZoneButton.gameObject.SetActive(false);
        }

        private static void LeaveConfirmed(UI_MultiplayerPanel panel, PlayerAvatar player)
        {
            List<NetworkConnectionToClient> clients = new List<NetworkConnectionToClient>();
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
                if (connection != null && connection != player.connectionToClient) clients.Add(connection);
            foreach (NetworkConnectionToClient client in clients) client.Disconnect();

            FloorData town = DungeonManager.Instance?.FindFloorByName("TheRabbittown");
            if (town != null)
            {
                DungeonManager.Instance.MoveFloor(player, town.guid, "FLOORSTARTING", 0,
                    recordHistory: false, allowSave: false, keepPrevFloor: false, randomPosition: true);
            }
            Reset();
            HorayNetworkAuthenticator.allowConnection = false;
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.NetworklobbyCreatedPhase = 0;
                DungeonManager.Instance.NetworklobbyCreatedSteamId = 0;
            }
            panel.searchLobbyGroup.SetActive(true);
            panel.createLobbyGroup.SetActive(false);
            panel.enteredLobbyGroup.SetActive(false);
            if (panel.rejoinGroup != null) panel.rejoinGroup.SetActive(false);
            Plugin.LogInfo("IP lobby closed by host.");
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnConfirmCreateButton))]
    internal static class IpLobbyConfirmCreatePatch
    {
        private static bool Prefix(UI_MultiplayerPanel __instance)
        {
            if (!IpTransport.IsActive) return true;
            UIManager.Instance.GetElement<UI_MessageBoxHolder>().OpenYesNo(
                __instance.createLobbyMessageString.ToString(), () => IpLobby.ConfirmCreate(__instance), null);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnOpened))]
    [HarmonyPriority(Priority.Last)]
    internal static class IpLobbyPanelStatePatch
    {
        private static void Postfix(UI_MultiplayerPanel __instance) => IpLobby.ApplyPanelState(__instance);
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnLeaveButton))]
    internal static class IpLobbyLeavePatch
    {
        private static bool Prefix(UI_MultiplayerPanel __instance)
        {
            if (!IpLobby.IsCreated) return true;
            IpLobby.Leave(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class IpLobbyServerStopPatch
    {
        private static void Postfix()
        {
            IpLobby.Reset();
            HorayNetworkAuthenticator.allowConnection = false;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartServer))]
    internal static class IpLobbyServerStartPatch
    {
        private static void Postfix()
        {
            if (!IpTransport.IsActive) return;
            IpLobby.Reset();
            HorayNetworkAuthenticator.allowConnection = false;
        }
    }
}
