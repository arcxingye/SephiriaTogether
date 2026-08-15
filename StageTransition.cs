using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class StageTransition
    {
        internal static string PendingStageName { get; private set; }

        internal static bool CanForce =>
            NetworkServer.active &&
            DungeonManager.Instance != null &&
            !string.IsNullOrEmpty(PendingStageName) &&
            DungeonManager.Instance.FindStage(PendingStageName) != null;

        internal static bool ForcePendingStage()
        {
            if (!CanForce)
            {
                return false;
            }

            DungeonManager.Instance.LoadStageAndMove(PendingStageName);
            PendingStageName = null;
            return true;
        }

        internal static void Clear() => PendingStageName = null;

        internal static void Remember(string stageName)
        {
            if (NetworkServer.active && !string.IsNullOrEmpty(stageName))
            {
                PendingStageName = stageName;
            }
        }
    }

    [HarmonyPatch]
    internal static class StageRequestCapturePatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(
            typeof(PlayerAvatar),
            "UserCode_CmdMoveStage__Boolean__PlayerAvatar__Vector3__String");

        private static void Prefix(string stageName) => StageTransition.Remember(stageName);
    }

    [HarmonyPatch]
    internal static class DungeonProgressValidationPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(
            typeof(DungeonManager),
            "UserCode_CmdSendProgressValidationRequest__Int32__Int32__Boolean__String_005B_005D__Int32__Int32__NetworkConnectionToClient");

        private static void Prefix(ref int clientChapterNum)
        {
            if (NetworkServer.active && Plugin.allowLowerProgressPlayers.Value && clientChapterNum >= 0)
            {
                clientChapterNum = int.MaxValue;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.ServerLoadStage))]
    internal static class StageProgressRequirementPatch
    {
        private static void Prefix(string stageName, out Dictionary<PlayerLocalDataStorage, int> __state)
        {
            __state = null;
            StageTransition.Remember(stageName);
            if (!NetworkServer.active || !Plugin.allowLowerProgressPlayers.Value ||
                PlayerSpawner.MultiplayerList == null)
            {
                return;
            }

            int hostProgress = -1;
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
            {
                if (player != null && player.isHost && player.PlayerAvatar != null &&
                    player.PlayerAvatar.localDataStorage != null)
                {
                    hostProgress = player.PlayerAvatar.localDataStorage.mainQuestProgress;
                    break;
                }
            }

            if (hostProgress < 0)
            {
                return;
            }

            __state = new Dictionary<PlayerLocalDataStorage, int>();
            foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
            {
                PlayerLocalDataStorage storage = player != null && player.PlayerAvatar != null
                    ? player.PlayerAvatar.localDataStorage
                    : null;
                if (storage != null && storage.mainQuestProgress < hostProgress)
                {
                    __state[storage] = storage.mainQuestProgress;
                    storage.mainQuestProgress = hostProgress;
                }
            }
        }

        private static void Postfix(Dictionary<PlayerLocalDataStorage, int> __state) => Restore(__state);

        private static System.Exception Finalizer(System.Exception __exception, Dictionary<PlayerLocalDataStorage, int> __state)
        {
            Restore(__state);
            return __exception;
        }

        private static void Restore(Dictionary<PlayerLocalDataStorage, int> state)
        {
            if (state == null)
            {
                return;
            }

            foreach (KeyValuePair<PlayerLocalDataStorage, int> entry in state)
            {
                if (entry.Key != null)
                {
                    entry.Key.mainQuestProgress = entry.Value;
                }
            }
            state.Clear();
        }
    }
}
