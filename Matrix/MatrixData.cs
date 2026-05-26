using System.Collections.Generic;
using Newtonsoft.Json;

namespace HsConquest.Matrix
{
    /// <summary>
    /// POCO mirror of the JSON shape served by /.netlify/functions/matrix?id=...
    /// — the schema the web app POSTs to its sync endpoint.
    ///
    /// The TS side uses:
    ///   { version, syncedAt, lastAccessedAt, table1: { rows, cols, cells, popularity, importedAt, filters? } }
    ///
    /// Only Table 1 is shipped (Table 2 stays in the browser); the plugin
    /// just looks up WR(myDeck, opponent) for individual cells.
    /// </summary>
    public class MatrixEnvelope
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("syncedAt")]
        public string SyncedAt { get; set; }

        [JsonProperty("lastAccessedAt")]
        public string LastAccessedAt { get; set; }

        [JsonProperty("table1")]
        public MatrixTable Table1 { get; set; }
    }

    public class MatrixTable
    {
        [JsonProperty("rows")]
        public List<MatrixArchetype> Rows { get; set; } = new List<MatrixArchetype>();

        [JsonProperty("cols")]
        public List<MatrixArchetype> Cols { get; set; } = new List<MatrixArchetype>();

        [JsonProperty("cells")]
        public List<MatrixCell> Cells { get; set; } = new List<MatrixCell>();

        // Popularity is sent as { archetypeName: fraction }. The plugin
        // doesn't use it for lookups, but we deserialize it anyway in case
        // we want to surface it later (e.g. in the settings dropdown).
        [JsonProperty("popularity")]
        public Dictionary<string, double> Popularity { get; set; } = new Dictionary<string, double>();

        [JsonProperty("importedAt")]
        public long ImportedAt { get; set; }

        [JsonProperty("filters")]
        public MatrixFilters Filters { get; set; }
    }

    public class MatrixArchetype
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        // HS class slug ("warrior", "deathknight", "demonhunter", etc) —
        // matches HDT's class string after a ToLowerInvariant + "death knight"
        // → "deathknight" normalization. See ClassNormalizer.Normalize.
        [JsonProperty("cls")]
        public string Cls { get; set; }

        [JsonProperty("id")]
        public int? Id { get; set; }
    }

    public class MatrixCell
    {
        [JsonProperty("rowName")]
        public string RowName { get; set; }

        [JsonProperty("colName")]
        public string ColName { get; set; }

        /// <summary>Win rate from the row deck's perspective, in [0,1].</summary>
        [JsonProperty("wr")]
        public double Wr { get; set; }

        /// <summary>Sample size for this cell.</summary>
        [JsonProperty("games")]
        public int Games { get; set; }
    }

    public class MatrixFilters
    {
        [JsonProperty("rankRange")]    public string RankRange { get; set; }
        [JsonProperty("timeFrame")]    public string TimeFrame { get; set; }
        [JsonProperty("region")]       public string Region { get; set; }
        [JsonProperty("format")]       public string Format { get; set; }
        [JsonProperty("gameType")]     public string GameType { get; set; }
        [JsonProperty("gameMode")]     public string GameMode { get; set; }
        [JsonProperty("contributors")] public string Contributors { get; set; }
        [JsonProperty("totalMatches")] public string TotalMatches { get; set; }
        [JsonProperty("url")]          public string Url { get; set; }
        [JsonProperty("capturedAt")]   public long? CapturedAt { get; set; }
    }
}
