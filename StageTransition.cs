using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(PlayerAvatar), nameof(PlayerAvatar.RequestWorldMap))]
    internal static class WorldMapGatherRequirementPatch
    {
        private static void Prefix(Vector3 requestPosition, out Dictionary<Transform, Vector3> __state)
        {
            __state = null;
            if (!NetworkServer.active || !Plugin.allowUngroupedStageTransition.Value)
            {
                return;
            }

            __state = new Dictionary<Transform, Vector3>();
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                PlayerAvatar avatar = connection != null && connection.identity != null
                    ? connection.identity.GetComponent<PlayerAvatar>()
                    : null;
                if (avatar != null && !avatar.IsDead)
                {
                    __state[avatar.transform] = avatar.transform.position;
                    avatar.transform.position = requestPosition;
                }
            }
            Plugin.LogInfo($"Bypassing world-map gather distance for {__state.Count} living players.");
        }

        private static void Postfix(Dictionary<Transform, Vector3> __state) => Restore(__state);

        private static System.Exception Finalizer(System.Exception __exception, Dictionary<Transform, Vector3> __state)
        {
            Restore(__state);
            return __exception;
        }

        private static void Restore(Dictionary<Transform, Vector3> positions)
        {
            if (positions == null)
            {
                return;
            }

            foreach (KeyValuePair<Transform, Vector3> entry in positions)
            {
                if (entry.Key != null)
                {
                    entry.Key.position = entry.Value;
                }
            }
            positions.Clear();
        }
    }

    [HarmonyPatch]
    internal static class StageEntranceSessionCheckPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(GoToNextStage), "MoveTo");
            yield return AccessTools.Method(typeof(GoToNextStage_MultiZone), "MoveTo");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo memberCountGetter = AccessTools.PropertyGetter(typeof(LobbyManager), "MemberCount");
            MethodInfo replacement = AccessTools.Method(typeof(StageEntranceSessionCheckPatch), nameof(GetMemberCount));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(memberCountGetter))
                {
                    yield return new CodeInstruction(OpCodes.Call, replacement);
                }
                else
                {
                    yield return instruction;
                }
            }
        }

        private static int GetMemberCount(LobbyManager manager)
        {
            if (Plugin.allowUngroupedStageTransition.Value && PlayerSpawner.MultiplayerList != null)
            {
                Plugin.LogInfo(
                    $"Bypassing stage entrance session check: lobby={manager.MemberCount}, " +
                    $"session={Plugin.PlayerCount}.");
                return Plugin.PlayerCount;
            }
            return manager.MemberCount;
        }
    }

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

        private static void Prefix(PlayerAvatar __instance, Vector3 requestPosition, out State __state)
        {
            __state = null;
            if (!NetworkServer.active)
            {
                return;
            }

            __state = new State();
            if (Plugin.allowLowerProgressPlayers.Value)
            {
                int hostProgress = __instance != null && __instance.localDataStorage != null
                    ? __instance.localDataStorage.mainQuestProgress
                    : -1;

                if (hostProgress >= 0)
                {
                    foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
                    {
                        PlayerAvatar avatar = GetAvatar(connection);
                        PlayerLocalDataStorage storage = avatar != null ? avatar.localDataStorage : null;
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
                foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
                {
                    PlayerAvatar avatar = GetAvatar(connection);
                    if (avatar != null && !avatar.IsDead)
                    {
                        __state.Positions[avatar.transform] = avatar.transform.position;
                        avatar.transform.position = requestPosition;
                    }
                }
                Plugin.LogInfo($"Bypassing stage entrance distance for {__state.Positions.Count} living players.");
            }
        }

        private static PlayerAvatar GetAvatar(NetworkConnectionToClient connection)
        {
            return connection != null && connection.identity != null
                ? connection.identity.GetComponent<PlayerAvatar>()
                : null;
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
