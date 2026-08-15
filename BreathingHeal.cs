using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(PlayerAvatar), "Update")]
    internal static class BreathingHealPatch
    {
        private const float RecoveryDelay = 10f;
        private static readonly Dictionary<PlayerAvatar, float> Timers =
            new Dictionary<PlayerAvatar, float>();

        private static void Prefix(PlayerAvatar __instance)
        {
            if (!NetworkServer.active || !Plugin.breathingHeal.Value || __instance == null)
            {
                return;
            }

            if (__instance.IsDead)
            {
                Timers.Remove(__instance);
                return;
            }

            float timer = Timers.TryGetValue(__instance, out float elapsed) ? elapsed : 0f;
            timer += Time.deltaTime;
            if (timer >= RecoveryDelay)
            {
                __instance.Heal(Time.deltaTime,
                    allowOverHeal: false,
                    ignorePenalty: false);
            }
            Timers[__instance] = timer;
        }

        internal static void Clear() => Timers.Clear();

        internal static void Reset(PlayerAvatar avatar)
        {
            if (avatar != null)
            {
                Timers[avatar] = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.ApplyDamage))]
    internal static class BreathingHealDamagePatch
    {
        private static void Postfix(UnitAvatar __instance, EApplyDamageResult __result)
        {
            if (__result == EApplyDamageResult.Success && __instance is PlayerAvatar avatar)
            {
                BreathingHealPatch.Reset(avatar);
            }
        }
    }
}
