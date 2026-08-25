using System;
using System.Reflection;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using HeathenEngineering.SteamworksIntegration.API;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class JoinProgressBypass
    {
        private static readonly FieldInfo MoveMultiZoneField =
            AccessTools.Field(typeof(UI_MultiplayerPanel), "moveMultiZone");

        internal static bool CanCreateLobbyForCurrentRun()
        {
            if (!NetworkServer.active || DungeonManager.Instance == null || !DungeonManager.Instance.isRunStarted)
                return false;
            GameObject steamManager = SingletonObject.Find("SteamManager");
            return steamManager != null && App.Initialized &&
                   steamManager.TryGetComponent(out LobbyManager manager) && !manager.HasLobby;
        }

        internal static void OpenLobbyCreationForCurrentRun()
        {
            if (!CanCreateLobbyForCurrentRun()) return;
            HorayNetworkAuthenticator.allowConnection = true;
            UI_MultiplayerPanel panel = UIManager.Instance?.GetElement<UI_MultiplayerPanel>();
            if (panel == null)
            {
                Plugin.LogInfo("Unable to open the vanilla multiplayer panel for the current run.");
                return;
            }
            CoopMenu.Close();
            panel.Open();
            panel.OnCreateButton();
            Plugin.LogInfo("Opened vanilla lobby creation for the resumed dungeon run.");
        }

        internal static void KeepCurrentFloorWhenCreatingLobby(UI_MultiplayerPanel panel)
        {
            if (panel == null || DungeonManager.Instance == null || !DungeonManager.Instance.isRunStarted) return;
            MoveMultiZoneField?.SetValue(panel, false);
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), "OnJoinButton", new[] { typeof(LobbyData) })]
    internal static class SteamJoinProgressPatch
    {
        private static bool Prefix(UI_MultiplayerPanel __instance, LobbyData lobby)
        {
            if (!Plugin.allowLowerProgressPlayers.Value)
            {
                return true;
            }

            // The original method also performs version, race and chapter-data validation.
            // Re-run only the non-progress checks, then join directly.
            RaceEntity race = DungeonManager.Instance != null ? DungeonManager.Instance.Race : null;
            if (race != null && race.isMultiplayerBlocked)
            {
                return true;
            }

            FieldInfo networkLobbyField = AccessTools.Field(typeof(UI_MultiplayerPanel), "networkLobby");
            FieldInfo enterHostRequestField = AccessTools.Field(typeof(UI_MultiplayerPanel), "enterHostRequest");
            LobbyManager networkLobby = networkLobbyField?.GetValue(__instance) as LobbyManager;
            if (networkLobbyField == null || enterHostRequestField == null || networkLobby == null)
            {
                return true;
            }

            enterHostRequestField.SetValue(__instance, true);
            networkLobby.Join(lobby);
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), "HandleCreated")]
    internal static class SteamLobbyCreatedProgressPatch
    {
        private static void Prefix(UI_MultiplayerPanel __instance)
        {
            JoinProgressBypass.KeepCurrentFloorWhenCreatingLobby(__instance);
        }

        private static void Postfix(LobbyData data)
        {
            data["pw"] = "open";
            data["SephiriaTogether"] = Plugin.PluginVersion;
            HorayNetworkAuthenticator.allowConnection = true;
            if (Plugin.allowLowerProgressPlayers.Value)
            {
                data["Chapter"] = "0";
            }
        }
    }

    [HarmonyPatch(typeof(UI_MultiplayerPanel), "UpdateChapter", new Type[0])]
    internal static class SteamLobbyChapterUpdatePatch
    {
        private static readonly FieldInfo NetworkLobbyField =
            AccessTools.Field(typeof(UI_MultiplayerPanel), "networkLobby");

        private static void Postfix(UI_MultiplayerPanel __instance)
        {
            LobbyManager manager = NetworkLobbyField?.GetValue(__instance) as LobbyManager;
            if (manager != null && manager.HasLobby && manager.Lobby.IsOwner)
            {
                LobbyData lobby = manager.Lobby;
                lobby["SephiriaTogether"] = Plugin.PluginVersion;
                if (Plugin.allowLowerProgressPlayers.Value)
                {
                    lobby["Chapter"] = "0";
                }
            }
        }
    }

}
