using System;
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
        private static readonly HashSet<NetworkConnectionToClient> HelloConnections = new HashSet<NetworkConnectionToClient>();
        private static readonly HashSet<NetworkConnectionToClient> ProtocolCompatibleConnections = new HashSet<NetworkConnectionToClient>();
        private static readonly Dictionary<NetworkConnectionToClient, string> RemoteGameVersions = new Dictionary<NetworkConnectionToClient, string>();
        private static readonly Dictionary<NetworkConnectionToClient, string> RemoteModVersions = new Dictionary<NetworkConnectionToClient, string>();
        private static readonly Dictionary<NetworkConnectionToClient, float> PendingConnections = new Dictionary<NetworkConnectionToClient, float>();
        private static readonly List<NetworkConnectionToClient> PendingSnapshot = new List<NetworkConnectionToClient>();
        private static NetworkConnectionToServer clientConnection;
        private static bool clientMessagesRegistered;
        private static float clientReadySince = -1f;
        private static float nextHelloAt;
        private static int helloAttempts;
        private static bool clientPendingShown;
        private const float PendingWarningDelay = 30f;
        private static bool clientNoticeReceived;
        private static string remoteGameVersion = "";
        private static string remoteModVersion = "";
        private static string remoteRoomScope = "";
        private static bool clientProtocolConfirmed;
        private static bool remoteModMetadataAuthoritative;
        internal static bool HasClientVersionReport => NetworkClient.active && clientNoticeReceived &&
            ReferenceEquals(clientConnection, NetworkClient.connection);

        internal static void RegisterServerMessages()
        {
            ConfigureSerialization();
            NetworkServer.RegisterHandler<VersionCompatibilityHelloMessage>(OnServerHello, true);
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
                TrackConnection(connection);
        }

        internal static void TrackConnection(NetworkConnectionToClient connection)
        {
            if (connection == null || connection == NetworkServer.localConnection ||
                HelloConnections.Contains(connection) || PendingConnections.ContainsKey(connection))
                return;
            PendingConnections[connection] = -1f;
        }

        internal static void RecordGameVersion(NetworkConnectionToClient connection, string version)
        {
            if (connection != null) RemoteGameVersions[connection] = version ?? "";
        }

        internal static void RemoveConnection(NetworkConnectionToClient connection)
        {
            if (connection == null) return;
            HelloConnections.Remove(connection);
            ProtocolCompatibleConnections.Remove(connection);
            RemoteGameVersions.Remove(connection);
            RemoteModVersions.Remove(connection);
            PendingConnections.Remove(connection);
            VersionReminder.Dismiss(ServerWarningScope(connection));
        }

        internal static void ClearConnections()
        {
            foreach (NetworkConnectionToClient connection in RemoteModVersions.Keys)
                VersionReminder.Dismiss(ServerWarningScope(connection));
            foreach (NetworkConnectionToClient connection in PendingConnections.Keys)
                VersionReminder.Dismiss(ServerWarningScope(connection));
            HelloConnections.Clear();
            ProtocolCompatibleConnections.Clear();
            RemoteGameVersions.Clear();
            RemoteModVersions.Clear();
            PendingConnections.Clear();
        }

        internal static void RegisterClientMessages()
        {
            ConfigureSerialization();
            NetworkClient.RegisterHandler<VersionCompatibilityNoticeMessage>(OnClientNotice, true);
            // Replacing the handler is harmless; resetting an established
            // handshake here loses confirmation during late Workshop activation.
            clientMessagesRegistered = true;
            if (NetworkServer.active) return;
            ShowPendingWarning();
        }

        internal static void PrepareLobbyJoin(LobbyData lobby)
        {
            ResetClientRoomState();
            string gameVersion = lobby["z_heathenGameVersion"] ?? "";
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
            if (!string.Equals(remoteRoomScope, scope, StringComparison.Ordinal))
                PrepareLobbyJoin(manager.Lobby);
            else if (!clientNoticeReceived)
            {
                remoteGameVersion = manager.Lobby["z_heathenGameVersion"] ?? "";
                remoteModVersion = manager.Lobby["SephiriaTogether"] ?? "";
                remoteModMetadataAuthoritative = !string.IsNullOrEmpty(remoteModVersion);
            }
        }

        internal static void PrepareEosJoin(EOSLobbyInfo info)
        {
            if (info == null) return;
            ResetClientRoomState();
            remoteGameVersion = info.version ?? "";
            remoteRoomScope = "eos:" + info.lobbyId;
            if (!string.IsNullOrEmpty(remoteGameVersion) &&
                !string.Equals(remoteGameVersion, Application.version, StringComparison.OrdinalIgnoreCase))
                ShowMismatch(remoteGameVersion, "", remoteRoomScope);
        }

        internal static void PrepareIpJoin(string gameVersion, string modVersion, string address, ushort port,
            bool metadataAuthoritative = false)
        {
            ResetClientRoomState();
            remoteGameVersion = gameVersion ?? "";
            remoteModVersion = modVersion ?? "";
            remoteRoomScope = "ip:" + address + ":" + port;
            remoteModMetadataAuthoritative = metadataAuthoritative && !string.IsNullOrEmpty(remoteModVersion);
        }

        internal static void ClearClientTarget()
        {
            // Steam/IP room transitions stop the old client before starting the
            // new one. Keep the selected room metadata through that handoff.
            if (SteamInvitation.waitForExternalConnect && !string.IsNullOrEmpty(remoteRoomScope))
            {
                ResetClientHandshake();
                return;
            }
            ResetClientRoomState();
        }

        internal static void AbortClientJoin() => ResetClientRoomState();

        internal static void WarnLanRoom(string gameVersion, string modVersion, string address, ushort port)
        {
            PrepareIpJoin(gameVersion, modVersion, address, port, metadataAuthoritative: true);
            bool gameMismatch = !string.IsNullOrEmpty(gameVersion) &&
                                !string.Equals(gameVersion, Application.version, StringComparison.OrdinalIgnoreCase);
            bool modMismatch = !string.IsNullOrEmpty(modVersion) &&
                               !string.Equals(modVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase);
            if (gameMismatch || modMismatch)
                ShowMismatch(gameVersion, modVersion, "ip:" + address + ":" + port);
        }

        internal static bool HostSupportsProtocolMetadata()
        {
            if (NetworkServer.active) return true;
            return NetworkClient.active && ReferenceEquals(clientConnection, NetworkClient.connection) && clientProtocolConfirmed;
        }

        internal static bool IsProtocolCompatibleConnection(NetworkConnectionToClient connection)
        {
            return connection != null && (connection == NetworkServer.localConnection ||
                IsCurrentServerConnection(connection) && ProtocolCompatibleConnections.Contains(connection));
        }

        internal static bool IsCrossVersionConnection(NetworkConnectionToClient connection)
        {
            if (connection == null) return false;
            bool gameMismatch = RemoteGameVersions.TryGetValue(connection, out string gameVersion) &&
                                !string.IsNullOrEmpty(gameVersion) &&
                                !string.Equals(gameVersion, Application.version,
                                    StringComparison.OrdinalIgnoreCase);
            bool modMismatch = RemoteModVersions.TryGetValue(connection, out string modVersion) &&
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
            // Probe every remote session so a Modded client can detect an
            // unmodded or differently Modded host without making custom features
            // a prerequisite for joining. Feature messages remain gated until
            // the notice confirms an identical game and Mod protocol.
            return !NetworkServer.active;
        }

        internal static void InspectLobby(LobbyData lobby)
        {
            try
            {
                if (lobby.IsOwner || HasClientVersionReport) return;
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

        internal static string FormatVersionMismatch(string remoteGameVersion, string remoteModVersion)
        {
            if (!string.IsNullOrEmpty(remoteGameVersion) &&
                !string.Equals(remoteGameVersion, Application.version, StringComparison.OrdinalIgnoreCase))
            {
                return string.Format(MenuText.Get("GameVersionMismatchWarning"),
                    Application.version, remoteGameVersion);
            }
            if (string.IsNullOrEmpty(remoteModVersion) ||
                string.Equals(remoteModVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase))
                return MenuText.Get("VersionUnconfirmedWarning");
            return string.Format(MenuText.Get("ModVersionMismatchWarning"), Plugin.PluginVersion, remoteModVersion);
        }

        private static void ShowMismatch(string remoteGameVersion, string remoteModVersion, string scope = null)
        {
            string message = FormatVersionMismatch(remoteGameVersion, remoteModVersion);
            scope = string.IsNullOrEmpty(scope) ? CurrentClientRoomScope() : scope;
            bool survivesNoLobby = scope.StartsWith("ip:", StringComparison.Ordinal) ||
                                   scope.StartsWith("eos:", StringComparison.Ordinal) || scope.StartsWith("host-", StringComparison.Ordinal);
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

        internal static void Tick()
        {
            TickPendingConnections();
            if (!clientMessagesRegistered || !CanAttemptProtocolHandshake()) return;
            if (!RefreshClientConnection()) return;
            if (!NetworkClient.ready || !clientConnection.isAuthenticated) return;
            if (clientNoticeReceived)
            {
                if (clientProtocolConfirmed) CatchUpRewards.SendHello();
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (clientReadySince < 0f) clientReadySince = now;
            if (now >= nextHelloAt)
            {
                helloAttempts++;
                // Keep a slow probe for hosts whose Workshop runtime activates
                // late, without flooding sessions which do not run Together.
                nextHelloAt = now + (helloAttempts < 5 ? 3f : 30f);
                if (helloAttempts == 1)
                    Plugin.LogInfo($"Version handshake started: localGame={Application.version}, " +
                                   $"localMod={Plugin.PluginVersion}, scope={CurrentClientRoomScope()}.");
                NetworkClient.Send(new VersionCompatibilityHelloMessage
                {
                    gameVersion = Application.version,
                    modVersion = Plugin.PluginVersion
                });
            }
            if (!clientNoticeReceived && !clientPendingShown && now - clientReadySince >= PendingWarningDelay)
            {
                clientPendingShown = true;
                Plugin.LogInfo($"Version handshake awaiting host report: attempts={helloAttempts}, scope={CurrentClientRoomScope()}.");
                if (!HasReportedMismatch(remoteGameVersion, remoteModVersion))
                    ShowMismatch("", "", CurrentClientRoomScope());
            }
        }

        private static bool RefreshClientConnection()
        {
            NetworkConnectionToServer current = NetworkClient.active ? NetworkClient.connection : null;
            if (!ReferenceEquals(clientConnection, current))
            {
                ResetClientHandshake();
                clientMessagesRegistered = true;
                clientConnection = current;
            }
            return current != null;
        }

        private static void OnServerHello(NetworkConnectionToClient connection, VersionCompatibilityHelloMessage message)
        {
            if (!IsCurrentServerConnection(connection)) return;
            bool firstReport = HelloConnections.Add(connection);
            bool changed = !RemoteModVersions.TryGetValue(connection, out string previousMod) ||
                           previousMod != (message.modVersion ?? "");
            RemoteModVersions[connection] = message.modVersion ?? "";
            // Native authentication already enforces the exact game version.
            // The custom hello still carries the client's report for warnings
            // and for gating custom features on an identical Mod protocol.
            string remoteGameVersion = message.gameVersion ?? "";
            if (string.IsNullOrEmpty(remoteGameVersion) &&
                RemoteGameVersions.TryGetValue(connection, out string reported))
                remoteGameVersion = reported;
            changed |= !RemoteGameVersions.TryGetValue(connection, out string previousGame) || previousGame != remoteGameVersion;
            RemoteGameVersions[connection] = remoteGameVersion;
            bool gameMismatch = !string.Equals(remoteGameVersion, Application.version, StringComparison.OrdinalIgnoreCase);
            bool modMismatch = !string.Equals(message.modVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase);
            if (!gameMismatch && !modMismatch)
                ProtocolCompatibleConnections.Add(connection);
            else ProtocolCompatibleConnections.Remove(connection);
            PendingConnections.Remove(connection);
            if (firstReport || changed)
            {
                Plugin.LogInfo($"Version handshake received: conn={connection.connectionId}, " +
                               $"remoteGame={remoteGameVersion}, remoteMod={message.modVersion}, " +
                               $"protocolCompatible={!gameMismatch && !modMismatch}.");
                VersionReminder.Dismiss(ServerWarningScope(connection));
                if (gameMismatch || modMismatch) ShowMismatch(remoteGameVersion, message.modVersion, ServerWarningScope(connection));
            }
            connection.Send(new VersionCompatibilityNoticeMessage
            {
                gameVersion = Application.version,
                modVersion = Plugin.PluginVersion
            });
        }

        private static string ServerWarningScope(NetworkConnectionToClient connection) => "host-peer:" + connection.connectionId;

        private static bool IsCurrentServerConnection(NetworkConnectionToClient connection) =>
            NetworkServer.active && connection != null &&
            NetworkServer.connections.TryGetValue(connection.connectionId, out NetworkConnectionToClient current) &&
            ReferenceEquals(current, connection);

        private static void TickPendingConnections()
        {
            PendingSnapshot.Clear();
            PendingSnapshot.AddRange(PendingConnections.Keys);
            foreach (NetworkConnectionToClient connection in PendingSnapshot)
            {
                if (!IsCurrentServerConnection(connection)) { RemoveConnection(connection); continue; }
                if (!connection.isAuthenticated || !connection.isReady) continue;
                float since = PendingConnections[connection];
                if (since < 0f) PendingConnections[connection] = Time.realtimeSinceStartup;
                else if (!float.IsPositiveInfinity(since) && Time.realtimeSinceStartup - since >= PendingWarningDelay)
                {
                    PendingConnections[connection] = float.PositiveInfinity;
                    Plugin.LogInfo($"Version handshake awaiting client report: conn={connection.connectionId}.");
                    ShowMismatch("", "", ServerWarningScope(connection));
                }
            }
            PendingSnapshot.Clear();
        }

        private static void OnClientNotice(VersionCompatibilityNoticeMessage message)
        {
            if (!clientMessagesRegistered || NetworkServer.active || !RefreshClientConnection()) return;
            remoteGameVersion = message.gameVersion ?? "";
            remoteModVersion = message.modVersion ?? "";
            clientNoticeReceived = !string.IsNullOrEmpty(remoteGameVersion) && !string.IsNullOrEmpty(remoteModVersion);
            bool gameMismatch = !string.Equals(message.gameVersion, Application.version,
                                    StringComparison.OrdinalIgnoreCase);
            bool modMismatch = !string.Equals(message.modVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase);
            clientProtocolConfirmed = !gameMismatch && !modMismatch;
            Plugin.LogInfo($"Version notice received: remoteGame={message.gameVersion}, " +
                            $"remoteMod={message.modVersion}, protocolCompatible={clientProtocolConfirmed}.");
            VersionReminder.Dismiss(CurrentClientRoomScope());
            if (gameMismatch || modMismatch) ShowMismatch(message.gameVersion, message.modVersion);
        }

        private static bool HasReportedMismatch(string gameVersion, string modVersion) =>
            !string.IsNullOrEmpty(gameVersion) && !string.Equals(gameVersion, Application.version, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(modVersion) && !string.Equals(modVersion, Plugin.PluginVersion, StringComparison.OrdinalIgnoreCase);

        private static void ShowPendingWarning()
        {
            if (HasClientVersionReport) return;
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
            remoteGameVersion = "";
            remoteModVersion = "";
            remoteRoomScope = "";
            ResetClientHandshake();
            remoteModMetadataAuthoritative = false;
            VersionReminder.Clear();
        }

        private static void ResetClientHandshake()
        {
            clientConnection = null;
            clientMessagesRegistered = false;
            clientNoticeReceived = false;
            clientProtocolConfirmed = false;
            clientReadySince = -1f;
            nextHelloAt = 0f;
            helloAttempts = 0;
            clientPendingShown = false;
        }
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

    [HarmonyPatch(typeof(UI_MultiplayerPanel_E), "OnJoinButton", new[] { typeof(EOSLobbyInfo) })]
    [HarmonyPriority(Priority.First)]
    internal static class EosLobbyJoinVersionCompatibilityPatch
    {
        private static void Prefix(EOSLobbyInfo info) => VersionCompatibility.PrepareEosJoin(info);
    }

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), "OnServerVersionMessage")]
    [HarmonyPriority(Priority.First)]
    internal static class ServerVersionCompatibilityPatch
    {
        private static void Prefix(NetworkConnectionToClient conn,
            HorayNetworkAuthenticator.VersionMessage message)
        {
            // Record only. The stock authenticator now enforces the exact game
            // version because updated builds change native network serialization
            // and command hashes; spoofing the version would desync the session.
            VersionCompatibility.RecordGameVersion(conn, message.version);
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
