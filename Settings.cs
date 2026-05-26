using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HsConquest
{
    /// <summary>
    /// Plugin configuration persisted to
    /// %AppData%/HearthstoneDeckTracker/Plugins/HsConquest/settings.json.
    ///
    /// Two fields:
    ///   - SyncUrl: the URL the user got from clicking "Sync to plugin" on
    ///     Tab 4 of hsconquest.netlify.app. The plugin GETs this URL to
    ///     pull the latest matchup matrix.
    ///   - DeckToArchetype: maps the user's HDT deck names (free-form
    ///     strings the user picked when they imported their deck into HDT)
    ///     to the archetype name as it appears in the matrix (e.g. "Aggro
    ///     Druid"). The user fills this in once via the settings panel.
    ///
    /// JSON file is human-readable so the user can hand-edit it if they
    /// rename a deck and don't want to re-open the settings UI.
    /// </summary>
    public class Settings
    {
        public string SyncUrl { get; set; } = "";

        public Dictionary<string, string> DeckToArchetype { get; set; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ---- Disk persistence ----

        public static string SettingsDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HearthstoneDeckTracker",
                "Plugins",
                "HsConquest"
            );

        public static string SettingsFile => Path.Combine(SettingsDirectory, "settings.json");

        public static Settings LoadFromDisk()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return new Settings();
                var json = File.ReadAllText(SettingsFile);
                var loaded = JsonConvert.DeserializeObject<Settings>(json);
                return loaded ?? new Settings();
            }
            catch (Exception ex)
            {
                // Corrupt settings file shouldn't kill the plugin — log + start fresh.
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Error($"[HsConquest] Failed to load settings: {ex}");
                return new Settings();
            }
        }

        public void SaveToDisk()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                File.WriteAllText(SettingsFile, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Error($"[HsConquest] Failed to save settings: {ex}");
            }
        }
    }
}
