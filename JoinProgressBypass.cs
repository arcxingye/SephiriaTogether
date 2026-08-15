using System;
using System.Reflection;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class JoinProgressBypass
    {
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

            if (Application.version != lobby.GameVersion)
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
        private static void Postfix(LobbyData data)
        {
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
            if (!Plugin.allowLowerProgressPlayers.Value)
            {
                return;
            }

            LobbyManager manager = NetworkLobbyField?.GetValue(__instance) as LobbyManager;
            if (manager != null && manager.HasLobby && manager.Lobby.IsOwner)
            {
                LobbyData lobby = manager.Lobby;
                lobby["Chapter"] = "0";
            }
        }
    }

}
