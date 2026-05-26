using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HsConquest.Matrix
{
    /// <summary>
    /// HTTP client + in-memory cache for the Tab-4 matrix JSON.
    ///
    /// Caching strategy: hold the parsed matrix for 10 minutes after the
    /// last fetch (matches the server-side `Cache-Control: public,
    /// max-age=600` we send from the Netlify Function). The plugin
    /// pre-fetches on plugin load and on settings save; per-game-start
    /// lookups hit the cache without going over the network.
    ///
    /// Lookup is positional-free: each cell carries both row + col names,
    /// so we just scan the cells list. ~2000 cells × maybe 20 lookups per
    /// session = trivial — no need for a precomputed index.
    /// </summary>
    public class MatrixClient
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        private readonly string _url;
        private MatrixTable _cached;
        private DateTime _cachedAtUtc = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public MatrixClient(string url) { _url = url ?? ""; }

        /// <summary>True if a fresh matrix is in memory and ready for lookups.</summary>
        public bool HasMatrix => _cached != null;

        public MatrixTable Cached => _cached;

        /// <summary>
        /// Fetch the matrix from the sync URL. Returns the parsed table or
        /// null if the URL is empty / the request fails. Caches the result
        /// for <see cref="CacheTtl"/>. Pass <c>force=true</c> to bypass the
        /// cache (e.g. when the user clicks "Reload" in settings).
        /// </summary>
        public async Task<MatrixTable> FetchAsync(bool force = false)
        {
            if (!force && _cached != null && (DateTime.UtcNow - _cachedAtUtc) < CacheTtl)
                return _cached;

            if (string.IsNullOrWhiteSpace(_url))
                return null;

            try
            {
                var json = await _http.GetStringAsync(_url).ConfigureAwait(false);
                var envelope = JsonConvert.DeserializeObject<MatrixEnvelope>(json);
                if (envelope?.Table1 == null) return null;
                _cached = envelope.Table1;
                _cachedAtUtc = DateTime.UtcNow;
                return _cached;
            }
            catch (Exception ex)
            {
                // Log but don't bubble — overlay falls back to "no data".
                Hearthstone_Deck_Tracker.Utility.Logging.Log.Error($"[HsConquest] Matrix fetch failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Return the cell for (myArchetype, oppArchetype), or null if no
        /// cell exists (e.g. the matchup wasn't in the captured sample).
        /// </summary>
        public MatrixCell Lookup(string myArchetype, string oppArchetype)
        {
            if (_cached == null) return null;
            if (string.IsNullOrEmpty(myArchetype) || string.IsNullOrEmpty(oppArchetype)) return null;

            foreach (var cell in _cached.Cells)
            {
                if (string.Equals(cell.RowName, myArchetype, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(cell.ColName, oppArchetype, StringComparison.OrdinalIgnoreCase))
                    return cell;
            }
            return null;
        }

        /// <summary>
        /// Archetype names in the matrix whose class matches the given HS
        /// class. Used by the in-game overlay to populate its "select
        /// opponent archetype" dropdown — once HDT tells us the opponent's
        /// class, we only show the matching archetypes.
        /// </summary>
        public IEnumerable<string> ArchetypesForClass(string hsClass)
        {
            if (_cached == null) return Enumerable.Empty<string>();
            var normalized = ClassNormalizer.Normalize(hsClass);
            return _cached.Cols
                .Where(a => string.Equals(ClassNormalizer.Normalize(a.Cls), normalized, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>All archetype names in the matrix (used by the settings panel to populate the user-deck dropdown).</summary>
        public IEnumerable<string> AllArchetypes()
        {
            if (_cached == null) return Enumerable.Empty<string>();
            return _cached.Rows
                .Select(r => r.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Bridges HDT's class names ("WARRIOR", "Death Knight") to the
    /// matrix's class slugs ("warrior", "deathknight"). Lowercase + strip
    /// whitespace + a few special cases for two-word classes.
    /// </summary>
    internal static class ClassNormalizer
    {
        /// <summary>
        /// Map any plausible HDT class representation to the lowercase
        /// one-word slug used in the matrix ("deathknight",
        /// "demonhunter", etc.). Handles uppercase ("MAGE"), spaces
        /// ("Death Knight"), underscores ("DEATH_KNIGHT"), hyphens
        /// ("death-knight"), and any combination thereof.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            // Strip all non-letters then lowercase — collapses MAGE, Mage,
            // Death Knight, DEATH_KNIGHT, death-knight all into the same
            // slug. Cheap and robust against future HDT formatting changes.
            var sb = new System.Text.StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (char.IsLetter(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
