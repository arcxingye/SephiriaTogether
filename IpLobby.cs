using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class IpLobby
    {
        private static bool created;
        private static bool joined;
        private static string joinedAddress;
        private static ushort joinedPort;
        internal static bool IpServerStarted;
        private static string roomName = "IP Room";
        private static int maxPlayers = 4;
        private static int lobbyGeneration;
        private static int restorePendingGeneration = -1;
        private static readonly Dictionary<PlayerSpawner, int> ReturnToLobbyScheduled =
            new Dictionary<PlayerSpawner, int>();

        internal static bool IsCreated => created && IpTransport.IsActive && NetworkServer.active;
        internal static bool IsJoined => joined && IpTransport.IsActive && NetworkClient.active && !NetworkServer.active;
        internal static string RoomName => roomName;
        internal static int MaxPlayers => maxPlayers;

        internal static void MarkJoined(string address, ushort port)
        {
            joined = true;
            joinedAddress = address;
            joinedPort = port;
            UI_MultiplayerPanel panel = UIManager.Instance?.GetElement<UI_MultiplayerPanel>();
            if (panel != null) ApplyPanelState(panel);
            Plugin.LogInfo($"IP join started: address={address}, port={port}.");
        }

        internal static void ConfirmCreate(UI_MultiplayerPanel panel)
        {
            if (panel == null || !IpTransport.IsActive)
            {
                UIManager.Instance?.GetElement<UI_SystemMessage>()?.Open(MenuText.Get("IpRestartRequired"), 4f);
                return;
            }

            if (!NetworkServer.active)
            {
                UIManager.Instance?.GetElement<UI_SystemMessage>()?.Open(MenuText.Get("IpCreateUnavailable"), 4f);
                return;
            }

            if (!(NetworkManager.singleton?.transport is TelepathyTransport))
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
            maxPlayers = Mathf.Clamp(maxPlayers, 2, PlayerLimit.CurrentLimit);
            NetworkManager.singleton.maxConnections = maxPlayers;
            NetworkServer.maxConnections = maxPlayers;
            lobbyGeneration++;
            restorePendingGeneration = -1;
            created = true;
            HorayNetworkAuthenticator.allowConnection = true;
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.NetworklobbyCreatedPhase = 1;
                DungeonManager.Instance.NetworklobbyCreatedSteamId = 0;
            }

            ShowEnteredRoom(panel, roomName, "IP:" + IpTransport.ActivePort);

            PlayerAvatar player = Traverse.Create(panel).Field("playerAvatar").GetValue<PlayerAvatar>();
            FloorData lobbyFloor = DungeonManager.Instance?.FindFloorByName("MultiZone");
            if (player != null && lobbyFloor != null && !DungeonManager.Instance.isRunStarted &&
                player.currentFloorGuid != lobbyFloor.guid)
            {
                DungeonManager.Instance.MoveFloor(player, lobbyFloor.guid, "FLOORSTARTING", 0,
                    recordHistory: false, allowSave: false, keepPrevFloor: false, randomPosition: true);
            }
            UIManager.Instance?.GetElement<UI_SystemMessage>()?.Open(
                string.Format(MenuText.Get("IpHostReady"), IpTransport.ActivePort), 4f);
            Plugin.LogInfo($"IP lobby created: name={roomName}, port={IpTransport.ActivePort}, max={maxPlayers}.");
        }

        internal static void Leave(UI_MultiplayerPanel panel)
        {
            PlayerAvatar player = panel != null
                ? Traverse.Create(panel).Field("playerAvatar").GetValue<PlayerAvatar>()
                : null;
            if (panel == null || player == null) return;
            if (player.isInDungeon > 0 && !(DungeonManager.Instance?.IsInMultiZone(player) ?? false)) return;
            UIManager.Instance.GetElement<UI_MessageBoxHolder>().OpenYesNo(
                panel.leaveLobbyMessageString.ToString(), () => LeaveConfirmed(panel, player), null);
        }

        internal static void Reset()
        {
            lobbyGeneration++;
            restorePendingGeneration = -1;
            created = false;
            joined = false;
            joinedAddress = null;
            joinedPort = 0;
            roomName = "IP Room";
            maxPlayers = 4;
            ReturnToLobbyScheduled.Clear();
        }

        internal static void MarkRestartPending()
        {
            if (!IsCreated) return;
            restorePendingGeneration = lobbyGeneration;
            Plugin.LogInfo("IP lobby restore armed for game restart.");
        }

        internal static void ApplyPanelState(UI_MultiplayerPanel panel)
        {
            if (panel == null || !IpTransport.IsActive) return;
            if (IsCreated)
            {
                ShowEnteredRoom(panel, roomName, "IP:" + IpTransport.ActivePort);
            }
            else if (IsJoined)
            {
                string address = string.IsNullOrWhiteSpace(joinedAddress)
                    ? NetworkManager.singleton != null ? NetworkManager.singleton.networkAddress : "IP"
                    : joinedAddress;
                ShowEnteredRoom(panel, MenuText.Get("IpJoinedRoom"), address + ":" + joinedPort);
            }
        }

        private static void ShowEnteredRoom(UI_MultiplayerPanel panel, string name, string code)
        {
            if (panel.searchLobbyGroup != null) panel.searchLobbyGroup.SetActive(false);
            if (panel.createLobbyGroup != null) panel.createLobbyGroup.SetActive(false);
            if (panel.enteredLobbyGroup != null) panel.enteredLobbyGroup.SetActive(true);
            if (panel.rejoinGroup != null) panel.rejoinGroup.SetActive(false);
            if (panel.roomNameText != null) panel.roomNameText.text = name;
            Traverse.Create(panel).Field("roomCode").SetValue(code);
            Traverse.Create(panel).Field("isRoomCodeHide").SetValue(false);
            panel.UpdateRoomCode(false);
            if (panel.enterMultizoneButton != null)
                panel.enterMultizoneButton.SetActive(IsCreated && CanEnterMultiZone(panel));
            if (panel.enterMultiZoneButton != null)
                panel.enterMultiZoneButton.gameObject.SetActive(IsCreated && CanEnterMultiZone(panel));
            Traverse.Create(panel).Method("RefreshDefaultSelectable").GetValue();
        }

        private static bool CanEnterMultiZone(UI_MultiplayerPanel panel)
        {
            if (!IsCreated || !NetworkServer.active || DungeonManager.Instance == null ||
                DungeonManager.Instance.isRunStarted)
                return false;
            PlayerAvatar player = GetPanelPlayer(panel);
            FloorData lobbyFloor = DungeonManager.Instance.FindFloorByName("MultiZone");
            return player != null && lobbyFloor != null && player.currentFloorGuid != lobbyFloor.guid &&
                   player.loadingScreenType == -1;
        }

        private static bool IsInLobby(PlayerAvatar player)
        {
            FloorData lobbyFloor = DungeonManager.Instance?.FindFloorByName("MultiZone");
            return player != null && lobbyFloor != null && player.currentFloorGuid == lobbyFloor.guid &&
                   player.loadingScreenType == -1;
        }

        internal static void EnterMultiZone(UI_MultiplayerPanel panel)
        {
            if (!IsCreated || !NetworkServer.active || DungeonManager.Instance == null ||
                DungeonManager.Instance.isRunStarted)
                return;
            PlayerAvatar player = GetPanelPlayer(panel);
            FloorData lobbyFloor = DungeonManager.Instance.FindFloorByName("MultiZone");
            if (player == null || lobbyFloor == null || player.currentFloorGuid == lobbyFloor.guid ||
                player.loadingScreenType != -1)
                return;
            DungeonManager.Instance.MoveFloor(player, lobbyFloor.guid, "FLOORSTARTING", 0,
                recordHistory: false, allowSave: false, keepPrevFloor: false, randomPosition: true);
            DungeonManager.Instance.NetworklobbyCreatedPhase = 1;
            DungeonManager.Instance.NetworklobbyCreatedSteamId = 0;
            HorayNetworkAuthenticator.allowConnection = true;
            Plugin.LogInfo("IP host entered the virtual multiplayer lobby.");
        }

        internal static void ScheduleReturnToLobby(PlayerSpawner player)
        {
            int generation = lobbyGeneration;
            if (!IsRestorePending(generation) || player == null ||
                !IsLocalHost(player) || Plugin.InstanceForPatches == null ||
                ReturnToLobbyScheduled.ContainsKey(player))
                return;
            ReturnToLobbyScheduled[player] = generation;
            Plugin.InstanceForPatches.StartCoroutine(ReturnToLobbyAfterRestart(player, generation));
        }

        private static IEnumerator ReturnToLobbyAfterRestart(PlayerSpawner player, int generation)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            try
            {
                // RestartNewGame queues the native town/floor move. Wait for that
                // transaction to finish before requesting MultiZone, otherwise the
                // two moves can race and leave the host in town.
                yield return null;
                bool ready = false;
                while (IsRestorePending(generation) && player != null && player.PlayerAvatar != null &&
                       Time.realtimeSinceStartup < deadline)
                {
                    if (IsInLobby(player.PlayerAvatar))
                    {
                        ready = true;
                        break;
                    }
                    if (!string.IsNullOrEmpty(player.PlayerAvatar.currentFloorGuid) &&
                        player.PlayerAvatar.loadingScreenType == -1 &&
                        DungeonManager.Instance != null && !DungeonManager.Instance.isRunStarted)
                    {
                        ready = true;
                        break;
                    }
                    yield return null;
                }

                if (!ready || !IsRestorePending(generation) || player == null || player.PlayerAvatar == null ||
                    DungeonManager.Instance == null || DungeonManager.Instance.isRunStarted)
                {
                    Plugin.LogInfo($"IP lobby restore skipped before floor move: player={player?.PlayerAvatar?.Name}, " +
                                   $"ready={ready}, runStarted={DungeonManager.Instance?.isRunStarted}, " +
                                   $"floor={player?.PlayerAvatar?.currentFloorGuid}.");
                    yield break;
                }

                FloorData lobbyFloor = DungeonManager.Instance.FindFloorByName("MultiZone");
                if (lobbyFloor == null) yield break;
                if (player.PlayerAvatar.currentFloorGuid != lobbyFloor.guid)
                {
                    DungeonManager.Instance.MoveFloor(player.PlayerAvatar, lobbyFloor.guid, "FLOORSTARTING", 0,
                        recordHistory: false, allowSave: false, keepPrevFloor: false, randomPosition: true);
                    float moveDeadline = Time.realtimeSinceStartup + 15f;
                    while (IsRestorePending(generation) && player != null && player.PlayerAvatar != null &&
                           !IsInLobby(player.PlayerAvatar) &&
                           Time.realtimeSinceStartup < moveDeadline)
                        yield return null;
                }

                if (!IsRestorePending(generation) || player == null || player.PlayerAvatar == null ||
                    !IsInLobby(player.PlayerAvatar) || FloorGenerator.FindByGuid(lobbyFloor.guid) == null)
                {
                    Plugin.LogInfo($"IP lobby restore floor move did not complete: player={player?.PlayerAvatar?.Name}, " +
                                   $"floor={player?.PlayerAvatar?.currentFloorGuid}, expected={lobbyFloor.guid}.");
                    yield break;
                }

                DungeonManager.Instance.NetworklobbyCreatedPhase = 1;
                DungeonManager.Instance.NetworklobbyCreatedSteamId = 0;
                HorayNetworkAuthenticator.allowConnection = true;
                ClearRestorePending(generation);
                UI_MultiplayerPanel panel = UIManager.Instance?.GetElement<UI_MultiplayerPanel>();
                if (panel != null) ApplyPanelState(panel);
                Plugin.LogInfo($"IP host returned to the virtual multiplayer lobby: player={player.PlayerAvatar.Name}, " +
                               $"floor={player.PlayerAvatar.currentFloorGuid}.");
            }
            finally
            {
                ClearRestorePending(generation);
                if (ReturnToLobbyScheduled.TryGetValue(player, out int scheduledGeneration) &&
                    scheduledGeneration == generation)
                    ReturnToLobbyScheduled.Remove(player);
            }
        }

        private static bool IsCurrentLobby(int generation)
        {
            return created && lobbyGeneration == generation && NetworkServer.active;
        }

        private static bool IsRestorePending(int generation)
        {
            return IsCurrentLobby(generation) && restorePendingGeneration == generation;
        }

        private static void ClearRestorePending(int generation)
        {
            if (restorePendingGeneration == generation)
                restorePendingGeneration = -1;
        }

        private static PlayerAvatar GetPanelPlayer(UI_MultiplayerPanel panel)
        {
            PlayerAvatar player = panel != null
                ? Traverse.Create(panel).Field("playerAvatar").GetValue<PlayerAvatar>()
                : null;
            return player ?? FindLocalHost()?.PlayerAvatar;
        }

        private static PlayerSpawner FindLocalHost()
        {
            if (PlayerSpawner.MultiplayerList == null) return null;
            return PlayerSpawner.MultiplayerList.FirstOrDefault(player =>
                       player != null && player.connectionToClient == NetworkServer.localConnection) ??
                   PlayerSpawner.MultiplayerList.FirstOrDefault(IsLocalHost);
        }

        private static bool IsLocalHost(PlayerSpawner player)
        {
            return player != null && (player.isHost || player.isOwned ||
                player.connectionToClient == NetworkServer.localConnection);
        }

        internal static void ResetClientPanel()
        {
            Reset();
            UI_MultiplayerPanel panel = UIManager.Instance?.GetElement<UI_MultiplayerPanel>();
            if (panel == null) return;
            if (panel.searchLobbyGroup != null) panel.searchLobbyGroup.SetActive(true);
            if (panel.createLobbyGroup != null) panel.createLobbyGroup.SetActive(false);
            if (panel.enteredLobbyGroup != null) panel.enteredLobbyGroup.SetActive(false);
            if (panel.rejoinGroup != null) panel.rejoinGroup.SetActive(false);
            Traverse.Create(panel).Method("RefreshDefaultSelectable").GetValue();
            if (IpTransport.IsActive)
            {
                LanRoomListUi.ActivateIpMode(panel);
                LanRoomDiscovery.Refresh();
            }
        }

        private static void LeaveConfirmed(UI_MultiplayerPanel panel, PlayerAvatar player)
        {
            Plugin.LogInfo($"IP host leaving virtual lobby: player={player?.Name}, floor={player?.currentFloorGuid}, " +
                           $"inDungeon={player?.isInDungeon}.");
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
            VersionReminder.Clear();
            NetworkServer.maxConnections = PlayerLimit.CurrentLimit;
            HorayNetworkAuthenticator.allowConnection = false;
            if (DungeonManager.Instance != null)
            {
                DungeonManager.Instance.NetworklobbyCreatedPhase = 0;
                DungeonManager.Instance.NetworklobbyCreatedSteamId = 0;
            }
            if (panel.searchLobbyGroup != null) panel.searchLobbyGroup.SetActive(true);
            if (panel.createLobbyGroup != null) panel.createLobbyGroup.SetActive(false);
            if (panel.enteredLobbyGroup != null) panel.enteredLobbyGroup.SetActive(false);
            if (panel.rejoinGroup != null) panel.rejoinGroup.SetActive(false);
            Plugin.LogInfo("IP lobby closed by host.");
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnConfirmCreateButton))]
    internal static class IpLobbyConfirmCreatePatch
    {
        private static bool Prefix(UI_MultiplayerPanel __instance)
        {
            if (!IpTransport.ShouldUseIpForUi) return true;
            IpTransport.EnsureInstalled();
            if (!IpTransport.IsActive)
            {
                IpTransport.ShowMessage(MenuText.Get("IpRestartRequired"));
                return false;
            }
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

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.EnterMultiZone))]
    internal static class IpLobbyEnterMultiZonePatch
    {
        private static bool Prefix(UI_MultiplayerPanel __instance)
        {
            if (!IpTransport.IsActive) return true;
            if (NetworkServer.active) IpLobby.EnterMultiZone(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerSpawner), "Initialize")]
    internal static class IpLobbyInitializePlayerPatch
    {
        private static void Postfix(PlayerSpawner __instance) => IpLobby.ScheduleReturnToLobby(__instance);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.RestartGame))]
    internal static class IpLobbyRestartGamePatch
    {
        private static void Prefix() => IpLobby.MarkRestartPending();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class IpLobbyServerStopPatch
    {
        private static void Postfix()
        {
            if (!IpLobby.IpServerStarted && !IpTransport.IsActive) return;
            IpLobby.Reset();
            IpLobby.IpServerStarted = false;
            HorayNetworkAuthenticator.allowConnection = false;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartServer))]
    internal static class IpLobbyServerStartPatch
    {
        private static void Postfix()
        {
            if (!IpTransport.IsActive) return;
            IpLobby.IpServerStarted = true;
            IpLobby.Reset();
            HorayNetworkAuthenticator.allowConnection = false;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopClient))]
    internal static class IpLobbyClientStopPatch
    {
        private static void Postfix()
        {
            if (!NetworkServer.active) IpLobby.ResetClientPanel();
        }
    }
}
