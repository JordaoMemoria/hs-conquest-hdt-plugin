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
                // OriginalClass = the class chosen at deck-build time, not
                // whatever the current hero might be (matters for shudderwock-
                // style class swaps). Use OriginalClass for both sides so the
                // overlay's dropdowns are filtered to the correct archetype
                // pool.
                var myClass  = Hearthstone_Deck_Tracker.Core.Game.Player.OriginalClass ?? "";
                var oppClass = Hearthstone_Deck_Tracker.Core.Game.Opponent.OriginalClass ?? "";

                // Diagnostic logging — surfaces in %AppData%/HearthstoneDeckTracker/hdt_log.txt.
                // Tells us at a glance: what classes HDT reported, whether the
                // matrix is loaded, and how many archetypes the filter found
                // for each side. Empty counts = either the classes don't match
                // any matrix entries or the matrix didn't fetch.
                var matrixState = _matrixClient.HasMatrix
                    ? $"matrix has {_matrixClient.Cached.Rows.Count} rows / {_matrixClient.Cached.Cols.Count} cols"
                    : "matrix NOT loaded";
                var myCount  = System.Linq.Enumerable.Count(_matrixClient.ArchetypesForClass(myClass));
                var oppCount = System.Linq.Enumerable.Count(_matrixClient.ArchetypesForClass(oppClass));
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Info(
                    $"[HsConquest] OnGameStart myClass='{myClass}' oppClass='{oppClass}' " +
                    $"{matrixState}; archetypes: my={myCount} opp={oppCount}");

                // No deck-mapping step: the user picks both their own
                // archetype and the opponent's from the overlay dropdowns
                // in-game. The plugin just supplies the matrix and the
                // class filters.
                _overlay.ShowForGame(_matrixClient, myClass, oppClass);
            }
            catch (Exception ex)
            {
                // Never crash HDT because of plugin code.
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Error($"[HsConquest] OnGameStart failed: {ex}");
            }
        }

        private void OnGameEnd()
        {
            try { _overlay?.HideForEnd(); } catch { }
        }
    }
}
