using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.API;
using HsConquest.Matrix;
using HsConquest.UI;

namespace HsConquest
{
    /// <summary>
    /// HDT plugin entry point. Implements <see cref="IPlugin"/> so HDT's
    /// plugin manager picks us up — drop the compiled DLL into
    /// %AppData%/HearthstoneDeckTracker/Plugins/HsConquest/ and HDT
    /// instantiates this class on startup.
    ///
    /// Flow:
    ///   - OnLoad: hydrate settings from disk, subscribe to game events.
    ///   - OnButtonPress: user clicked "Settings" → open SettingsWindow.
    ///   - OnGameStart: read opponent class, show overlay with archetype
    ///                  dropdown for that class. Overlay stays hidden
    ///                  until the user picks an opponent archetype.
    ///   - OnGameEnd: hide overlay.
    /// </summary>
    public class HsConquestPlugin : IPlugin
    {
        // === IPlugin metadata (shown in HDT's Plugin Manager) ===
        public string Name        => "HS Conquest Helper";
        public string Description => "Shows in-game WR vs the opponent's archetype using your Tab-4 matrix synced from hsconquest.netlify.app.";
        public string ButtonText  => "Settings";
        public string Author      => "hsconquest";
        public Version Version    => new Version(1, 0, 0);
        public MenuItem MenuItem  => null;

        // Plugin state — created once in OnLoad, torn down in OnUnload.
        private Settings _settings;
        private MatrixClient _matrixClient;
        private MatchupOverlay _overlay;

        public void OnLoad()
        {
            _settings     = Settings.LoadFromDisk();
            _matrixClient = new MatrixClient(_settings.SyncUrl);
            _overlay      = new MatchupOverlay();

            // Pre-fetch matrix in the background so the overlay has data ready
            // when the first game starts. Errors are swallowed — the overlay
            // shows "no data" if the fetch failed.
            _ = _matrixClient.FetchAsync();

            GameEvents.OnGameStart.Add(OnGameStart);
            GameEvents.OnGameEnd.Add(OnGameEnd);
        }

        public void OnUnload()
        {
            try { _overlay?.Hide(); } catch { /* HDT shutdown is best-effort */ }
            _overlay = null;
        }

        public void OnButtonPress()
        {
            // Pass current state into the window. Window writes back to
            // disk on save; we reload our cached state after it closes so
            // the next game-start sees the updated URL / mappings.
            var win = new SettingsWindow(_settings, _matrixClient);
            win.ShowDialog();

            _settings     = Settings.LoadFromDisk();
            _matrixClient = new MatrixClient(_settings.SyncUrl);
            _ = _matrixClient.FetchAsync();
        }

        public void OnUpdate() { /* called per frame — we don't need it */ }

        private void OnGameStart()
        {
            try
            {
                var oppClass = Hearthstone_Deck_Tracker.Core.Game.Opponent.Class ?? "";
                var myDeckName = Hearthstone_Deck_Tracker.Core.Game.Player.OriginalDeck?.Name
                                 ?? Hearthstone_Deck_Tracker.Core.Game.Player.Class
                                 ?? "";

                // Look up the user's archetype mapping for this HDT deck name.
                _settings.DeckToArchetype.TryGetValue(myDeckName, out var myArchetype);
                if (string.IsNullOrEmpty(myArchetype))
                {
                    // No mapping yet — overlay will tell the user to set one in
                    // the settings panel.
                    _overlay.ShowUnmapped(myDeckName, oppClass);
                    return;
                }

                _overlay.ShowForGame(_matrixClient, myArchetype, oppClass);
            }
            catch (Exception ex)
            {
                // Never crash HDT because of plugin code.
                Hearthstone_Deck_Tracker.Logging.Log.Error($"[HsConquest] OnGameStart failed: {ex}");
            }
        }

        private void OnGameEnd()
        {
            try { _overlay?.HideForEnd(); } catch { }
        }
    }
}
