using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Hearthstone_Deck_Tracker;
using HsConquest.Matrix;

namespace HsConquest.UI
{
    /// <summary>
    /// Settings dialog opened when the user clicks "Settings" on the
    /// plugin's row in HDT's Plugin Manager.
    ///
    /// Two responsibilities:
    ///   1. Edit the sync URL and reload the matrix.
    ///   2. For each HDT deck the user has, let them pick the matching
    ///      archetype name from the matrix. This is the "deck → archetype"
    ///      mapping that drives the in-game overlay's "my deck" side.
    ///
    /// The window doesn't mutate the live Settings object directly — it
    /// works on a copy, then writes back on Save. Cancel just closes the
    /// window without persisting anything.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly Settings _settingsCopy;
        private readonly MatrixClient _matrixClient;
        private readonly List<DeckRowVm> _rows = new List<DeckRowVm>();

        public SettingsWindow(Settings settings, MatrixClient matrixClient)
        {
            InitializeComponent();
            _settingsCopy = new Settings
            {
                SyncUrl = settings.SyncUrl,
                DeckToArchetype = new Dictionary<string, string>(
                    settings.DeckToArchetype,
                    System.StringComparer.OrdinalIgnoreCase),
            };
            _matrixClient = matrixClient;
            UrlBox.Text = _settingsCopy.SyncUrl;
            ReloadDeckList();
        }

        // ---- Reload matrix button ----

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            _settingsCopy.SyncUrl = UrlBox.Text.Trim();
            ReloadButton.IsEnabled = false;
            ReloadStatus.Text = "Loading…";
            // Recreate the client so the new URL is in effect.
            var client = new MatrixClient(_settingsCopy.SyncUrl);
            var matrix = await client.FetchAsync(force: true);
            ReloadButton.IsEnabled = true;
            if (matrix == null)
            {
                ReloadStatus.Text = "Failed — check URL";
                ReloadStatus.Foreground = System.Windows.Media.Brushes.IndianRed;
            }
            else
            {
                ReloadStatus.Text = $"OK — {matrix.Rows.Count} archetypes";
                ReloadStatus.Foreground = System.Windows.Media.Brushes.SeaGreen;
                // Swap the live client's cached matrix so the dropdown options refresh.
                await _matrixClient.FetchAsync(force: true);
                ReloadDeckList();
            }
        }

        // ---- Save / Cancel ----

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            _settingsCopy.SyncUrl = UrlBox.Text.Trim();
            _settingsCopy.DeckToArchetype.Clear();
            foreach (var row in _rows)
            {
                if (!string.IsNullOrEmpty(row.SelectedArchetype))
                    _settingsCopy.DeckToArchetype[row.DeckName] = row.SelectedArchetype;
            }
            _settingsCopy.SaveToDisk();
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ---- Building the deck list ----

        private void ReloadDeckList()
        {
            _rows.Clear();
            // HDT decks come from DeckList.Instance.Decks. Each Deck has a
            // .Name (user-chosen string) and .Class (HS class enum-as-string).
            // We list all of them — even ones without an archetype yet — so
            // the user can fill in the mapping.
            var allArchetypes = _matrixClient.AllArchetypes().ToList();
            // Add an empty option so the user can clear a mapping.
            allArchetypes.Insert(0, "");

            var decks = DeckList.Instance.Decks
                .OrderBy(d => d.Class ?? "")
                .ThenBy(d => d.Name ?? "")
                .ToList();

            foreach (var deck in decks)
            {
                var deckName = deck.Name ?? "(unnamed deck)";
                _settingsCopy.DeckToArchetype.TryGetValue(deckName, out var current);
                _rows.Add(new DeckRowVm
                {
                    DeckName = deckName,
                    ArchetypeOptions = allArchetypes,
                    SelectedArchetype = current ?? "",
                });
            }

            DeckList.ItemsSource = _rows;
        }

        /// <summary>One row in the deck-mapping list, two-way bound to a ComboBox.</summary>
        private class DeckRowVm : INotifyPropertyChanged
        {
            public string DeckName { get; set; }
            public List<string> ArchetypeOptions { get; set; }

            private string _selectedArchetype;
            public string SelectedArchetype
            {
                get => _selectedArchetype;
                set
                {
                    if (_selectedArchetype == value) return;
                    _selectedArchetype = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedArchetype)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
