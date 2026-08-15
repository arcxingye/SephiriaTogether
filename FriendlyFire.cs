using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.ApplyDamage))]
    internal static class FriendlyFirePatch
    {
        private sealed class State
        {
            internal bool Friendly;
            internal bool PreviousPeaceMode;
        }

        private static void Prefix(UnitAvatar __instance, DamageInstance damage, out State __state)
        {
            __state = new State();
            if (!NetworkServer.active || !Plugin.friendlyFire.Value ||
                !(__instance is PlayerAvatar) || !(damage?.origin is PlayerAvatar attacker) ||
                attacker == __instance || damage.isSystemDamage)
            {
                return;
            }

            __state.Friendly = true;
            if (CombatManager.Instance != null)
            {
                __state.PreviousPeaceMode = CombatManager.Instance.PeaceMode;
                CombatManager.Instance.PeaceMode = false;
            }
            damage.targetFactionLayers = -1L;
            damage.damage = UnityEngine.Mathf.Clamp(damage.damage * 0.01f, 1f, 5f);
        }

        private static void Postfix(UnitAvatar __instance, DamageInstance damage, EApplyDamageResult __result, State __state)
        {
            if (__state != null && __state.Friendly)
            {
                Plugin.LogInfo(
                    $"Friendly fire result={__result}, damage={damage.damage:0.##}, " +
                    $"applied={damage.damageResult}, target={__instance.name}, " +
                    $"sameLeader={__instance.NetworkLeader == damage.origin}, " +
                    $"targetFaction={__instance.faction}, attackerFaction={(damage.origin as UnitAvatar)?.faction}, " +
                    $"layers={damage.targetFactionLayers}, peace={CombatManager.Instance?.PeaceMode}, " +
                    $"dead={__instance.IsDead}, invulnerable={__instance.IsInvulnerable}, " +
                    $"lifeInvulnerable={GetLifeInvulnerable(__instance)}, " +
                    $"pitfall={__instance.TopdownRigidbody != null && __instance.TopdownRigidbody.IsPitFalling}.");
            }
            RestorePeaceMode(__state);
        }

        private static bool GetLifeInvulnerable(UnitAvatar avatar)
        {
            return (bool)(AccessTools.Field(typeof(UnitAvatar), "isLifeInvincibleApplied")?.GetValue(avatar) ?? false);
        }

        private static System.Exception Finalizer(System.Exception __exception, State __state)
        {
            RestorePeaceMode(__state);
            return __exception;
        }

        private static void RestorePeaceMode(State state)
        {
            if (state != null && state.Friendly && CombatManager.Instance != null)
            {
                CombatManager.Instance.PeaceMode = state.PreviousPeaceMode;
            }
        }
    }

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.GetHostileFactionLayers))]
    internal static class FriendlyFireTargetPatch
    {
        private static void Postfix(PlayerAvatar __instance, ref long __result)
        {
            if (NetworkServer.active && Plugin.friendlyFire.Value && __instance != null &&
                RuntimeFactionManager.Instance != null)
            {
                __result |= RuntimeFactionManager.Instance.FindFactionLayer(__instance.faction);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "HandleBeforeAttack")]
    internal static class FriendlyFireRelationPatch
    {
        private static bool Prefix(UnitAvatar target, DamageInstance damage)
        {
            if (Plugin.friendlyFire.Value && target is PlayerAvatar victim &&
                damage?.origin is PlayerAvatar attacker && attacker != victim)
            {
                return false;
            }
            return true;
        }
    }
}
