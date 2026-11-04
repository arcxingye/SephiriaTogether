#if WORKSHOP
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SephiriaTogether.WorkshopSupport
{
    internal readonly struct KeyboardShortcut
    {
        private readonly KeyCode[] modifiers;
        internal KeyCode MainKey { get; }
        internal IEnumerable<KeyCode> Modifiers => modifiers ?? Array.Empty<KeyCode>();
        internal static KeyboardShortcut Empty => new KeyboardShortcut(KeyCode.None);

        internal KeyboardShortcut(KeyCode mainKey, params KeyCode[] modifiers)
        {
            MainKey = mainKey;
            this.modifiers = (modifiers ?? Array.Empty<KeyCode>())
                .Where(key => key != KeyCode.None && key != mainKey).Distinct().ToArray();
        }

        internal bool IsDown() => ShortcutInput.IsDown(MainKey, Modifiers);

        public override string ToString() => string.Join(" + ", new[] { MainKey }.Concat(Modifiers));

        internal static bool TryParse(string value, out KeyboardShortcut shortcut)
        {
            shortcut = Empty;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Split('+');
            KeyCode[] keys = new KeyCode[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!Enum.TryParse(parts[i].Trim(), true, out keys[i]) || !Enum.IsDefined(typeof(KeyCode), keys[i]))
                    return false;
            }
            shortcut = new KeyboardShortcut(keys[0], keys.Skip(1).ToArray());
            return true;
        }
    }
}
#endif
