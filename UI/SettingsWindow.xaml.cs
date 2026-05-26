using System.Windows;
using HsConquest.Matrix;

namespace HsConquest.UI
{
    /// <summary>
    /// Settings dialog: just the sync URL + Reload button. No per-deck
    /// mapping anymore — the in-game overlay has dropdowns for both
    /// sides, so the user picks both archetypes per match. Way less
    /// upfront setup.
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly Settings _settingsCopy;
        private readonly MatrixClient _matrixClient;

        public SettingsWindow(Settings settings, MatrixClient matrixClient)
        {
            InitializeComponent();
            // Work on a copy so Cancel discards changes cleanly.
            _settingsCopy = new Settings { SyncUrl = settings.SyncUrl };
            _matrixClient = matrixClient;
            UrlBox.Text = _settingsCopy.SyncUrl;
        }

        private async void OnReloadClick(object sender, RoutedEventArgs e)
        {
            _settingsCopy.SyncUrl = UrlBox.Text.Trim();
            ReloadButton.IsEnabled = false;
            ReloadStatus.Text = "Loading…";
            // Try the new URL on a temporary client first so we don't trash
            // the live cache on a failed reload.
            var probe = new MatrixClient(_settingsCopy.SyncUrl);
            var matrix = await probe.FetchAsync(force: true);
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
                // Refresh the live client's cache so in-game lookups see the
                // new matrix immediately.
                await _matrixClient.FetchAsync(force: true);
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            // Load fresh from disk so we don't clobber fields managed
            // by the overlay (OverlayLeft / OverlayTop / OverlayCollapsed)
            // — this window only edits SyncUrl.
            var current = Settings.LoadFromDisk();
            current.SyncUrl = UrlBox.Text.Trim();
            current.SaveToDisk();
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
