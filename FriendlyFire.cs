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
                attacker == __instance || damage.isSystemDamage ||
                CloneBotManager.IsBot(attacker.spawner) ||
                CloneBotManager.IsBot((__instance as PlayerAvatar)?.spawner))
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
            damage.failed = EDamageFailType.None;
        }

        private static void Postfix(UnitAvatar __instance, DamageInstance damage, EApplyDamageResult __result, State __state)
        {
            RestorePeaceMode(__state);
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

    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.ApplyDamage))]
    internal static class CloneBotDamageBoundaryPatch
    {
        private static bool Prefix(UnitAvatar __instance, DamageInstance damage, ref EApplyDamageResult __result)
        {
            PlayerAvatar target = __instance as PlayerAvatar;
            PlayerAvatar attacker = damage?.origin as PlayerAvatar;
            if (target == null || attacker == null ||
                (!CloneBotManager.IsBot(target.spawner) && !CloneBotManager.IsBot(attacker.spawner)))
                return true;
            __result = EApplyDamageResult.Fail_Absolute;
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "HandleBeforeAttack")]
    internal static class FriendlyFireRelationPatch
    {
        private static bool Prefix(UnitAvatar target, DamageInstance damage)
        {
            if (Plugin.friendlyFire.Value && target is PlayerAvatar victim &&
                damage?.origin is PlayerAvatar attacker && attacker != victim &&
                !CloneBotManager.IsBot(attacker.spawner) && !CloneBotManager.IsBot(victim.spawner))
            {
                damage.failed = EDamageFailType.None;
                return false;
            }
            return true;
        }

        private static void Postfix(UnitAvatar target, DamageInstance damage)
        {
            if (Plugin.friendlyFire.Value && target is PlayerAvatar victim &&
                damage?.origin is PlayerAvatar attacker && attacker != victim &&
                !CloneBotManager.IsBot(attacker.spawner) && !CloneBotManager.IsBot(victim.spawner))
            {
                damage.failed = EDamageFailType.None;
            }
        }
    }
}
