#if WORKSHOP
using System;
using System.Collections.Generic;

namespace SephiriaTogether.WorkshopSupport
{
    // A small adapter for the settings used by the shared plugin. Storage belongs
    // to the game's ModOptions API, not to the downloaded Workshop directory.
    internal sealed class ConfigFile
    {
        private readonly ModOptions options;
        private readonly Dictionary<string, object> entries = new Dictionary<string, object>(StringComparer.Ordinal);
        internal bool SaveOnConfigSet { get; set; } = true;

        internal ConfigFile(ModOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            if (!options.IsAvailable)
                throw new InvalidOperationException("Sephiria Together requires initialized OptionsBinding.DeviceOptions.");
        }

        internal ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description = "") =>
            Bind(section, key, defaultValue, new ConfigDescription(description));

        internal ConfigEntry<T> Bind<T>(ConfigDefinition definition, T defaultValue) =>
            Bind(definition.Section, definition.Key, defaultValue);

        internal ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription description)
        {
            string storageKey = section + "/" + key;
            if (entries.TryGetValue(storageKey, out object existing))
                return existing as ConfigEntry<T> ?? throw new InvalidOperationException("Setting type changed: " + storageKey);
            ConfigEntry<T> entry = new ConfigEntry<T>(this, storageKey, defaultValue, description?.Range);
            entries.Add(storageKey, entry);
            return entry;
        }

        internal T Read<T>(string key, T fallback)
        {
            if (typeof(T) == typeof(bool)) return (T)(object)options.GetBool(key, (bool)(object)fallback);
            if (typeof(T) == typeof(int)) return (T)(object)options.GetInt(key, (int)(object)fallback);
            if (typeof(T) == typeof(float)) return (T)(object)options.GetFloat(key, (float)(object)fallback);
            if (typeof(T) == typeof(string)) return (T)(object)options.GetString(key, (string)(object)fallback);
            if (typeof(T) == typeof(KeyboardShortcut))
            {
                string value = options.GetString(key, fallback.ToString());
                return KeyboardShortcut.TryParse(value, out KeyboardShortcut shortcut) ? (T)(object)shortcut : fallback;
            }
            throw new NotSupportedException("Unsupported Workshop setting type: " + typeof(T).FullName);
        }

        internal void Write<T>(string key, T value)
        {
            if (typeof(T) == typeof(bool)) options.SetBool(key, (bool)(object)value);
            else if (typeof(T) == typeof(int)) options.SetInt(key, (int)(object)value);
            else if (typeof(T) == typeof(float)) options.SetFloat(key, (float)(object)value);
            else if (typeof(T) == typeof(string)) options.SetString(key, (string)(object)value ?? "");
            else if (typeof(T) == typeof(KeyboardShortcut)) options.SetString(key, value.ToString());
            else throw new NotSupportedException("Unsupported Workshop setting type: " + typeof(T).FullName);
            if (SaveOnConfigSet) Save();
        }

        internal void Remove(ConfigDefinition definition)
        {
            string key = definition.Section + "/" + definition.Key;
            entries.Remove(key);
            if (options.Remove(key) && SaveOnConfigSet) Save();
        }

        internal void Save()
        {
            if (!options.Save())
                UnityEngine.Debug.LogWarning("[Sephiria Together] Could not persist Workshop options.");
        }
    }

    internal sealed class ConfigEntry<T>
    {
        private readonly ConfigFile file;
        private readonly string key;
        private readonly T fallback;
        private readonly IValueRange range;

        internal ConfigEntry(ConfigFile file, string key, T fallback, IValueRange range)
        {
            this.file = file;
            this.key = key;
            this.fallback = fallback;
            this.range = range;
        }

        internal T Value
        {
            get => Normalize(file.Read(key, fallback));
            set => file.Write(key, Normalize(value));
        }

        private T Normalize(T value)
        {
            if (value is float number && (float.IsNaN(number) || float.IsInfinity(number))) value = fallback;
            return range != null ? (T)range.Clamp(value) : value;
        }
    }

    internal sealed class ConfigDefinition
    {
        internal string Section { get; }
        internal string Key { get; }
        internal ConfigDefinition(string section, string key) { Section = section; Key = key; }
    }

    internal sealed class ConfigDescription
    {
        internal IValueRange Range { get; }
        internal ConfigDescription(string description, IValueRange range = null) { Range = range; }
    }

    internal interface IValueRange { object Clamp(object value); }

    internal sealed class AcceptableValueRange<T> : IValueRange where T : IComparable<T>
    {
        private readonly T minimum;
        private readonly T maximum;
        internal AcceptableValueRange(T minimum, T maximum) { this.minimum = minimum; this.maximum = maximum; }
        public object Clamp(object value)
        {
            T typed = (T)value;
            return typed.CompareTo(minimum) < 0 ? minimum : typed.CompareTo(maximum) > 0 ? maximum : typed;
        }
    }
}
#endif
