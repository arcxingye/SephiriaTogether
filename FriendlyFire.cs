using HarmonyLib;
using Mirror;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(UnitAvatar), nameof(UnitAvatar.ApplyDamage))]
    internal static class MerchantProtectionPatch
    {
        private static readonly AccessTools.FieldRef<UnitAI_NewBasic, EProceduralMerchantType> MerchantType =
            AccessTools.FieldRefAccess<UnitAI_NewBasic, EProceduralMerchantType>("merchantType");

        private static bool Prefix(UnitAvatar __instance, DamageInstance damage, ref EApplyDamageResult __result)
        {
            if (!NetworkServer.active || Plugin.allowAttackingMerchants.Value || damage == null ||
                damage.isSystemDamage || !IsMerchant(__instance) || !IsPlayerControlled(damage.origin as UnitAvatar))
            {
                return true;
            }

            damage.failed = EDamageFailType.Deny;
            __result = EApplyDamageResult.Fail_Absolute;
            return false;
        }

        private static bool IsMerchant(UnitAvatar avatar)
        {
            UnitAI_NewBasic ai = avatar != null ? avatar.GetComponent<UnitAI_NewBasic>() : null;
            return ai != null && (MerchantType(ai) != EProceduralMerchantType.None || avatar.faction == "Merchant");
        }

        private static bool IsPlayerControlled(UnitAvatar attacker)
        {
            for (int depth = 0; attacker != null && depth < 8; depth++)
            {
                if (attacker is PlayerAvatar) return true;
                UnitAvatar leader = attacker.NetworkLeader;
                if (leader == null || leader == attacker) return false;
                attacker = leader;
            }
            return false;
        }
    }

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

    [HarmonyPatch(typeof(PlayerAvatar), "HandleBeforeAttack")]
    internal static class FriendlyFireRelationPatch
    {
        private static bool Prefix(UnitAvatar target, DamageInstance damage)
        {
            if (Plugin.friendlyFire.Value && target is PlayerAvatar victim &&
                damage?.origin is PlayerAvatar attacker && attacker != victim)
            {
                damage.failed = EDamageFailType.None;
                return false;
            }
            return true;
        }

        private static void Postfix(UnitAvatar target, DamageInstance damage)
        {
            if (Plugin.friendlyFire.Value && target is PlayerAvatar victim &&
                damage?.origin is PlayerAvatar attacker && attacker != victim)
            {
                damage.failed = EDamageFailType.None;
            }
        }
    }
}
