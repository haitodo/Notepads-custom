// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2019-2024, Jiaqi (0x7c13) Liu. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Settings
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Text.Json;
    using Notepads.Services;

    public static class ApplicationSettingsStore
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Notepads",
            "settings.json");

        private static readonly ConcurrentDictionary<string, object> Settings = new ConcurrentDictionary<string, object>();

        static ApplicationSettingsStore()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var dict = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            if (kvp.Value is JsonElement jsonEl)
                            {
                                Settings[kvp.Key] = ConvertJsonElement(jsonEl);
                            }
                            else
                            {
                                Settings[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex}");
            }
        }

        private static object ConvertJsonElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int i)) return i;
                    if (element.TryGetInt64(out long l)) return l;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Null:
                    return null;
                default:
                    return element.ToString();
            }
        }

        private static void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Settings);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to save settings: {ex.Message}");
            }
        }

        public static object Read(string key)
        {
            return Settings.TryGetValue(key, out var value) ? value : null;
        }

        public static void Write(string key, object obj)
        {
            Settings[key] = obj;
            Save();
        }

        public static bool Remove(string key)
        {
            if (Settings.TryRemove(key, out _))
            {
                Save();
                return true;
            }
            return false;
        }
    }
}