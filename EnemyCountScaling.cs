using System;
using HarmonyLib;
using UnityEngine;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(MonsterSpawnPhase), nameof(MonsterSpawnPhase.GenerateSpawnData))]
    internal static class EnemyCountScalingPatch
    {
        private static void Postfix(int multiplayerCount, ref MonsterSpawnPhase __result)
        {
            if (!Plugin.scaleEnemyCount.Value || __result == null || __result.spawnDatas == null ||
                __result.spawnDatas.Count == 0)
            {
                return;
            }

            int extraPlayers = Math.Max(0, multiplayerCount - Plugin.BaselinePlayersValue);
            if (extraPlayers == 0)
            {
                return;
            }

            float multiplier = Mathf.Min(
                Plugin.MaximumEnemyCountMultiplierValue,
                1f + extraPlayers * Plugin.EnemyCountPerExtraPlayerValue);
            int originalCount = __result.spawnDatas.Count;
            int targetCount = Mathf.Max(originalCount, Mathf.RoundToInt(originalCount * multiplier));
            for (int i = originalCount; i < targetCount; i++)
            {
                __result.spawnDatas.Add(__result.spawnDatas[i % originalCount].GetClone());
            }

            if (targetCount > originalCount)
            {
                float economyFactor = (float)targetCount / originalCount;
                foreach (MonsterSpawnData spawn in __result.spawnDatas)
                {
                    spawn.moneyDropPercent /= economyFactor;
                }
            }
        }
    }
}
