using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VPT.Core
{
    public static class SettingsService
    {
        private static readonly string SettingsFile = Path.Combine(AppContext.BaseDirectory, "vpt_settings.json");

        public static string Load(string key, string defaultValue)
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    return settings != null && settings.TryGetValue(key, out var value) ? value : defaultValue;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load setting '{key}'", ex);
            }
            return defaultValue;
        }

        public static void Save(string key, string value)
        {
            try
            {
                Dictionary<string, string> settings = new();
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
                settings[key] = value;
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save setting '{key}'", ex);
            }
        }
    }
}
