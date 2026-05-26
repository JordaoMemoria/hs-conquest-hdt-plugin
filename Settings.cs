using System;
using System.IO;
using Newtonsoft.Json;

namespace HsConquest
{
    /// <summary>
    /// Plugin configuration persisted to
    /// %AppData%/HearthstoneDeckTracker/Plugins/HsConquest/settings.json.
    ///
    /// Single field for V1: the sync URL the user pasted from Tab 4 of
    /// hsconquest.netlify.app. The plugin GETs this URL to pull the
    /// latest matchup matrix. The user picks both their own and the
    /// opponent's archetype in-game from the overlay, so we don't need
    /// any per-deck mapping here.
    /// </summary>
    public class Settings
    {
        public string SyncUrl { get; set; } = "";

        // Overlay window state — persisted so the user's dragged position
        // and collapsed/expanded preference survive HDT restarts. Nullable
        // doubles for position so an unset value (first run) defers to the
        // XAML default (Left=40, Top=120).
        public double? OverlayLeft { get; set; }
        public double? OverlayTop { get; set; }
        public bool OverlayCollapsed { get; set; }

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
