using System;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class ModCompatibilityPolicy
    {
        // The Organizer accepts extra held modifiers, so Ctrl+F8 would still
        // sort when its key is F8. Use a different main key, not a modifier.
        internal static KeyCode MenuFallback(KeyCode configuredKey, bool organizerActive,
            KeyCode organizerKey, KeyCode rescueKey)
        {
            if (!organizerActive || organizerKey == KeyCode.None || configuredKey != organizerKey)
                return KeyCode.None;
            foreach (KeyCode candidate in new[] { KeyCode.F9, KeyCode.F10, KeyCode.F11, KeyCode.F12 })
                if (candidate != organizerKey && candidate != rescueKey) return candidate;
            return KeyCode.None;
        }

        internal static bool RestrictStoryProgress(bool nativeCustomGame, bool starActive,
            int bossRush, int infinite) => nativeCustomGame || starActive && (bossRush > 0 || infinite > 0);

        internal static int CaptureRoomCapacity(int requested, int configuredMaximum) =>
            Math.Max(1, Math.Min(requested, Math.Max(2, Math.Min(configuredMaximum, 250))));
    }
}
