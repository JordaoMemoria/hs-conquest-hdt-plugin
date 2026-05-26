using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HsConquest.Matrix;

namespace HsConquest.UI
{
    /// <summary>
    /// Small always-on-top overlay shown during a game. Displays the
    /// opponent's class + a dropdown of archetypes for that class; when
    /// the user picks one, shows the matchup WR for their own deck vs
    /// that archetype.
    ///
    /// Three visible states:
    ///   - "vs &lt;Class&gt;" + dropdown + (empty WR area) — waiting for user pick.
    ///   - "vs &lt;Class&gt;" + dropdown + "XX.X%" + "N games" — user picked.
    ///   - "Set up your deck mapping" — the user's HDT deck has no
    ///     archetype mapping in settings yet.
    /// </summary>
    public partial class MatchupOverlay : Window
    {
        private MatrixClient _client;
        private string _myArchetype;

        public MatchupOverlay()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Configure the overlay for a fresh game. Populates the dropdown
        /// with the opponent class's archetypes; WR stays blank until the
        /// user picks one.
        /// </summary>
        public void ShowForGame(MatrixClient client, string myArchetype, string opponentClass)
        {
            _client = client;
            _myArchetype = myArchetype;

            HeaderText.Text   = $"vs {Capitalize(opponentClass)} — pick archetype";
            WrText.Text       = "";
            WrSubText.Text    = "";
            FooterText.Text   = $"My deck: {myArchetype}";

            // Populate dropdown.
            var archetypes = (client?.ArchetypesForClass(opponentClass) ?? new List<string>()).ToList();
            // Insert a placeholder so the dropdown starts unselected.
            archetypes.Insert(0, "— select —");
            ArchetypeBox.ItemsSource = archetypes;
            ArchetypeBox.SelectedIndex = 0;

            if (archetypes.Count <= 1)
            {
                HeaderText.Text = $"vs {Capitalize(opponentClass)}";
                WrSubText.Text = "No matrix data yet. Reload in settings.";
            }

            ShowIfNotVisible();
        }

        /// <summary>
        /// Render an "unmapped deck" state: HDT picked up the user's deck
        /// but they haven't tagged it with an archetype yet.
        /// </summary>
        public void ShowUnmapped(string deckName, string opponentClass)
        {
            HeaderText.Text   = $"vs {Capitalize(opponentClass)}";
            ArchetypeBox.ItemsSource = new[] { "(no deck mapping)" };
            ArchetypeBox.SelectedIndex = 0;
            ArchetypeBox.IsEnabled = false;
            WrText.Text       = "?";
            WrSubText.Text    = $"Tag '{deckName}' with an archetype";
            FooterText.Text   = "Open Settings in HDT plugin manager";
            ShowIfNotVisible();
        }

        /// <summary>Hide on game end, but don't tear down — we'll reuse for the next game.</summary>
        public void HideForEnd() => Hide();

        // ---- Internal ----

        private void OnArchetypeChanged(object sender, SelectionChangedEventArgs e)
        {
            var opp = ArchetypeBox.SelectedItem as string;
            if (string.IsNullOrEmpty(opp) || opp == "— select —")
            {
                WrText.Text    = "";
                WrSubText.Text = "";
                return;
            }
            var cell = _client?.Lookup(_myArchetype, opp);
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
