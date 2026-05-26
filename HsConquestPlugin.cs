using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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
        // Polling timer for the post-OnGameStart "wait for classes" loop.
        // Kept as a field so a re-entrant OnGameStart (e.g. fast restart
        // after a concede) can cancel the in-flight poll before starting a
        // fresh one.
        private DispatcherTimer _classPollTimer;
        // True between OnGameStart (first fire) and OnGameEnd. HDT sometimes
        // fires OnGameStart multiple times during a single match (around
        // mulligan completion, opponent reveal, etc.) — repopulating the
        // overlay dropdowns on those re-fires wipes the user's archetype
        // selections. Gate the populate-and-show logic on this flag so each
        // match gets exactly one populate.
        private bool _gameInProgress;

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
            try
            {
                _classPollTimer?.Stop();
                _classPollTimer = null;
                _overlay?.Hide();
            }
            catch { /* HDT shutdown is best-effort */ }
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

        // Poll interval + max retries for the "wait for HDT to populate class"
        // loop. 500ms × 20 = 10s upper bound. Empirically the classes are
        // ready within 1-3 seconds of OnGameStart; the cap is just a safety
        // net so we don't poll forever on a borked game state.
        private const int ClassPollIntervalMs = 500;
        private const int ClassPollMaxAttempts = 20;

        private void OnGameStart()
        {
            // Drop re-fires: HDT will fire OnGameStart multiple times during
            // a single match (around mulligan completion etc.), and we don't
            // want to clobber the user's archetype picks each time. First
            // game-start fire takes responsibility for the whole match;
            // subsequent ones until OnGameEnd are ignored.
            if (_gameInProgress)
            {
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Info(
                    "[HsConquest] OnGameStart re-fire ignored (still in-progress).");
                return;
            }
            _gameInProgress = true;

            // HDT fires OnGameStart *before* the hero entities are placed on
            // the board — at that moment Player.OriginalClass and
            // Opponent.OriginalClass both return empty strings. So we kick
            // off a short polling loop and only show the overlay once both
            // classes are populated. (Confirmed empirically: logs showed
            // myClass='' oppClass='' at OnGameStart.)
            try
            {
                _classPollTimer?.Stop();
                _classPollTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(ClassPollIntervalMs),
                };
                int attempts = 0;
                _classPollTimer.Tick += (sender, args) =>
                {
                    attempts++;
                    try
                    {
                        var myClass  = Hearthstone_Deck_Tracker.Core.Game.Player.OriginalClass ?? "";
                        var oppClass = Hearthstone_Deck_Tracker.Core.Game.Opponent.OriginalClass ?? "";
                        var bothKnown = !string.IsNullOrEmpty(myClass) && !string.IsNullOrEmpty(oppClass);
                        var timedOut  = attempts >= ClassPollMaxAttempts;
                        if (!bothKnown && !timedOut) return; // try again next tick

                        // Stop the timer first so we don't re-enter.
                        _classPollTimer.Stop();
                        _classPollTimer = null;

                        // One-line diagnostic for the log — tells us at a
                        // glance whether the classes resolved before timeout
                        // and how many archetypes the filter found on each side.
                        var matrixState = _matrixClient.HasMatrix
                            ? $"matrix has {_matrixClient.Cached.Rows.Count} rows / {_matrixClient.Cached.Cols.Count} cols"
                            : "matrix NOT loaded";
                        var myCount  = _matrixClient.ArchetypesForClass(myClass).Count();
                        var oppCount = _matrixClient.ArchetypesForClass(oppClass).Count();
                        Hearthstone_Deck_Tracker.Utility.Logging.Log.Info(
                            $"[HsConquest] Classes ready after {attempts * ClassPollIntervalMs}ms: " +
                            $"my='{myClass}' opp='{oppClass}' {matrixState}; " +
                            $"archetypes: my={myCount} opp={oppCount} " +
                            (bothKnown ? "OK" : "TIMED OUT"));

                        _overlay.ShowForGame(_matrixClient, myClass, oppClass);
                    }
                    catch (Exception ex)
                    {
                        _classPollTimer?.Stop();
                        _classPollTimer = null;
                        Hearthstone_Deck_Tracker.Utility.Logging.Log.Error(
                            $"[HsConquest] class-poll tick failed: {ex}");
                    }
                };
                _classPollTimer.Start();
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Info(
                    "[HsConquest] OnGameStart fired — polling for hero classes...");
            }
            catch (Exception ex)
            {
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Error($"[HsConquest] OnGameStart failed: {ex}");
            }
        }

        private void OnGameEnd()
        {
            try
            {
                _classPollTimer?.Stop();
                _classPollTimer = null;
                _gameInProgress = false; // ready for next OnGameStart
                _overlay?.HideForEnd();
            }
            catch { }
        }
    }
}
