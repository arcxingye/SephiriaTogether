using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Mirror;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SephiriaTogether
{
    internal static class IpTransport
    {
        private static bool active;
        private static bool offlineEnvironment;
        private static bool environmentKnown;
        private static Transport previousTransport;
        private static NetworkManager trackedManager;
        private static ushort? clientTargetPort;
        private static bool applyingSettings;
        private static bool openSettingsOnTitle;
        private static float nextEnvironmentCheck;
        private static bool activeTransportWarningLogged;
        private static string applyError;

        internal static bool IsActive => active && NetworkManager.singleton != null &&
                                         NetworkManager.singleton.transport is TelepathyTransport;
        internal static bool IsOfflineEnvironment => offlineEnvironment;
        internal static bool ShouldUseIp
        {
            get
            {
                if (!environmentKnown) return false;
                return offlineEnvironment
                    ? Plugin.offlineDirectModeEnabled != null && Plugin.offlineDirectModeEnabled.Value
                    : Plugin.directModeEnabled != null && Plugin.directModeEnabled.Value;
            }
        }
        // Preferences can be edited during a session; only the installed transport
        // controls the current UI. Changes are applied after the session ends.
        internal static bool ShouldUseIpForUi => IsActive ||
                                                 (ShouldUseIp && !NetworkServer.active && !NetworkClient.active);
        internal static bool CanApplySettings => SessionTransportPolicy.CanApply(NetworkServer.active,
            NetworkClient.active, SteamInvitation.waitForExternalConnect,
            DungeonManager.Instance != null && DungeonManager.Instance.isRunStarted);
        internal static bool CanEditSettings => !SteamInvitation.waitForExternalConnect && !SaveManagement.IsBusy;
        internal static string ApplyError => applyError;
        internal static bool HasPendingSettings
        {
            get
            {
                NetworkManager manager = NetworkManager.singleton;
                if (manager == null || !environmentKnown) return ShouldUseIp;
                return DecideChange(manager, CanApplySettings, DesiredPortForCurrentState) != SessionTransportChange.None;
            }
        }
        // A client's remote port is not its saved port for future hosting.
        private static ushort DesiredPortForCurrentState => SessionTransportPolicy.ComparisonPort(
            NetworkServer.active, NetworkClient.active, ConfiguredPort, ActivePort);
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
            if (applyingSettings) return;
            if (NetworkManager.singleton is HorayNetworkManager manager)
            {
                Install(manager);
                if (openSettingsOnTitle && CanApplySettings && UIManager.Instance != null &&
                    SceneManager.GetActiveScene().name == manager.titleSceneName)
                {
                    openSettingsOnTitle = false;
                    CoopMenu.OpenNetworkSettings();
                }
            }
        }

        internal static void ApplySettingsFromMenu()
        {
            if (!environmentKnown) RefreshEnvironment(force: true);
            EnsureInstalled();
            Plugin.LogInfo($"Network preferences saved: requestedIp={ShouldUseIp}, hostPort={ConfiguredPort}, " +
                           $"currentIp={IsActive}, pending={HasPendingSettings}.");
        }

        internal static void ReturnToTitleForSettings()
        {
            if (!CanEditSettings) return;
            Plugin.SaveSettings();
            if (CanApplySettings)
            {
                ApplySettingsFromMenu();
                return;
            }
            UI_PausePanel pause = UIManager.Instance?.GetElement<UI_PausePanel>();
            if (pause == null)
            {
                ShowMessage(MenuText.Get("IpReturnUnavailable"));
                return;
            }
            // Keep the original confirmation and client dungeon-settlement flow.
            // No forced save, transport swap, or process restart while connected.
            openSettingsOnTitle = true;
            CoopMenu.Close();
            pause.GoToTitle();
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
            if (manager == null || applyingSettings) return;
            ObserveManager(manager);
            RefreshEnvironment();
            Reconcile(manager, CanApplySettings, DesiredPortForCurrentState);
        }

        private static void ObserveManager(NetworkManager manager)
        {
            if (trackedManager != manager)
            {
                trackedManager = manager;
                previousTransport = null;
                activeTransportWarningLogged = false;
            }
            // Remember the original on late Workshop activation too, without
            // mutating an active session. Never reuse another manager's transport.
            if (manager.transport != null && !(manager.transport is TelepathyTransport))
                previousTransport = manager.transport;
            active = manager.transport is TelepathyTransport;
        }

        private static void RefreshEnvironment(bool force = false)
        {
            if (!force && environmentKnown &&
                (NetworkServer.active || NetworkClient.active || Time.realtimeSinceStartup < nextEnvironmentCheck)) return;
            offlineEnvironment = !IsSteamLoggedOn();
            environmentKnown = true;
            nextEnvironmentCheck = Time.realtimeSinceStartup + 1f;
        }

        private static SessionTransportChange DecideChange(NetworkManager manager, bool canApply, ushort port)
        {
            TelepathyTransport current = manager.transport as TelepathyTransport;
            return SessionTransportPolicy.Decide(ShouldUseIp, port, current != null,
                current != null ? current.port : (ushort)0, canApply, previousTransport != null);
        }

        private static bool Reconcile(NetworkManager manager, bool canApply, ushort port)
        {
            SessionTransportChange change = DecideChange(manager, canApply, port);
            if (change == SessionTransportChange.None)
            {
                applyError = null;
                activeTransportWarningLogged = false;
                return true;
            }
            if (change == SessionTransportChange.WaitForSessionEnd)
            {
                applyError = null;
                if (!activeTransportWarningLogged)
                {
                    activeTransportWarningLogged = true;
                    Plugin.LogInfo("Network preferences are pending. Return to the title after ending this session to apply them; no process restart is needed.");
                }
                return false;
            }
            if (change == SessionTransportChange.NativeTransportUnavailable)
            {
                applyError = MenuText.Get("IpNativeUnavailable");
                return false;
            }
            applyingSettings = true;
            try
            {
                if (change == SessionTransportChange.RestoreNative)
                {
                    if (!Deactivate(manager)) return false;
                }
                else
                {
                    Activate(manager, port);
                    ApplyOpenPanelMode();
                }
                applyError = null;
                activeTransportWarningLogged = false;
                manager.maxConnections = PlayerLimit.CurrentLimit;
                Plugin.LogInfo($"Network preferences applied while idle: ip={IsActive}, port={ActivePort}, offline={offlineEnvironment}.");
                return true;
            }
            finally { applyingSettings = false; }
        }

        private static void Activate(NetworkManager manager, ushort port)
        {
            TelepathyTransport transport = manager.GetComponent<TelepathyTransport>();
            if (transport == null) transport = manager.gameObject.AddComponent<TelepathyTransport>();
            if (!(manager.transport is TelepathyTransport) && manager.transport != null)
                previousTransport = manager.transport;
            transport.port = port;
            transport.enabled = true;
            if (previousTransport != null && previousTransport != transport) previousTransport.enabled = false;
            manager.transport = transport;
            Transport.active = transport;
            active = true;
            activeTransportWarningLogged = false;
        }

        private static bool Deactivate(NetworkManager manager)
        {
            if (previousTransport == null)
            {
                applyError = MenuText.Get("IpNativeUnavailable");
                return false;
            }
            active = false;
            TelepathyTransport transport = manager.GetComponent<TelepathyTransport>();
            if (transport != null) transport.enabled = false;
            previousTransport.enabled = true;
            manager.transport = previousTransport;
            Transport.active = previousTransport;
            UI_MultiplayerPanel panel = UIManager.Instance?.GetElement<UI_MultiplayerPanel>();
            if (panel != null)
            {
                LanRoomListUi.DeactivateIpMode(panel);
                if (panel.searchLobbyGroup != null) panel.searchLobbyGroup.SetActive(true);
                if (panel.createLobbyGroup != null) panel.createLobbyGroup.SetActive(false);
                if (panel.enteredLobbyGroup != null) panel.enteredLobbyGroup.SetActive(false);
                if (panel.IsOpened) panel.RefreshLobbyList();
            }
            return true;
        }

        internal static void Shutdown()
        {
            openSettingsOnTitle = false;
            clientTargetPort = null;
            // An active session owns its transport until it stops; do not tear
            // its socket out from under Mirror when the native mod unloads.
            if (CanApplySettings && NetworkManager.singleton != null && active)
                Deactivate(NetworkManager.singleton);
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
            if (WorkshopLobbyModsUI.BlockLocalModJoining()) return true;
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
            clientTargetPort = port;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) host = "127.0.0.1";
            if (!VersionCompatibility.IsPreparedForIp(host, port))
                VersionCompatibility.PrepareIpJoin("", "", host, port);
            title.ipField.text = host;
            return false;
        }

        internal static bool PrepareForNetworkStart(NetworkManager manager)
        {
            if (manager == null) return false;
            if (NetworkServer.active || NetworkClient.active) return true;
            ObserveManager(manager);
            RefreshEnvironment(force: true);
            clientTargetPort = null;
            bool canApply = SessionTransportPolicy.CanApply(NetworkServer.active, NetworkClient.active,
                SteamInvitation.waitForExternalConnect, false, startingSession: true);
            if (!Reconcile(manager, canApply, ConfiguredPort))
            {
                ShowMessage(applyError ?? MenuText.Get("IpRestartRequired"));
                return false;
            }
            // A native host may start while the native scene transition flag is
            // set. This explicit boundary is safe because both network sides are idle.
            manager.maxConnections = PlayerLimit.CurrentLimit;
            Transport.active = manager.transport;
            return true;
        }

        internal static bool PrepareForClientStart(NetworkManager manager)
        {
            if (manager == null) return false;
            if (NetworkClient.active || NetworkServer.active) return true;
            ObserveManager(manager);
            RefreshEnvironment(force: true);
            if (!SessionTransportPolicy.ClientModeMatchesTarget(ShouldUseIp, clientTargetPort.HasValue))
            {
                // Steam/EOS join addresses are not TCP endpoints. A queued IP
                // preference must never reinterpret a native lobby's address.
                bool needsIpTarget = ShouldUseIp;
                clientTargetPort = null;
                ShowMessage(MenuText.Get(needsIpTarget ? "IpExplicitTargetRequired" : "IpRestartRequired"));
                return false;
            }
            bool canApply = SessionTransportPolicy.CanApply(NetworkServer.active, NetworkClient.active,
                SteamInvitation.waitForExternalConnect, false, startingSession: true);
            if (Reconcile(manager, canApply, SessionTransportPolicy.ClientPort(ConfiguredPort, clientTargetPort)))
            {
                Transport.active = manager.transport;
                return true;
            }
            ShowMessage(applyError ?? MenuText.Get("IpRestartRequired"));
            return false;
        }

        internal static void ClearClientPort() => clientTargetPort = null;

        internal static void PromptJoinFromPanel()
        {
            if (WorkshopLobbyModsUI.BlockLocalModJoining()) return;
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
            if (SteamInvitation.waitForExternalConnect) yield break;
            if (!ShouldUseIp)
            {
                ShowMessage(MenuText.Get("IpLockedInSession"));
                yield break;
            }
            if (WorkshopLobbyModsUI.BlockLocalModJoining()) yield break;
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
            // Match the new native join lifecycle before creating client objects.
            yield return Traverse.Create(typeof(WorkshopModManager))
                .Method("PrepareForActivation").GetValue<IEnumerator>();
            manager = NetworkManager.singleton;
            if (manager == null || !(manager.transport is TelepathyTransport) || NetworkServer.active || NetworkClient.active)
            {
                SteamInvitation.waitForExternalConnect = false;
                VersionCompatibility.AbortClientJoin();
                IpLobby.ResetClientPanel();
                yield break;
            }
            clientTargetPort = port;
            if (!VersionCompatibility.IsPreparedForIp(address, port))
                VersionCompatibility.PrepareIpJoin("", "", address, port);
            manager.networkAddress = address;
            (manager as HorayNetworkManager)?.ShowConnectingScreen();
            manager.StartClient();
            if (!NetworkClient.active)
            {
                clientTargetPort = null;
                SteamInvitation.waitForExternalConnect = false;
                VersionCompatibility.AbortClientJoin();
                IpLobby.ResetClientPanel();
                yield break;
            }
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

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo ready = AccessTools.Method(typeof(UI_MultiplayerPanel), "IsSteamLobbyReady");
            MethodInfo includeIp = AccessTools.Method(typeof(IpPanelCreateEnsurePatch), nameof(CanCreateRoom));
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(ready))
                {
                    yield return new CodeInstruction(OpCodes.Call, includeIp);
                    matches++;
                }
            }
            if (matches != 1)
                throw new InvalidOperationException("Lobby creation readiness check changed.");
        }

        private static bool CanCreateRoom(bool steamReady) => steamReady || IpTransport.IsActive;
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), "ApplySteamDependentButtons")]
    internal static class IpPanelButtonsPatch
    {
        private static void Postfix(UI_MultiplayerPanel __instance)
        {
            if (!IpTransport.IsActive) return;
            if (__instance.joinGameButton != null) __instance.joinGameButton.interactable = true;
            if (__instance.createGameButton != null) __instance.createGameButton.interactable = true;
            if (__instance.steamNotInitializedNotice != null) __instance.steamNotInitializedNotice.SetActive(false);
        }
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
