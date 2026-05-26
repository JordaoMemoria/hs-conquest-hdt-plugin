using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HsConquest.Matrix;

namespace HsConquest.UI
{
    /// <summary>
    /// In-game overlay. Two dropdowns (my class / opp class) → WR display.
    ///
    /// User controls:
    ///   - Drag: click-and-drag from any non-interactive area to reposition.
    ///   - Minimize button: collapse to just the title bar + compact WR.
    ///   - Close button: hide the overlay (reappears at next game start).
    ///
    /// Position and collapsed state persist to settings.json between
    /// sessions, so the user only positions the overlay once.
    /// </summary>
    public partial class MatchupOverlay : Window
    {
        private const string PlaceholderText = "— select —";
        private MatrixClient _client;
        private bool _collapsed;

        public MatchupOverlay()
        {
            InitializeComponent();
            // Restore saved position + collapsed state on construction. If
            // the user hasn't dragged yet, Left/Top stay at the XAML default
            // (40, 120). OverlayCollapsed defaults to false → body visible.
            var settings = Settings.LoadFromDisk();
            if (settings.OverlayLeft.HasValue) Left = settings.OverlayLeft.Value;
            if (settings.OverlayTop.HasValue)  Top  = settings.OverlayTop.Value;
            if (settings.OverlayCollapsed) ApplyCollapsedState(true);
        }

        /// <summary>
        /// Configure the overlay for a fresh game. Populates both dropdowns
        /// with archetypes for each side's detected class; WR stays blank
        /// until the user picks an archetype on each side.
        /// </summary>
        public void ShowForGame(MatrixClient client, string playerClass, string opponentClass)
        {
            _client = client;
            HeaderText.Text = $"{Capitalize(playerClass)} vs {Capitalize(opponentClass)}";

            PopulateDropdown(MyArchetypeBox,  client?.ArchetypesForClass(playerClass));
            PopulateDropdown(OppArchetypeBox, client?.ArchetypesForClass(opponentClass));

            WrText.Text    = "";
            CompactWrText.Text = "";
            WrSubText.Text = client?.HasMatrix == true
                ? ""
                : "No matrix loaded — paste URL in plugin Settings.";

            ShowIfNotVisible();
        }

        /// <summary>Hide on game end, but don't tear down — we'll reuse for the next game.</summary>
        public void HideForEnd() => Hide();

        // ---- Title-bar controls: drag / minimize / close ----

        private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Drag the whole window from any non-interactive surface. Clicks
            // on ComboBoxes / Buttons mark themselves as Handled and never
            // reach this handler, so dragging doesn't interfere with the
            // dropdowns or the min/close buttons.
            if (e.ChangedButton != MouseButton.Left) return;
            try
            {
                DragMove();           // blocking until mouse-up
                SaveOverlayPosition();
            }
            catch { /* DragMove throws if mouse state weirdness; ignore */ }
        }

        private void OnMinimizeClick(object sender, RoutedEventArgs e)
        {
            ApplyCollapsedState(!_collapsed);
            PersistCollapsedState();
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            // Hide rather than Close — closing destroys the WPF window
            // and we want to reuse it for the next game. Reappears via
            // ShowForGame on the next OnGameStart.
            Hide();
        }

        // ---- Internal ----

        private void ApplyCollapsedState(bool collapsed)
        {
            _collapsed = collapsed;
            BodyPanel.Visibility    = collapsed ? Visibility.Collapsed : Visibility.Visible;
            CompactWrText.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
            // Use a unicode "expand" glyph when collapsed so the user knows
            // clicking it re-expands. "—" minimize → "▢" expand.
            MinimizeButton.Content   = collapsed ? "▢" : "—";
            MinimizeButton.ToolTip   = collapsed ? "Expand" : "Minimize";
        }

        private static void PopulateDropdown(ComboBox box, IEnumerable<string> options)
        {
            var list = new List<string> { PlaceholderText };
            if (options != null) list.AddRange(options);
            box.ItemsSource    = list;
            box.SelectedIndex  = 0;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var mine = MyArchetypeBox.SelectedItem as string;
            var opp  = OppArchetypeBox.SelectedItem as string;
            var minePicked = !string.IsNullOrEmpty(mine) && mine != PlaceholderText;
            var oppPicked  = !string.IsNullOrEmpty(opp)  && opp  != PlaceholderText;

            if (!minePicked || !oppPicked)
            {
                WrText.Text        = "";
                CompactWrText.Text = "";
                WrSubText.Text     = _client?.HasMatrix == true
                    ? "Pick both archetypes to see WR."
                    : "No matrix loaded — paste URL in plugin Settings.";
                return;
            }
            var cell = _client?.Lookup(mine, opp);
            if (cell == null || cell.Games <= 0)
            {
                WrText.Text          = "?";
                CompactWrText.Text   = "?";
                WrSubText.Text       = "no matchup data";
                WrText.Foreground    = Brushes.DimGray;
                CompactWrText.Foreground = Brushes.DimGray;
                return;
            }
            var wrPct = cell.Wr * 100.0;
            var brush = WrBrush(cell.Wr);
            WrText.Text          = $"{wrPct:0.0}%";
            CompactWrText.Text   = $"{wrPct:0.0}%";
            WrSubText.Text       = $"{cell.Games:N0} games";
            WrText.Foreground    = brush;
            CompactWrText.Foreground = brush;
        }

        private void ShowIfNotVisible()
        {
            if (!IsVisible) Show();
            // Force topmost re-apply — WPF sometimes loses topmost on focus changes.
            Topmost = false;
            Topmost = true;
        }

        // ---- Settings persistence ----

        private void SaveOverlayPosition()
        {
            // Round-trip through disk so we don't clobber unrelated fields
            // (SyncUrl, OverlayCollapsed). Same pattern as SettingsWindow.
            try
            {
                var s = Settings.LoadFromDisk();
                s.OverlayLeft = Left;
                s.OverlayTop  = Top;
                s.SaveToDisk();
            }
            catch { /* settings persistence is best-effort */ }
        }

        private void PersistCollapsedState()
        {
            try
            {
                var s = Settings.LoadFromDisk();
                s.OverlayCollapsed = _collapsed;
                s.SaveToDisk();
            }
            catch { }
        }

        // ---- Misc ----

        // Maps WR [0,1] to a red→yellow→green color, matching the web app's wrColor scale.
        private static Brush WrBrush(double wr)
        {
            if (wr < 0.45) return new SolidColorBrush(Color.FromRgb(0xC4, 0x1E, 0x3A)); // red
            if (wr < 0.55) return new SolidColorBrush(Color.FromRgb(0xB8, 0x90, 0x2A)); // amber
            return new SolidColorBrush(Color.FromRgb(0x1F, 0x73, 0x3D));                // green
        }

        private static string Capitalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "?";
            // HDT gives e.g. "MAGE" or "DEATHKNIGHT". We want "Mage" / "Death Knight".
            var s = raw.ToLowerInvariant();
            switch (s)
            {
                case "deathknight": return "Death Knight";
                case "demonhunter": return "Demon Hunter";
                default:            return char.ToUpperInvariant(s[0]) + s.Substring(1);
            }
        }
    }
}
