using System;
using System.Collections;
using HarmonyLib;
using Mirror;
using Steamworks;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class IpTransport
    {
        private static bool active;
        private static bool offlineEnvironment;

        internal static bool IsActive => active && NetworkManager.singleton != null &&
                                         NetworkManager.singleton.transport is TelepathyTransport;
        internal static bool IsOfflineEnvironment => offlineEnvironment;
        internal static ushort ConfiguredPort => Plugin.directPort != null
            ? (ushort)Mathf.Clamp(Plugin.directPort.Value, 1, 65535)
            : (ushort)7777;

        internal static void Install(HorayNetworkManager manager)
        {
            if (manager == null) return;
            offlineEnvironment = !IsSteamLoggedOn();
            if (!offlineEnvironment && (Plugin.directModeEnabled == null || !Plugin.directModeEnabled.Value))
            {
                active = false;
                return;
            }

            Transport previous = manager.transport;
            TelepathyTransport transport = manager.GetComponent<TelepathyTransport>();
            if (transport == null) transport = manager.gameObject.AddComponent<TelepathyTransport>();
            transport.port = ConfiguredPort;
            transport.enabled = true;
            if (previous != null && previous != transport) previous.enabled = false;
            manager.transport = transport;
            Transport.active = transport;
            active = true;
            Plugin.LogInfo($"IP transport installed at startup: port={transport.port}, offline={offlineEnvironment}.");
        }

        internal static bool PrepareTitleJoin(UI_TitleLobby title)
        {
            if (!IsActive) return false;
            string value = title?.ipField != null ? title.ipField.text : "";
            if (!TryParseAddress(value, out string host, out ushort port))
            {
                ShowMessage(MenuText.Get("IpInvalidAddress"));
                return true;
            }
            if (NetworkManager.singleton.transport is TelepathyTransport telepathy) telepathy.port = port;
            title.ipField.text = host;
            return false;
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
            if (!IsActive || SteamInvitation.waitForExternalConnect) yield break;
            SteamInvitation.waitForExternalConnect = true;
            NetworkManager manager = NetworkManager.singleton;
            if (manager == null)
            {
                SteamInvitation.waitForExternalConnect = false;
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
                yield break;
            }
            telepathy.port = port;
            Transport.active = telepathy;
            manager.networkAddress = address;
            (manager as HorayNetworkManager)?.ShowConnectingScreen();
            manager.StartClient();
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

        private static void ShowMessage(string message)
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
            if (!IpTransport.IsActive) return true;
            IpTransport.PromptJoinFromPanel();
            return false;
        }
    }

}
