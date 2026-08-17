using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(MonsterSpawnPhase), nameof(MonsterSpawnPhase.GenerateSpawnData))]
    internal static class EnemyCountScalingPatch
    {
        private const int MaximumConcurrentEnemies = 32;

        private static void Postfix(MonsterSpawnPhase __instance, int multiplayerCount,
            ref MonsterSpawnPhase __result)
        {
            if (!Plugin.scaleEnemyCount.Value || __instance?.spawnDatas == null ||
                __instance.spawnDatas.Count == 0 || __result?.spawnDatas == null ||
                __result.spawnDatas.Count == 0) return;

            int templateCount = __instance.spawnDatas.Count;
            int[] baseCounts = new int[templateCount];
            MonsterSpawnData[] templates = new MonsterSpawnData[templateCount];
            for (int i = 0; i < __result.spawnDatas.Count; i++)
            {
                int bucket = i % templateCount;
                MonsterSpawnData generated = __result.spawnDatas[i];
                if (generated == null) continue;
                if (templates[bucket] == null) templates[bucket] = generated.GetClone();
                baseCounts[bucket] += Math.Max(0, generated.count);
            }

            int extraPlayers = Math.Max(0, multiplayerCount - Plugin.BaselinePlayersValue);
            float multiplier = extraPlayers > 0
                ? Mathf.Min(Plugin.MaximumEnemyCountMultiplierValue,
                    1f + extraPlayers * Plugin.EnemyCountPerExtraPlayerValue)
                : 1f;
            int baseTotal = baseCounts.Sum();
            int targetTotal = Mathf.Clamp(Mathf.RoundToInt(baseTotal * multiplier), baseTotal,
                MaximumConcurrentEnemies);
            int[] targetCounts = DistributeCounts(baseCounts, baseTotal, targetTotal);
            float economyFactor = baseTotal > 0 ? (float)targetTotal / baseTotal : 1f;

            __result.spawnDatas.Clear();
            for (int i = 0; i < templateCount; i++)
            {
                MonsterSpawnData spawn = templates[i] ?? __instance.spawnDatas[i].GetClone();
                spawn.count = targetCounts[i];
                if (economyFactor > 1f) spawn.moneyDropPercent /= economyFactor;
                if (spawn.count > 0) __result.spawnDatas.Add(spawn);
            }
        }

        private static int[] DistributeCounts(int[] baseCounts, int baseTotal, int targetTotal)
        {
            int[] result = new int[baseCounts.Length];
            if (baseTotal <= 0) return result;
            List<KeyValuePair<int, float>> remainders = new List<KeyValuePair<int, float>>();
            int assigned = 0;
            for (int i = 0; i < baseCounts.Length; i++)
            {
                float exact = (float)baseCounts[i] * targetTotal / baseTotal;
                result[i] = Mathf.FloorToInt(exact);
                assigned += result[i];
                remainders.Add(new KeyValuePair<int, float>(i, exact - result[i]));
            }
            foreach (KeyValuePair<int, float> remainder in remainders.OrderByDescending(value => value.Value))
            {
                if (assigned >= targetTotal) break;
                result[remainder.Key]++;
                assigned++;
            }
            return result;
        }

        internal static int VanillaConcurrentLimit(int players)
        {
            int[] limits = { 5, 5, 6, 7, 7, 8, 9, 10, 11 };
            return limits[Mathf.Clamp(players - 1, 0, limits.Length - 1)];
        }

        internal static int DesiredConcurrentLimit(RandomEnemyPhaseSpawner spawner)
        {
            if (!Plugin.scaleEnemyCount.Value || spawner?.spawnPhases?.phases == null) return 0;
            int largestPhase = spawner.spawnPhases.phases
                .Where(phase => phase?.spawnDatas != null)
                .Select(phase => phase.spawnDatas.Sum(data => data != null ? Math.Max(0, data.count) : 0))
                .DefaultIfEmpty(0)
                .Max();
            return Mathf.Clamp(largestPhase, 0, MaximumConcurrentEnemies);
        }
    }

    [HarmonyPatch(typeof(RandomEnemyPhaseSpawner), nameof(RandomEnemyPhaseSpawner.StartSpawn))]
    internal static class EnemyConcurrentLimitPatch
    {
        private static void Prefix(RandomEnemyPhaseSpawner __instance)
        {
            int desired = EnemyCountScalingPatch.DesiredConcurrentLimit(__instance);
            if (desired <= 0) return;
            int players = PlayerSpawner.MultiplayerList?.Count ?? 1;
            int vanilla = EnemyCountScalingPatch.VanillaConcurrentLimit(players);
            __instance.additionalUnitCountLimit = Math.Max(__instance.additionalUnitCountLimit,
                Math.Max(0, desired - vanilla));
        }
    }
}
