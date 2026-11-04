using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaTogether
{
    internal static class ShortcutInput
    {
        internal static bool IsDown(KeyCode mainKey, IEnumerable<KeyCode> modifiers)
        {
            if (mainKey == KeyCode.None) return false;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && TryGetInputKey(mainKey, out Key key))
            {
                try
                {
                    if (!keyboard[key].wasPressedThisFrame) return false;
                    foreach (KeyCode modifier in modifiers)
                        if (!TryGetInputKey(modifier, out Key modifierKey) || !keyboard[modifierKey].isPressed)
                            return false;
                    return true;
                }
                catch (ArgumentException) { }
            }
            try
            {
                if (!Input.GetKeyDown(mainKey)) return false;
                foreach (KeyCode modifier in modifiers)
                    if (!Input.GetKey(modifier)) return false;
                return true;
            }
            catch (InvalidOperationException) { return false; }
        }

        private static bool TryGetInputKey(KeyCode keyCode, out Key key)
        {
            string name = keyCode.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal)) name = "Digit" + name.Substring(5);
            else if (name.StartsWith("Keypad", StringComparison.Ordinal)) name = "Numpad" + name.Substring(6);
            switch (name)
            {
                case "BackQuote": name = "Backquote"; break;
                case "LeftControl": name = "LeftCtrl"; break;
                case "RightControl": name = "RightCtrl"; break;
                case "LeftCommand": case "LeftWindows": case "LeftApple": name = "LeftMeta"; break;
                case "RightCommand": case "RightWindows": case "RightApple": name = "RightMeta"; break;
                case "Print": name = "PrintScreen"; break;
                case "Return": name = "Enter"; break;
            }
            return Enum.TryParse(name, true, out key);
        }
    }
}
