using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal struct VersionCompatibilityHelloMessage : NetworkMessage
    {
        public string gameVersion;
        public string modVersion;
    }

    internal struct VersionCompatibilityNoticeMessage : NetworkMessage
    {
        public string gameVersion;
        public string modVersion;
    }

    internal static class VersionCompatibility
    {
        private const string LegacyGameVersion = "1.0.29";
        private const string LegacyModVersion = "3.7.0";
        private static readonly HashSet<int> HelloConnections = new HashSet<int>();
        private static readonly HashSet<int> ProtocolCompatibleConnections = new HashSet<int>();
        private static readonly Dictionary<int, string> RemoteGameVersions = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> RemoteModVersions = new Dictionary<int, string>();
        private static bool clientNoticeReceived;
        private static string authenticationTargetGameVersion = "";
        private static string remoteGameVersion = "";
        private static string remoteModVersion = "";
        private static string remoteRoomScope = "";
        private static bool authenticationTargetSent;
        private static bool legacyAuthenticationRetryAvailable;
        private static bool legacyAuthenticationRetryAttempted;
        private static bool clientProtocolConfirmed;
        private static bool remoteModMetadataAuthoritative;

        internal static void RegisterServerMessages()
        {
            ConfigureSerialization();
            NetworkServer.RegisterHandler<VersionCompatibilityHelloMessage>(OnServerHello, true);
        }

        internal static void TrackConnection(NetworkConnectionToClient connection)
        {
            if (connection == null || connection == NetworkServer.localConnection || Plugin.InstanceForPatches == null)
                return;
            Plugin.InstanceForPatches.StartCoroutine(WaitForHello(connection));
        }

        internal static void RecordGameVersion(NetworkConnectionToClient connection, string version)
        {
            if (connection != null) RemoteGameVersions[connection.connectionId] = version ?? "";
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            HelloConnections.Remove(connection.connectionId);
            ProtocolCompatibleConnections.Remove(connection.connectionId);
            RemoteGameVersions.Remove(connection.connectionId);
            RemoteModVersions.Remove(connection.connectionId);
        }

        internal static void ClearConnections()
        {
            HelloConnections.Clear();
            ProtocolCompatibleConnections.Clear();
            RemoteGameVersions.Clear();
            RemoteModVersions.Clear();
        }

        internal static void RegisterClientMessages()
        {
            ConfigureSerialization();
            clientNoticeReceived = false;
            authenticationTargetSent = false;
            legacyAuthenticationRetryAttempted = false;
            clientProtocolConfirmed = false;
            NetworkClient.RegisterHandler<VersionCompatibilityNoticeMessage>(OnClientNotice, true);
            if (NetworkServer.active)
            {
                clientProtocolConfirmed = true;
                return;
            }
            ShowPendingWarning();
            if (Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(SendHelloWhenReady());
        }

        internal static void PrepareLobbyJoin(LobbyData lobby)
        {
            ResetClientRoomState();
            string gameVersion = lobby["z_heathenGameVersion"] ?? "";
            authenticationTargetGameVersion = gameVersion;
            remoteGameVersion = gameVersion;
            remoteModVersion = lobby["SephiriaTogether"] ?? "";
            remoteRoomScope = "steam:" + lobby;
            remoteModMetadataAuthoritative = !string.IsNullOrEmpty(remoteModVersion);
        }

        internal static void PrepareCurrentSteamLobbyIfNeeded()
        {
            if (!string.IsNullOrEmpty(remoteRoomScope) &&
                !remoteRoomScope.StartsWith("steam:", StringComparison.Ordinal)) return;

            GameObject steamManager = SingletonObject.Find("SteamManager");
            if (steamManager == null || !App.Initialized ||
                !steamManager.TryGetComponent(out LobbyManager manager) || !manager.HasLobby ||
                manager.Lobby.IsOwner) return;

            string scope = "steam:" + manager.Lobby;
            if (!string.Equals(remoteRoomScope, scope, StringComparison.Ordinal) ||
                string.IsNullOrEmpty(remoteModVersion) || string.IsNullOrEmpty(authenticationTargetGameVersion))
                PrepareLobbyJoin(manager.Lobby);
        }

        internal static void PrepareEosJoin(EOSLobbyInfo info)
        {
            if (info == null) return;
            ResetClientRoomState();
            authenticationTargetGameVersion = info.version ?? "";
            remoteGameVersion = info.version ?? "";
            remoteRoomScope = "eos:" + info.lobbyId;
            if (!string.IsNullOrEmpty(remoteGameVersion) &&
                !string.Equals(remoteGameVersion, Application.version, StringComparison.OrdinalIgnoreCase))
                ShowMismatch(remoteGameVersion, "", remoteRoomScope);
            info.version = Application.version;
        }

        internal static void PrepareIpJoin(string gameVersion, string modVersion, string address, ushort port,
            bool metadataAuthoritative = false)
        {
            ResetClientRoomState();
            authenticationTargetGameVersion = gameVersion ?? "";
            remoteGameVersion = gameVersion ?? "";
            remoteModVersion = modVersion ?? "";
            remoteRoomScope = "ip:" + address + ":" + port;
            remoteModMetadataAuthoritative = metadataAuthoritative && !string.IsNullOrEmpty(remoteModVersion);
            legacyAuthenticationRetryAvailable = string.IsNullOrEmpty(gameVersion) &&
                string.Equals(Application.version, "1.0.30", StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(modVersion) ||
                 string.Equals(modVersion, LegacyModVersion, StringComparison.OrdinalIgnoreCase));
        }

        internal static void ClearClientTarget()
        {
            // Steam/IP room transitions stop the old client before starting the
            // new one. Keep the selected room metadata through that handoff.
            if (SteamInvitation.waitForExternalConnect && !string.IsNullOrEmpty(remoteRoomScope))
            {
                authenticationTargetSent = false;
                legacyAuthenticationRetryAttempted = false;
                clientNoticeReceived = false;
                clientProtocolConfirmed = false;
                return;
            }
            ResetClientRoomState();
        }

        internal static void AbortClientJoin() => ResetClientRoomState();

        internal static bool TryRetryLegacyAuthentication(
            HorayNetworkAuthenticator.VersionResponseMessage message)
        {
            if (message.success || !legacyAuthenticationRetryAvailable ||
                legacyAuthenticationRetryAttempted ||
                !remoteRoomScope.StartsWith("ip:", StringComparison.Ordinal) ||
                !string.Equals(message.errorMessage, "DIFFERENT_VERSION",
                    StringComparison.OrdinalIgnoreCase) ||
                !NetworkClient.active || NetworkClient.connection == null)
                return false;

            legacyAuthenticationRetryAttempted = true;
            NetworkClient.Send(new HorayNetworkAuthenticator.VersionMessage
            {
                version = LegacyGameVersion,
                playerGuid = HorayNetworkAuthenticator.GetLastRejoinGuid()
            });
            Plugin.LogInfo($"Retrying IP authentication for legacy game {LegacyGameVersion}: " +
                           $"localGame={Application.version}, scope={remoteRoomScope}.");
            ShowMismatch(LegacyGameVersion,
                string.IsNullOrEmpty(remoteModVersion) ? LegacyModVersion : remoteModVersion,
                remoteRoomScope);
            return true;
        }

        internal static void WarnLanRoom(string gameVersion, string modVersion, string address, ushort port)
        {
            PrepareIpJoin(gameVersion, modVersion, address, port, metadataAuthoritative: true);
            bool gameMismatch = !string.IsNullOrEmpty(gameVersion) &&
                                !string.Equals(gameVersion, Application.version, StringComparison.OrdinalIgnoreCase);
            bool modMismatch = !string.Equals(modVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase);
            if (gameMismatch || modMismatch)
                ShowMismatch(gameVersion, modVersion, "ip:" + address + ":" + port);
        }

        internal static bool HostSupportsProtocolMetadata()
        {
            if (NetworkServer.active) return true;
            return clientProtocolConfirmed;
        }

        internal static bool IsProtocolCompatibleConnection(NetworkConnectionToClient connection)
        {
            return connection != null && (connection.connectionId == 0 ||
                connection == NetworkServer.localConnection ||
                ProtocolCompatibleConnections.Contains(connection.connectionId));
        }

        internal static bool IsCrossVersionConnection(NetworkConnectionToClient connection)
        {
            if (connection == null) return false;
            bool gameMismatch = RemoteGameVersions.TryGetValue(connection.connectionId, out string gameVersion) &&
                                !string.IsNullOrEmpty(gameVersion) &&
                                !string.Equals(gameVersion, Application.version,
                                    StringComparison.OrdinalIgnoreCase);
            bool modMismatch = RemoteModVersions.TryGetValue(connection.connectionId, out string modVersion) &&
                               !string.IsNullOrEmpty(modVersion) &&
                               !string.Equals(modVersion, Plugin.PluginVersion,
                                   StringComparison.OrdinalIgnoreCase);
            return gameMismatch || modMismatch;
        }

        internal static bool IsPreparedForIp(string address, ushort port) =>
            string.Equals(remoteRoomScope, "ip:" + address + ":" + port, StringComparison.Ordinal);

        internal static bool CanAttemptProtocolHandshake()
        {
            // Mirror is configured by the game with exceptionsDisconnect=false.
            // Probe every remote session so a newer client can detect an older
            // or unmodded host without making custom features a prerequisite for
            // joining. Feature messages remain gated until the notice confirms
            // an identical game and Mod protocol.
            return !NetworkServer.active;
        }

        internal static bool SendTargetAuthenticationIfNeeded()
        {
            if (NetworkServer.active) return false;
            if (authenticationTargetSent) return true;
            if (string.IsNullOrEmpty(authenticationTargetGameVersion)) return false;
            string targetVersion = string.IsNullOrEmpty(authenticationTargetGameVersion)
                ? Application.version
                : authenticationTargetGameVersion;
            if (string.Equals(targetVersion, Application.version, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!NetworkClient.active || NetworkClient.connection == null) return false;
            NetworkClient.Send(new HorayNetworkAuthenticator.VersionMessage
            {
                version = targetVersion,
                playerGuid = HorayNetworkAuthenticator.GetLastRejoinGuid()
            });
            Plugin.LogInfo($"Version authentication sent: localGame={Application.version}, " +
                           $"targetGame={targetVersion}, scope={CurrentClientRoomScope()}.");
            authenticationTargetSent = true;
            return true;
        }

        internal static void InspectLobby(LobbyData lobby)
        {
            try
            {
                if (lobby.IsOwner) return;
                string gameVersion = lobby["z_heathenGameVersion"] ?? "";
                string modVersion = lobby["SephiriaTogether"] ?? "";
                bool gameMismatch = !string.IsNullOrEmpty(gameVersion) &&
                                    !string.Equals(gameVersion, Application.version, StringComparison.OrdinalIgnoreCase);
                bool modMismatch = !string.IsNullOrEmpty(modVersion) &&
                                   !string.Equals(modVersion, Plugin.PluginVersion,
                                       StringComparison.OrdinalIgnoreCase);
                if (!gameMismatch && !modMismatch) return;
                ShowMismatch(gameVersion, modVersion, "steam:" + lobby.ToString());
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("Version metadata inspection failed: " + exception.Message);
            }
        }

        private static void ShowMismatch(string remoteGameVersion, string remoteModVersion, string scope = null)
        {
            string message = string.Format(MenuText.Get("VersionMismatchWarning"),
                Application.version,
                string.IsNullOrEmpty(remoteGameVersion) ? MenuText.Get("VersionNotInstalled") : remoteGameVersion,
                Plugin.PluginVersion,
                string.IsNullOrEmpty(remoteModVersion) ? MenuText.Get("VersionNotInstalled") : remoteModVersion);
            scope = string.IsNullOrEmpty(scope) ? CurrentClientRoomScope() : scope;
            bool survivesNoLobby = scope.StartsWith("ip:", StringComparison.Ordinal) ||
                                   scope.StartsWith("eos:", StringComparison.Ordinal) || scope == "host-room";
            VersionReminder.ShowTemporary(message, scope, survivesNoLobby);
        }

        private static string CurrentClientRoomScope()
        {
            if (!string.IsNullOrEmpty(remoteRoomScope)) return remoteRoomScope;
            GameObject steamManager = SingletonObject.Find("SteamManager");
            if (steamManager != null && steamManager.TryGetComponent(out LobbyManager lobby) && lobby.HasLobby)
                return "steam:" + lobby.Lobby.ToString();
            string address = NetworkManager.singleton != null ? NetworkManager.singleton.networkAddress : "IP";
            return "ip:" + address + ":" + IpTransport.ActivePort;
        }

        private static IEnumerator SendHelloWhenReady()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (NetworkClient.active && (!NetworkClient.ready || !CanAttemptProtocolHandshake()) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (NetworkClient.active && NetworkClient.ready && CanAttemptProtocolHandshake())
            {
                Plugin.LogInfo($"Version handshake sent: localGame={Application.version}, " +
                               $"localMod={Plugin.PluginVersion}, scope={CurrentClientRoomScope()}.");
                NetworkClient.Send(new VersionCompatibilityHelloMessage
                {
                    gameVersion = Application.version,
                    modVersion = Plugin.PluginVersion
                });
                float noticeDeadline = Time.realtimeSinceStartup + 12f;
                while (NetworkClient.active && !clientNoticeReceived && Time.realtimeSinceStartup < noticeDeadline)
                    yield return null;
                if (NetworkClient.active && !clientNoticeReceived && !remoteModMetadataAuthoritative)
                    ShowMismatch(remoteGameVersion, "", remoteRoomScope);
            }
        }

        private static void OnServerHello(NetworkConnectionToClient connection, VersionCompatibilityHelloMessage message)
        {
            if (connection == null) return;
            HelloConnections.Add(connection.connectionId);
            RemoteModVersions[connection.connectionId] = message.modVersion ?? "";
            // The native authentication version may be the host's version when
            // a client had to bypass the stock game-version check. The custom
            // hello carries the client's actual version for diagnostics.
            string remoteGameVersion = message.gameVersion ?? "";
            if (string.IsNullOrEmpty(remoteGameVersion) &&
                RemoteGameVersions.TryGetValue(connection.connectionId, out string reported))
                remoteGameVersion = reported;
            bool gameMismatch = !string.Equals(remoteGameVersion, Application.version, StringComparison.OrdinalIgnoreCase);
            bool modMismatch = !string.Equals(message.modVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase);
            if (!gameMismatch && !modMismatch)
                ProtocolCompatibleConnections.Add(connection.connectionId);
            Plugin.LogInfo($"Version handshake received: conn={connection.connectionId}, " +
                           $"remoteGame={remoteGameVersion}, remoteMod={message.modVersion}, " +
                           $"protocolCompatible={!gameMismatch && !modMismatch}.");
            if (gameMismatch || modMismatch) ShowMismatch(remoteGameVersion, message.modVersion, "host-room");
            connection.Send(new VersionCompatibilityNoticeMessage
            {
                gameVersion = Application.version,
                modVersion = Plugin.PluginVersion
            });
        }

        private static IEnumerator WaitForHello(NetworkConnectionToClient connection)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (NetworkServer.active && connection != null &&
                   NetworkServer.connections.ContainsKey(connection.connectionId) &&
                   !HelloConnections.Contains(connection.connectionId) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            if (NetworkServer.active && connection != null &&
                NetworkServer.connections.ContainsKey(connection.connectionId) &&
                !HelloConnections.Contains(connection.connectionId))
            {
                string remoteGameVersion = RemoteGameVersions.TryGetValue(connection.connectionId, out string reported)
                    ? reported : Application.version;
                Plugin.LogInfo($"Version handshake not received: conn={connection.connectionId}, " +
                               $"remoteGame={remoteGameVersion}.");
                ShowMismatch(remoteGameVersion, MenuText.Get("VersionNotInstalled"), "host-room");
            }
        }

        private static void OnClientNotice(VersionCompatibilityNoticeMessage message)
        {
            clientNoticeReceived = true;
            remoteModVersion = message.modVersion ?? "";
            bool gameMismatch = !string.IsNullOrEmpty(message.gameVersion) &&
                                !string.Equals(message.gameVersion, Application.version,
                                    StringComparison.OrdinalIgnoreCase);
            bool modMismatch = !string.Equals(message.modVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase);
            clientProtocolConfirmed = !gameMismatch && !modMismatch;
            Plugin.LogInfo($"Version notice received: remoteGame={message.gameVersion}, " +
                           $"remoteMod={message.modVersion}, protocolCompatible={clientProtocolConfirmed}.");
            if (gameMismatch || modMismatch) ShowMismatch(message.gameVersion, message.modVersion);
            if (clientProtocolConfirmed && Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(SendCatchUpHelloNextFrame());
        }

        private static IEnumerator SendCatchUpHelloNextFrame()
        {
            yield return null;
            CatchUpRewards.SendHello();
        }

        private static void ShowPendingWarning()
        {
            bool gameMismatch = !string.IsNullOrEmpty(remoteGameVersion) &&
                                !string.Equals(remoteGameVersion, Application.version,
                                    StringComparison.OrdinalIgnoreCase);
            bool modMismatch = remoteModMetadataAuthoritative &&
                               !string.Equals(remoteModVersion, Plugin.PluginVersion,
                                   StringComparison.OrdinalIgnoreCase);
            if (gameMismatch || modMismatch)
                ShowMismatch(remoteGameVersion, remoteModVersion, remoteRoomScope);
        }

        private static void ConfigureSerialization()
        {
            Writer<VersionCompatibilityHelloMessage>.write = (writer, value) =>
            {
                writer.WriteString(value.gameVersion ?? "");
                writer.WriteString(value.modVersion ?? "");
            };
            Reader<VersionCompatibilityHelloMessage>.read = reader => new VersionCompatibilityHelloMessage
            {
                gameVersion = reader.ReadString(),
                modVersion = reader.ReadString()
            };
            Writer<VersionCompatibilityNoticeMessage>.write = (writer, value) =>
            {
                writer.WriteString(value.gameVersion ?? "");
                writer.WriteString(value.modVersion ?? "");
            };
            Reader<VersionCompatibilityNoticeMessage>.read = reader => new VersionCompatibilityNoticeMessage
            {
                gameVersion = reader.ReadString(),
                modVersion = reader.ReadString()
            };
        }

        private static void ResetClientRoomState()
        {
            authenticationTargetGameVersion = "";
            remoteGameVersion = "";
            remoteModVersion = "";
            remoteRoomScope = "";
            authenticationTargetSent = false;
            legacyAuthenticationRetryAvailable = false;
            legacyAuthenticationRetryAttempted = false;
            clientNoticeReceived = false;
            clientProtocolConfirmed = false;
            remoteModMetadataAuthoritative = false;
            VersionReminder.Clear();
        }
    }

    [HarmonyPatch(typeof(LobbyData), "get_GameVersion")]
    internal static class LobbyGameVersionCompatibilityPatch
    {
        private static bool Prefix(ref string __result)
        {
            // Preserve the raw metadata for VersionCompatibility, but make the
            // stock lobby UI and join validation non-blocking across game versions.
            __result = Application.version;
            return false;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), nameof(HorayNetworkAuthenticator.OnClientAuthenticate))]
    internal static class ClientAuthenticationTargetVersionPatch
    {
        private static bool Prefix() => !VersionCompatibility.SendTargetAuthenticationIfNeeded();
    }

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), "OnClientVersionResponseMessage")]
    internal static class ClientVersionResponseCompatibilityPatch
    {
        private static bool Prefix(HorayNetworkAuthenticator.VersionResponseMessage message) =>
            !VersionCompatibility.TryRetryLegacyAuthentication(message);
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnOpened))]
    internal static class LobbyOpenedVersionCompatibilityPatch
    {
        private static void Postfix()
        {
            GameObject steamManager = SingletonObject.Find("SteamManager");
            if (steamManager != null && App.Initialized &&
                steamManager.TryGetComponent(out LobbyManager manager) && manager.HasLobby)
                VersionCompatibility.InspectLobby(manager.Lobby);
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.HandleLeave))]
    internal static class LobbyLeaveVersionCompatibilityPatch
    {
        private static void Postfix() => VersionReminder.Clear();
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel_E), nameof(UI_MultiplayerPanel_E.HandleLeave))]
    internal static class EosLobbyLeaveVersionCompatibilityPatch
    {
        private static void Postfix() => VersionReminder.Clear();
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel_E), "HandleEnterFail")]
    internal static class EosLobbyJoinFailedVersionCompatibilityPatch
    {
        private static void Postfix() => VersionCompatibility.AbortClientJoin();
    }

    [HarmonyPatch(typeof(SteamInvitation), "HandleLobbyJoinFailed")]
    internal static class SteamInvitationJoinFailedVersionCompatibilityPatch
    {
        private static void Postfix() => VersionCompatibility.AbortClientJoin();
    }

    [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.Join), new[] { typeof(LobbyData) })]
    [HarmonyPriority(Priority.First)]
    internal static class LobbyJoinTargetVersionPatch
    {
        private static void Prefix(LobbyData lobby) => VersionCompatibility.PrepareLobbyJoin(lobby);
    }

    [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.Join), new[] { typeof(ulong) })]
    [HarmonyPriority(Priority.First)]
    internal static class LobbyJoinIdVersionCompatibilityPatch
    {
        private static void Prefix(ulong lobby) => VersionCompatibility.PrepareLobbyJoin(LobbyData.Get(lobby));
    }

    [HarmonyPatch(typeof(SteamInvitation), nameof(SteamInvitation.ConnectToOtherHost), new[] { typeof(string) })]
    internal static class SteamConnectionTargetVersionPatch
    {
        private static void Prefix() => VersionCompatibility.PrepareCurrentSteamLobbyIfNeeded();
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel_E), "HandleFound")]
    [HarmonyPriority(Priority.First)]
    internal static class EosLobbyFoundVersionCompatibilityPatch
    {
        private static bool Prefix(UI_MultiplayerPanel_E __instance, List<EOSLobbyInfo> lobbies)
        {
            if (lobbies == null) return false;
            foreach (EOSLobbyInfo lobby in lobbies)
            {
                if (lobby == null || __instance.lobbyElementPrefab == null || __instance.lobbyListZone == null) continue;
                UI_MultiplayerLobbyElement_E element =
                    UnityEngine.Object.Instantiate(__instance.lobbyElementPrefab, __instance.lobbyListZone);
                element.SetLobby(lobby, __instance);
                __instance.lobbyElements.Add(element);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel_E), "OnJoinButton", new[] { typeof(EOSLobbyInfo) })]
    [HarmonyPriority(Priority.First)]
    internal static class EosLobbyJoinVersionCompatibilityPatch
    {
        private static void Prefix(EOSLobbyInfo info, out string __state)
        {
            __state = info?.version ?? "";
            VersionCompatibility.PrepareEosJoin(info);
        }

        private static void Postfix(EOSLobbyInfo info, string __state)
        {
            if (info != null) info.version = __state;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), "OnServerVersionMessage")]
    [HarmonyPriority(Priority.Last)]
    internal static class ServerVersionCompatibilityPatch
    {
        private static void Prefix(NetworkConnectionToClient conn,
            ref HorayNetworkAuthenticator.VersionMessage message)
        {
            VersionCompatibility.RecordGameVersion(conn, message.version);
            // The stock authenticator rejects mismatched game versions before
            // the normal lobby admission and rejoin logic runs.
            message.version = Application.version;
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartServer))]
    internal static class VersionCompatibilityServerStartPatch
    {
        private static void Postfix() => VersionCompatibility.RegisterServerMessages();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStartClient))]
    internal static class VersionCompatibilityClientStartPatch
    {
        private static void Postfix() => VersionCompatibility.RegisterClientMessages();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopClient))]
    internal static class VersionCompatibilityClientStopPatch
    {
        private static void Postfix() => VersionCompatibility.ClearClientTarget();
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerAddPlayer))]
    internal static class VersionCompatibilityConnectionStartPatch
    {
        private static void Postfix(NetworkConnectionToClient conn) => VersionCompatibility.TrackConnection(conn);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnServerDisconnect))]
    internal static class VersionCompatibilityConnectionStopPatch
    {
        private static void Prefix(NetworkConnectionToClient conn) => VersionCompatibility.RemoveConnection(conn);
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.OnStopServer))]
    internal static class VersionCompatibilityServerStopPatch
    {
        private static void Postfix()
        {
            VersionCompatibility.ClearConnections();
            VersionReminder.Clear();
        }
    }
}
