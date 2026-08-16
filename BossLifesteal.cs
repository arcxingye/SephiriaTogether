using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace SephiriaTogether
{
    internal static class BossLifesteal
    {
        internal static void Apply(UnitAvatar attacker, float originalPercent)
        {
            if (attacker == null || attacker.monsterType != EMonsterType.Boss && attacker.monsterType != EMonsterType.Miniboss)
            {
                attacker?.HealPercent(originalPercent);
                return;
            }

            if (Plugin.bossLifesteal.Value && originalPercent > 0f) attacker.HealPercent(originalPercent);
        }

        internal static void ApplyFx(UnitAvatar attacker)
        {
            if (attacker == null || attacker.monsterType != EMonsterType.Boss && attacker.monsterType != EMonsterType.Miniboss ||
                Plugin.bossLifesteal.Value)
            {
                attacker?.RpcBloodFestivalHealFx();
            }
        }
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.AttackEvent))]
    internal static class BossLifestealPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            System.Reflection.MethodInfo original = AccessTools.Method(typeof(UnitAvatar), nameof(UnitAvatar.HealPercent), new[] { typeof(float) });
            System.Reflection.MethodInfo replacement = AccessTools.Method(typeof(BossLifesteal), nameof(BossLifesteal.Apply));
            System.Reflection.MethodInfo originalFx = AccessTools.Method(typeof(UnitAvatar), nameof(UnitAvatar.RpcBloodFestivalHealFx));
            System.Reflection.MethodInfo replacementFx = AccessTools.Method(typeof(BossLifesteal), nameof(BossLifesteal.ApplyFx));
            foreach (CodeInstruction instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                    instruction.operand as System.Reflection.MethodInfo == original)
                {
                    yield return new CodeInstruction(OpCodes.Call, replacement).MoveLabelsFrom(instruction);
                }
                else if ((instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
                         instruction.operand as System.Reflection.MethodInfo == originalFx)
                {
                    yield return new CodeInstruction(OpCodes.Call, replacementFx).MoveLabelsFrom(instruction);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
}
