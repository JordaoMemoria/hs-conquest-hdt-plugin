using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HsConquest.Matrix;

namespace HsConquest.UI
{
    /// <summary>
    /// In-game overlay. Two dropdowns:
    ///   - "My deck": archetypes filtered to the player's class
    ///   - "Opponent's deck": archetypes filtered to the opponent's class
    /// Once both are picked, the WR for that matchup is displayed.
    ///
    /// HDT auto-detects both classes at game start and we pre-filter the
    /// dropdowns to relevant archetypes for each side. The user picks both
    /// per match — no deck-name mapping in settings required.
    /// </summary>
    public partial class MatchupOverlay : Window
    {
        private const string PlaceholderText = "— select —";
        private MatrixClient _client;

        public MatchupOverlay()
        {
            InitializeComponent();
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
            WrSubText.Text = client?.HasMatrix == true
                ? ""
                : "No matrix loaded — paste URL in plugin Settings.";

            ShowIfNotVisible();
        }

        /// <summary>Hide on game end, but don't tear down — we'll reuse for the next game.</summary>
        public void HideForEnd() => Hide();

        // ---- Internal ----

        private static void PopulateDropdown(ComboBox box, IEnumerable<string> options)
        {
            // Always start with a placeholder so the box reads "— select —"
            // until the user makes a real choice. If the class has no
            // archetypes in the matrix slice (e.g. very-low-popularity
            // class hidden by sample threshold), the placeholder is the
            // only entry — the user can't pick anything, which is
            // expected. We don't bypass-disable here because the user
            // might still want to manually type or look at it.
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
                WrText.Text    = "";
                WrSubText.Text = _client?.HasMatrix == true
                    ? "Pick both archetypes to see WR."
                    : "No matrix loaded — paste URL in plugin Settings.";
                return;
            }
            var cell = _client?.Lookup(mine, opp);
            if (cell == null || cell.Games <= 0)
            {
                WrText.Text       = "?";
                WrSubText.Text    = "no matchup data";
                WrText.Foreground = Brushes.DimGray;
                return;
            }
            var wrPct = cell.Wr * 100.0;
            WrText.Text       = $"{wrPct:0.0}%";
            WrSubText.Text    = $"{cell.Games:N0} games";
            WrText.Foreground = WrBrush(cell.Wr);
        }

        private void ShowIfNotVisible()
        {
            if (!IsVisible) Show();
            // Force topmost re-apply — WPF sometimes loses topmost on focus changes.
            Topmost = false;
            Topmost = true;
        }

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
