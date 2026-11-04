using System;
using System.Reflection;
using UnityEngine;

namespace SephiriaTogether
{
    internal static class OrganizerKeyReader
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Read only the two audited layouts (Workshop 3.0.0 and BepInEx 2.5.x).
        // Do not instantiate/load the mod, invoke sorting, or write its settings.
        internal static object GetInstance(Type pluginType) => pluginType?
            .GetField("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);

        internal static bool TryReadMainKey(object instance, out KeyCode key)
        {
            key = KeyCode.None;
            if (instance == null) return false;
            FieldInfo parsed = instance.GetType().GetField("mainKey", Members);
            object value;
            if (parsed != null) value = parsed.GetValue(instance);
            else
            {
                object entry = instance.GetType().GetField("Hotkey", Members)?.GetValue(instance);
                object shortcut = entry?.GetType().GetProperty("Value", Members)?.GetValue(entry);
                value = shortcut?.GetType().GetProperty("MainKey", Members)?.GetValue(shortcut);
            }
            return value != null && Enum.TryParse(value.ToString(), true, out key) &&
                   Enum.IsDefined(typeof(KeyCode), key);
        }
    }
}
