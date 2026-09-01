using System;
using System.Collections;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Mirror;
using Steamworks;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class IpTransport
    {
        private static bool active;
        private static bool offlineEnvironment;
        private static bool environmentKnown;
        private static Transport previousTransport;
        private static bool installLogged;
        private static bool activeTransportWarningLogged;

        internal static bool IsActive => active && NetworkManager.singleton != null &&
                                         NetworkManager.singleton.transport is TelepathyTransport;
        internal static bool IsOfflineEnvironment => offlineEnvironment;
        internal static bool ShouldUseIp => environmentKnown && offlineEnvironment ||
                                            (Plugin.directModeEnabled != null && Plugin.directModeEnabled.Value);
        // A setting change is only allowed to select IP before a network session starts.
        // Once a session is active, the installed transport is the source of truth.
        internal static bool ShouldUseIpForUi => IsActive ||
                                                 (ShouldUseIp && !NetworkServer.active && !NetworkClient.active);
        internal static bool CanChangeSettings =>
            !NetworkServer.active && !NetworkClient.active &&
            (DungeonManager.Instance == null || !DungeonManager.Instance.isRunStarted);
        internal static ushort ConfiguredPort => Plugin.directPort != null
            ? (ushort)Mathf.Clamp(Plugin.directPort.Value, 1, 65535)
            : (ushort)7777;
        internal static ushort ActivePort
        {
            get
            {
                TelepathyTransport transport = NetworkManager.singleton?.transport as TelepathyTransport;
                return transport != null ? transport.port : ConfiguredPort;
            }
        }

        internal static void EnsureInstalled()
        {
            if (IsActive)
            {
                if (CanChangeSettings && NetworkManager.singleton.transport is TelepathyTransport)
                {
                    NetworkManager.singleton.maxConnections = PlayerLimit.CurrentLimit;
                }
                return;
            }
            if (NetworkManager.singleton is HorayNetworkManager manager)
            {
                if (!environmentKnown || ShouldUseIp) Install(manager);
            }
        }

        internal static void ApplySettingsFromMenu()
        {
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null || !CanChangeSettings) return;
            offlineEnvironment = !IsSteamLoggedOn();
            environmentKnown = true;
            if (!ShouldUseIp)
            {
                Deactivate(manager);
                return;
            }
            Activate(manager);
            manager.maxConnections = PlayerLimit.CurrentLimit;
            ApplyOpenPanelMode();
            Plugin.LogInfo($"IP transport applied from menu: port={ConfiguredPort}, offline={offlineEnvironment}.");
        }

        internal static void ApplyLobbyUiMode(UI_MultiplayerPanel panel)
        {
            if (panel == null || !ShouldUseIpForUi) return;
            EnsureInstalled();
            if (!IsActive) return;
            LeaveSteamLobbyIfPresent();
            if (panel.searchLobbyGroup != null) panel.searchLobbyGroup.SetActive(true);
            if (panel.createLobbyGroup != null && !IpLobby.IsCreated && !IpLobby.IsJoined)
                panel.createLobbyGroup.SetActive(false);
            if (panel.enteredLobbyGroup != null && !IpLobby.IsCreated && !IpLobby.IsJoined)
                panel.enteredLobbyGroup.SetActive(false);
            if (panel.rejoinGroup != null) panel.rejoinGroup.SetActive(false);
            LanRoomListUi.ActivateIpMode(panel);
            IpLobby.ApplyPanelState(panel);
            if (panel.searchLobbyGroup != null && panel.searchLobbyGroup.activeSelf)
                LanRoomDiscovery.Refresh();
        }

        internal static void Install(HorayNetworkManager manager)
        {
            if (manager == null) return;
            offlineEnvironment = !IsSteamLoggedOn();
            environmentKnown = true;
            bool networkActive = NetworkServer.active || NetworkClient.active;
            if (networkActive)
            {
                if (manager.transport is TelepathyTransport)
                {
                    active = true;
                    return;
                }
                if (!activeTransportWarningLogged)
                {
                    activeTransportWarningLogged = true;
                    Plugin.LogInfo("IP transport cannot replace the active network transport until the next launch.");
                }
                return;
            }
            activeTransportWarningLogged = false;
            if (!ShouldUseIp)
            {
                active = false;
                return;
            }
            Activate(manager);
            ApplyOpenPanelMode();
            if (!installLogged)
            {
                installLogged = true;
                Plugin.LogInfo($"IP transport installed at startup: port={ConfiguredPort}, offline={offlineEnvironment}.");
            }
        }

        private static void Activate(NetworkManager manager)
        {
            TelepathyTransport transport = manager.GetComponent<TelepathyTransport>();
            if (transport == null) transport = manager.gameObject.AddComponent<TelepathyTransport>();
            if (!(manager.transport is TelepathyTransport) && manager.transport != null)
                previousTransport = manager.transport;
            transport.port = ConfiguredPort;
            transport.enabled = true;
            if (previousTransport != null && previousTransport != transport) previousTransport.enabled = false;
            manager.transport = transport;
            Transport.active = transport;
            active = true;
            activeTransportWarningLogged = false;
        }

        private static void Deactivate(NetworkManager manager)
        {
            active = false;
            TelepathyTransport transport = manager.GetComponent<TelepathyTransport>();
            if (transport != null) transport.enabled = false;
            if (previousTransport != null)
            {
                previousTransport.enabled = true;
                manager.transport = previousTransport;
                Transport.active = previousTransport;
            }
            UI_MultiplayerPanel panel = UIManager.Instance?.GetElement<UI_MultiplayerPanel>();
            if (panel != null)
            {
                LanRoomListUi.DeactivateIpMode(panel);
                if (panel.searchLobbyGroup != null) panel.searchLobbyGroup.SetActive(true);
                if (panel.createLobbyGroup != null) panel.createLobbyGroup.SetActive(false);
                if (panel.enteredLobbyGroup != null) panel.enteredLobbyGroup.SetActive(false);
                if (panel.IsOpened) panel.RefreshLobbyList();
            }
        }

        private static void ApplyOpenPanelMode()
        {
            UI_MultiplayerPanel panel = UIManager.Instance?.GetElement<UI_MultiplayerPanel>();
            if (panel == null) return;
            if (IsActive) ApplyLobbyUiMode(panel);
        }

        private static void LeaveSteamLobbyIfPresent()
        {
            try
            {
                GameObject steamManager = SingletonObject.Find("SteamManager");
                if (IsActive && steamManager != null && App.Initialized &&
                    steamManager.TryGetComponent(out LobbyManager lobby) && lobby.HasLobby)
                {
                    lobby.Leave();
                    Plugin.LogInfo("Left the Steam lobby because IP transport is active.");
                }
            }
            catch (Exception exception)
            {
                Plugin.LogInfo("Unable to clear the Steam lobby in IP mode: " + exception.Message);
            }
        }

        internal static bool PrepareTitleJoin(UI_TitleLobby title)
        {
            EnsureInstalled();
            if (!ShouldUseIp) return false;
            if (!IsActive)
            {
                ShowMessage(MenuText.Get("IpRestartRequired"));
                return true;
            }
            string value = title?.ipField != null ? title.ipField.text : "";
            if (!TryParseAddress(value, out string host, out ushort port))
            {
                ShowMessage(MenuText.Get("IpInvalidAddress"));
                return true;
            }
            if (NetworkManager.singleton.transport is TelepathyTransport telepathy) telepathy.port = port;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) host = "127.0.0.1";
            if (!VersionCompatibility.IsPreparedForIp(host, port))
                VersionCompatibility.PrepareIpJoin("", "", host, port);
            title.ipField.text = host;
            return false;
        }

        internal static bool PrepareForNetworkStart(NetworkManager manager)
        {
            EnsureInstalled();
            if (!ShouldUseIp)
            {
                return true;
            }
            if (!IsActive || manager == null || !(manager.transport is TelepathyTransport telepathy))
            {
                ShowMessage(MenuText.Get("IpRestartRequired"));
                return false;
            }
            // A manual IP join may temporarily use a different client port. Hosts always
            // bind the configured server port when a new session starts.
            telepathy.port = ConfiguredPort;
            manager.maxConnections = PlayerLimit.CurrentLimit;
            Transport.active = telepathy;
            return true;
        }

        internal static bool PrepareForClientStart(NetworkManager manager)
        {
            if (NetworkClient.active) return true;
            EnsureInstalled();
            if (ShouldUseIp && !IsActive)
            {
                ShowMessage(MenuText.Get("IpRestartRequired"));
                return false;
            }
            return true;
        }

        internal static void PromptJoinFromPanel()
        {
            UI_MessageBox_InputYesNo dialog = UIManager.Instance?.GetElement<UI_MessageBox_InputYesNo>();
            if (dialog == null) return;
            dialog.Open(MenuText.Get("IpJoinPrompt"), JoinFromPanelText, null, "", "IP or IP:Port", true, 64);
        }

        internal static void JoinRoom(string address, ushort port)
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            if (Plugin.InstanceForPatches != null)
                Plugin.InstanceForPatches.StartCoroutine(JoinRoomCoroutine(address.Trim(), port));
        }

        private static void JoinFromPanelText(string value)
        {
            if (!TryParseAddress(value, out string host, out ushort port))
            {
                ShowMessage(MenuText.Get("IpInvalidAddress"));
                return;
            }
            JoinRoom(host, port);
        }

        private static IEnumerator JoinRoomCoroutine(string address, ushort port)
        {
            if (!ShouldUseIp || SteamInvitation.waitForExternalConnect) yield break;
            EnsureInstalled();
            if (!IsActive)
            {
                VersionCompatibility.AbortClientJoin();
                ShowMessage(MenuText.Get("IpRestartRequired"));
                yield break;
            }
            SteamInvitation.waitForExternalConnect = true;
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null)
            {
                SteamInvitation.waitForExternalConnect = false;
                VersionCompatibility.AbortClientJoin();
                yield break;
            }
            HorayNetworkManager horay = manager as HorayNetworkManager;
            if (horay != null) horay.requestSelfLeave = true;
            if (NetworkServer.active) manager.StopHost();
            else if (NetworkClient.active) manager.StopClient();
            while (ScreenFader.Instance != null && ScreenFader.Instance.IsFading) yield return null;
            yield return new WaitForSeconds(1.5f);
            string profile = OptionsBinding.Instance != null && OptionsBinding.Instance.Options != null
                ? OptionsBinding.Instance.Options.GetString("SelectedProfile", SaveManager.defaultSlotName)
                : SaveManager.defaultSlotName;
            if (!SaveManager.Load(profile))
            {
                SteamInvitation.waitForExternalConnect = false;
                VersionCompatibility.AbortClientJoin();
                IpLobby.ResetClientPanel();
                ShowMessage(MenuText.Get("IpProfileLoadFailed"));
                yield break;
            }
            SaveManager.CreateNewTMP(profile);
            SaveManager.ApplyPostLoadSaveFixes();
            yield return new WaitForSeconds(0.2f);
            manager = NetworkManager.singleton;
            if (manager == null || !(manager.transport is TelepathyTransport telepathy))
            {
                SteamInvitation.waitForExternalConnect = false;
                VersionCompatibility.AbortClientJoin();
                IpLobby.ResetClientPanel();
                yield break;
            }
            telepathy.port = port;
            Transport.active = telepathy;
            if (!VersionCompatibility.IsPreparedForIp(address, port))
                VersionCompatibility.PrepareIpJoin("", "", address, port);
            manager.networkAddress = address;
            (manager as HorayNetworkManager)?.ShowConnectingScreen();
            manager.StartClient();
            IpLobby.MarkJoined(address, port);
            yield return new WaitForSeconds(2f);
            SteamInvitation.waitForExternalConnect = false;
            Plugin.LogInfo($"IP join requested: {address}:{port}.");
        }

        private static bool TryParseAddress(string value, out string host, out ushort port)
        {
            host = (value ?? "").Trim();
            port = ConfiguredPort;
            if (host.Length == 0) return false;
            int colon = host.LastIndexOf(':');
            if (colon > 0 && colon < host.Length - 1 &&
                ushort.TryParse(host.Substring(colon + 1), out ushort parsed) && parsed != 0)
            {
                port = parsed;
                host = host.Substring(0, colon).Trim();
            }
            return host.Length > 0;
        }

        private static bool IsSteamLoggedOn()
        {
            try
            {
                return SteamUser.BLoggedOn();
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static void ShowMessage(string message)
        {
            UIManager.Instance?.GetElement<UI_SystemMessage>()?.Open(message, 4f);
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), nameof(HorayNetworkManager.Awake))]
    internal static class IpTransportInstallPatch
    {
        private static void Postfix(HorayNetworkManager __instance) => IpTransport.Install(__instance);
    }

    [HarmonyPatch(typeof(UI_TitleLobby), nameof(UI_TitleLobby.Join))]
    internal static class IpTitleJoinPatch
    {
        private static bool Prefix(UI_TitleLobby __instance) => !IpTransport.PrepareTitleJoin(__instance);
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnJoinButton), new Type[0])]
    internal static class IpPanelJoinPatch
    {
        private static bool Prefix()
        {
            IpTransport.EnsureInstalled();
            if (!IpTransport.ShouldUseIpForUi) return true;
            if (!IpTransport.IsActive)
            {
                IpTransport.ShowMessage(MenuText.Get("IpRestartRequired"));
                return false;
            }
            IpTransport.PromptJoinFromPanel();
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnOpened))]
    [HarmonyPriority(Priority.First)]
    internal static class IpPanelEnsureTransportPatch
    {
        private static void Prefix() => IpTransport.EnsureInstalled();
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnOpened))]
    internal static class IpPanelModePatch
    {
        private static void Postfix(UI_MultiplayerPanel __instance) => IpTransport.ApplyLobbyUiMode(__instance);
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.OnCreateButton))]
    internal static class IpPanelCreateEnsurePatch
    {
        private static void Prefix() => IpTransport.EnsureInstalled();
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), nameof(UI_MultiplayerPanel.RefreshLobbyList))]
    internal static class IpPanelRefreshPatch
    {
        private static bool Prefix()
        {
            IpTransport.EnsureInstalled();
            if (!IpTransport.ShouldUseIpForUi) return true;
            if (!IpTransport.IsActive)
            {
                IpTransport.ShowMessage(MenuText.Get("IpRestartRequired"));
            }
            else
            {
                LanRoomDiscovery.Refresh();
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), "HandleFound")]
    internal static class IpSteamLobbyFoundPatch
    {
        private static bool Prefix() => !IpTransport.ShouldUseIpForUi;
    }

    [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.Search), new[] { typeof(int) })]
    internal static class IpSteamSearchPatch
    {
        private static bool Prefix()
        {
            return !IpTransport.ShouldUseIpForUi;
        }
    }

    [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.Create), new Type[0])]
    internal static class IpSteamCreatePatch
    {
        private static bool Prefix()
        {
            return !IpTransport.ShouldUseIpForUi;
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartHost))]
    internal static class IpPrepareHostPatch
    {
        private static bool Prefix(NetworkManager __instance) => IpTransport.PrepareForNetworkStart(__instance);
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartServer))]
    internal static class IpPrepareServerPatch
    {
        private static bool Prefix(NetworkManager __instance) => IpTransport.PrepareForNetworkStart(__instance);
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartClient), new Type[0])]
    internal static class IpPrepareClientPatch
    {
        private static bool Prefix(NetworkManager __instance) => IpTransport.PrepareForClientStart(__instance);
    }

}
