using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;

namespace SephiriaTogether
{
    [HarmonyPatch(typeof(UI_MultiplayerPanel), "OnJoinButton", new[] { typeof(LobbyData) })]
    internal static class SteamJoinProgressPatch
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // LobbyData exposes multiple Item indexers. PropertyGetter("Item")
            // is ambiguous at runtime; select the string metadata accessor.
            MethodInfo getter = AccessTools.Method(typeof(LobbyData), "get_Item", new[] { typeof(string) });
            if (getter == null || getter.ReturnType != typeof(string))
                throw new MissingMethodException("LobbyData.get_Item(string) returning string was not found.");
            MethodInfo normalize = AccessTools.Method(typeof(SteamJoinProgressPatch), nameof(ChapterForAdmission));
            CodeInstruction previous = null;
            int matches = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(getter) && previous != null && previous.opcode == OpCodes.Ldstr &&
                    Equals(previous.operand, "Chapter"))
                {
                    yield return new CodeInstruction(OpCodes.Call, normalize);
                    matches++;
                }
                previous = instruction;
            }
            if (matches != 1)
                throw new InvalidOperationException("Steam join chapter read changed; expected one admission check.");
        }

        private static string ChapterForAdmission(string chapter)
        {
            // Keep the native version/Workshop/mode checks and JoinLobbyFromUI
            // state machine. Only relax a well-formed story chapter requirement.
            if (!Plugin.allowLowerProgressPlayers.Value || string.IsNullOrEmpty(chapter)) return chapter;
            string[] parts = chapter.Split('-');
            if (parts.Length < 1 || parts.Length > 2 || !int.TryParse(parts[0], out int main) || main < 0 ||
                parts.Length == 2 && (!int.TryParse(parts[1], out int sub) || sub < 0)) return chapter;
            return "0";
        }
    }
}
