using System;
using System.Collections.Generic;

// Minimal stand-ins for the native storage and input APIs. The tests compile the
// actual adapter/input source; these stand-ins do not simulate Unity networking.
public sealed class ModOptions
{
    public bool IsAvailable { get; set; } = true;
    public Dictionary<string, object> Values { get; } = new Dictionary<string, object>();
    public Dictionary<string, object> Saved { get; private set; } = new Dictionary<string, object>();
    public int SaveCount { get; private set; }
    private T Get<T>(string key, T fallback) => Values.TryGetValue(key, out object value) && value is T typed ? typed : fallback;
    public bool GetBool(string key, bool fallback) => Get(key, fallback);
    public int GetInt(string key, int fallback) => Get(key, fallback);
    public float GetFloat(string key, float fallback) => Get(key, fallback);
    public string GetString(string key, string fallback) => Get(key, fallback);
    public void SetBool(string key, bool value) => Values[key] = value;
    public void SetInt(string key, int value) => Values[key] = value;
    public void SetFloat(string key, float value) => Values[key] = value;
    public void SetString(string key, string value) => Values[key] = value;
    public bool Remove(string key) => Values.Remove(key);
    public bool Save() { Saved = new Dictionary<string, object>(Values); SaveCount++; return true; }
}

namespace UnityEngine
{
    public enum KeyCode { None, F8, F9, F10, F11, F12, R, A, Alpha1, Keypad2, Return, LeftControl, RightControl, LeftShift, RightShift, LeftAlt, RightAlt, LeftCommand, RightCommand, Mouse0 }
    public static class Debug { public static void LogWarning(object value) { } }
    public static class Input
    {
        public static bool Available;
        public static readonly HashSet<KeyCode> Down = new HashSet<KeyCode>();
        public static readonly HashSet<KeyCode> Held = new HashSet<KeyCode>();
        public static bool GetKeyDown(KeyCode key) => Available ? Down.Contains(key) : throw new InvalidOperationException("Legacy input disabled");
        public static bool GetKey(KeyCode key) => Available ? Held.Contains(key) : throw new InvalidOperationException("Legacy input disabled");
    }
}

namespace UnityEngine.InputSystem
{
    public enum Key { F8, F9, F10, F11, F12, R, A, Digit1, Numpad2, Enter, LeftCtrl, RightCtrl, LeftShift, RightShift, LeftAlt, RightAlt, LeftMeta, RightMeta }
    public sealed class KeyControl
    {
        public bool wasPressedThisFrame;
        public bool isPressed;
    }
    public sealed class Keyboard
    {
        public static Keyboard current;
        private readonly Dictionary<Key, KeyControl> keys = new Dictionary<Key, KeyControl>();
        public KeyControl this[Key key]
        {
            get
            {
                if (!keys.TryGetValue(key, out KeyControl value)) keys[key] = value = new KeyControl();
                return value;
            }
        }
    }
}
