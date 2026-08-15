using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    [HarmonyPatch]
    internal static class DungeonProgressValidationPatch
    {
        private static bool Prepare() => TargetMethod() != null;

        private static MethodBase TargetMethod() => typeof(DungeonManager)
            .GetMethods(AccessTools.all)
            .FirstOrDefault(method => method.Name.StartsWith("UserCode_CmdSendProgressValidationRequest") &&
                                      method.GetParameters().Length == 7);

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
        private sealed class State
        {
            internal readonly Dictionary<PlayerLocalDataStorage, int> Progress =
                new Dictionary<PlayerLocalDataStorage, int>();
            internal readonly Dictionary<Transform, Vector3> Positions =
                new Dictionary<Transform, Vector3>();
        }

        private static void Prefix(Vector3 requestPosition, out State __state)
        {
            __state = null;
            if (!NetworkServer.active || PlayerSpawner.MultiplayerList == null)
            {
                return;
            }

            __state = new State();
            if (Plugin.allowLowerProgressPlayers.Value)
            {
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

                if (hostProgress >= 0)
                {
                    foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
                    {
                        PlayerLocalDataStorage storage = player != null && player.PlayerAvatar != null
                            ? player.PlayerAvatar.localDataStorage
                            : null;
                        if (storage != null && storage.mainQuestProgress < hostProgress)
                        {
                            __state.Progress[storage] = storage.mainQuestProgress;
                            storage.mainQuestProgress = hostProgress;
                        }
                    }
                }
            }

            if (Plugin.allowUngroupedStageTransition.Value)
            {
                foreach (PlayerSpawner player in PlayerSpawner.MultiplayerList)
                {
                    PlayerAvatar avatar = player != null ? player.PlayerAvatar : null;
                    if (avatar != null && !avatar.IsDead)
                    {
                        __state.Positions[avatar.transform] = avatar.transform.position;
                        avatar.transform.position = requestPosition;
                    }
                }
            }
        }

        private static void Postfix(State __state) => Restore(__state);

        private static System.Exception Finalizer(System.Exception __exception, State __state)
        {
            Restore(__state);
            return __exception;
        }

        private static void Restore(State state)
        {
            if (state == null)
            {
                return;
            }

            foreach (KeyValuePair<PlayerLocalDataStorage, int> entry in state.Progress)
            {
                if (entry.Key != null)
                {
                    entry.Key.mainQuestProgress = entry.Value;
                }
            }
            state.Progress.Clear();
            foreach (KeyValuePair<Transform, Vector3> entry in state.Positions)
            {
                if (entry.Key != null)
                {
                    entry.Key.position = entry.Value;
                }
            }
            state.Positions.Clear();
        }
    }
}
