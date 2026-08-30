using DaxStudio.Common;
using Newtonsoft.Json.Linq;
using Parquet;
using Parquet.Schema;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DaxStudio.Core.DeltaAnalyzer
{
    /// <summary>
    /// Represents a OneLake DFS location split into the filesystem (workspace) root and the
    /// directory (relative path) below it, which is the form required by the ADLS Gen2 list API.
    /// </summary>
    public class OneLakeLocation
    {
        public string FilesystemUrl { get; set; }

        /// <summary>
        /// The directory below the filesystem root, in <b>decoded</b> form (e.g. a table called
        /// <c>Currency Exchange</c> appears with a real space, not <c>%20</c>). Callers that build a URL
        /// from it must escape it - <see cref="BuildFileUrl"/> does, and the DFS list API takes it as a
        /// query string value which is escaped by <c>OneLakeHttpClient</c>.
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// Parses a full OneLake DFS URL such as
        /// <c>https://onelake.dfs.fabric.microsoft.com/{workspace}/{lakehouse}.Lakehouse/Tables/{schema}/{table}</c>
        /// into its filesystem root (<c>https://host/{workspace}</c>) and directory
        /// (<c>{lakehouse}.Lakehouse/Tables/{schema}/{table}</c>).
        /// </summary>
        public static OneLakeLocation Parse(string dfsUrl)
        {
            if (string.IsNullOrWhiteSpace(dfsUrl)) throw new ArgumentException("OneLake path is empty", nameof(dfsUrl));
            if (!Uri.TryCreate(dfsUrl.TrimEnd('/'), UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException($"'{dfsUrl}' is not a valid OneLake URL.", nameof(dfsUrl));
            }
            // AbsolutePath starts with '/'; first segment is the filesystem (workspace), the rest is the directory.
            var path = uri.AbsolutePath.TrimStart('/');
            var firstSlash = path.IndexOf('/');
            string filesystem;
            string directory;
            if (firstSlash < 0)
            {
                filesystem = path;
                directory = string.Empty;
            }
            else
            {
                filesystem = path.Substring(0, firstSlash);
                directory = path.Substring(firstSlash + 1);
            }
            return new OneLakeLocation
            {
                // The filesystem segment goes straight into a URL, so it stays percent-encoded.
                FilesystemUrl = $"{uri.Scheme}://{uri.Host}/{filesystem}",
                Directory = DecodeSegments(directory)
            };
        }

        /// <summary>
        /// Percent-decodes each segment of a path individually, so an encoded separator (<c>%2F</c>)
        /// inside a name is not turned into a real path separator.
        /// </summary>
        private static string DecodeSegments(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return string.Join("/", path.Split('/').Select(Unescape));
        }

        private static string Unescape(string value)
        {
            try { return Uri.UnescapeDataString(value); }
            catch { return value; }
        }

        /// <summary>
        /// Builds the full file URL for a <b>decoded</b> path relative to the filesystem root. Each
        /// segment is escaped individually so names containing spaces or other reserved characters
        /// produce a valid URL (and the separators are preserved).
        /// </summary>
        public string BuildFileUrl(string relativePath)
        {
            var relative = (relativePath ?? string.Empty).TrimStart('/');
            var escaped = string.Join("/", relative.Split('/').Select(Uri.EscapeDataString));
            return $"{FilesystemUrl.TrimEnd('/')}/{escaped}";
        }
    }

    /// <summary>Information about a single active Delta data (parquet) file.</summary>
    public class DeltaAddFile
    {
        public string Path { get; set; }
        public long Size { get; set; }
        public long? NumRecords { get; set; }

        /// <summary>True when this file has an associated deletion vector (soft-deleted rows).</summary>
        public bool HasDeletionVector { get; set; }

        /// <summary>Number of rows logically deleted by this file's deletion vector (0 when none).</summary>
        public long DeletionVectorCardinality { get; set; }
    }

    /// <summary>Aggregated result of parsing a Delta transaction log.</summary>
    public class DeltaLogResult
    {
        public List<DeltaAddFile> ActiveFiles { get; } = new List<DeltaAddFile>();
        public string PartitionColumns { get; set; }
        public bool LiquidClusteringEnabled { get; set; }
        public string ClusteringColumns { get; set; }
        public string Error { get; set; }

        private long? _lastModifiedMillis;

        /// <summary>
        /// Records a candidate "last modified" timestamp (Unix epoch milliseconds) observed while
        /// replaying the log - the newest of the commit <c>timestamp</c> values and the per-file
        /// <c>add.modificationTime</c> values wins. Non-positive / null values are ignored.
        /// </summary>
        public void TrackModified(long? epochMillis)
        {
            if (!epochMillis.HasValue || epochMillis.Value <= 0) return;
            if (!_lastModifiedMillis.HasValue || epochMillis.Value > _lastModifiedMillis.Value)
                _lastModifiedMillis = epochMillis.Value;
        }

        /// <summary>The time the table's data was last modified, from the delta log. Null when unknown.</summary>
        public DateTimeOffset? LastModifiedUtc => _lastModifiedMillis.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(_lastModifiedMillis.Value)
            : (DateTimeOffset?)null;

        /// <summary>
        /// Maps a Delta column-mapping physical name (a GUID for Fabric Direct Lake tables) to its
        /// logical column name. Populated from the table schema when column mapping is enabled; empty
        /// otherwise. Used to translate the GUID column names stored in the parquet files back to their
        /// friendly names.
        /// </summary>
        public Dictionary<string, string> PhysicalToLogicalColumn { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Sum of numRecords across active files where the delta stats supplied a value.</summary>
        public long RowCountFromStats => ActiveFiles.Sum(f => f.NumRecords ?? 0);

        public bool AllFilesHaveRowCount => ActiveFiles.All(f => f.NumRecords.HasValue);

        /// <summary>True when any active file carries a deletion vector.</summary>
        public bool HasDeletionVectors => ActiveFiles.Any(f => f.HasDeletionVector);

        /// <summary>Number of active files that carry a deletion vector.</summary>
        public long DeletionVectorFileCount => ActiveFiles.Count(f => f.HasDeletionVector);

        /// <summary>Total number of rows logically deleted by deletion vectors across active files.</summary>
        public long DeletionVectorRowCount => ActiveFiles.Sum(f => f.DeletionVectorCardinality);

        /// <summary>
        /// Translates a parquet physical column name to its logical name using the column-mapping map.
        /// Tries an exact match, then with/without a <c>col-</c> prefix. Falls back to the input when
        /// no mapping is available (e.g. column mapping disabled).
        /// </summary>
        public string TranslateColumnName(string physicalName)
        {
            if (string.IsNullOrEmpty(physicalName) || PhysicalToLogicalColumn.Count == 0) return physicalName;
            if (PhysicalToLogicalColumn.TryGetValue(physicalName, out var logical)) return logical;
            if (PhysicalToLogicalColumn.TryGetValue("col-" + physicalName, out logical)) return logical;
            if (physicalName.StartsWith("col-", StringComparison.OrdinalIgnoreCase)
                && PhysicalToLogicalColumn.TryGetValue(physicalName.Substring(4), out logical)) return logical;
            return physicalName;
        }
    }

    /// <summary>
    /// Reads and interprets a Delta table's <c>_delta_log</c> (checkpoint + commit files) directly from
    /// OneLake. Everything is best-effort and defensive: a parsing failure on any single commit or the
    /// checkpoint is logged and skipped so a partial result can still be returned.
    /// </summary>
    public class DeltaLogReader
    {
        private static readonly Regex CommitFileRegex = new Regex(@"(\d{20})\.json$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex CheckpointFileRegex = new Regex(@"(\d{20})\.checkpoint.*\.parquet$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly OneLakeHttpClient _client;

        public DeltaLogReader(OneLakeHttpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<DeltaLogResult> ReadAsync(string tableDfsUrl, CancellationToken ct)
        {
            var result = new DeltaLogResult();
            var location = OneLakeLocation.Parse(tableDfsUrl);
            var deltaLogDir = $"{location.Directory.TrimEnd('/')}/_delta_log";

            List<OneLakePathItem> logFiles;
            try
            {
                logFiles = await _client.ListPathsAsync(location.FilesystemUrl, deltaLogDir, false, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaLogReader), nameof(ReadAsync), $"Unable to list _delta_log for {tableDfsUrl}");
                result.Error = $"Unable to read _delta_log: {ex.Message}";
                return result;
            }

            // Active file state keyed by (url-decoded) path.
            var activeFiles = new Dictionary<string, DeltaAddFile>(StringComparer.Ordinal);
            long checkpointVersion = -1;

            // 1. Read _last_checkpoint (if present) to find the checkpoint version.
            var lastCheckpoint = logFiles.FirstOrDefault(f => f.Name != null && f.Name.EndsWith("_last_checkpoint", StringComparison.OrdinalIgnoreCase));
            if (lastCheckpoint != null)
            {
                try
                {
                    var text = await _client.ReadTextAsync(location.BuildFileUrl(lastCheckpoint.Name), ct).ConfigureAwait(false);
                    var obj = JObject.Parse(text);
                    checkpointVersion = obj.Value<long?>("version") ?? -1;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaLogReader), nameof(ReadAsync), "Unable to parse _last_checkpoint, falling back to full commit replay");
                    checkpointVersion = -1;
                }
            }

            // 2. Read the checkpoint parquet for that version.
            if (checkpointVersion >= 0)
            {
                var checkpointParts = logFiles
                    .Where(f => f.Name != null && CheckpointFileRegex.IsMatch(f.Name) && ExtractVersion(CheckpointFileRegex, f.Name) == checkpointVersion)
                    .ToList();

                foreach (var part in checkpointParts)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        await ReadCheckpointAsync(location.BuildFileUrl(part.Name), activeFiles, result, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaLogReader), nameof(ReadAsync), $"Error reading checkpoint {part.Name}");
                        result.Error = AppendError(result.Error, $"checkpoint parse error: {ex.Message}");
                    }
                }
            }

            // 3. Replay commit json files with version > checkpointVersion (ascending).
            var commits = logFiles
                .Where(f => f.Name != null && CommitFileRegex.IsMatch(f.Name))
                .Select(f => new { File = f, Version = ExtractVersion(CommitFileRegex, f.Name) })
                .Where(x => x.Version > checkpointVersion)
                .OrderBy(x => x.Version)
                .ToList();

            foreach (var commit in commits)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var text = await _client.ReadTextAsync(location.BuildFileUrl(commit.File.Name), ct).ConfigureAwait(false);
                    ApplyCommitJson(text, activeFiles, result);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaLogReader), nameof(ReadAsync), $"Error reading commit {commit.File.Name}");
                    result.Error = AppendError(result.Error, $"commit {commit.Version} parse error: {ex.Message}");
                }
            }

            result.ActiveFiles.AddRange(activeFiles.Values);
            return result;
        }

        private static long ExtractVersion(Regex regex, string name)
        {
            var m = regex.Match(name);
            if (m.Success && long.TryParse(m.Groups[1].Value, out var v)) return v;
            return -1;
        }

        /// <summary>Applies the actions in one commit json file (one JSON object per line).</summary>
        private void ApplyCommitJson(string text, Dictionary<string, DeltaAddFile> activeFiles, DeltaLogResult result)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                JObject action;
                try { action = JObject.Parse(line); }
                catch { continue; }

                var add = action["add"] as JObject;
                if (add != null)
                {
                    var path = DecodePath(add.Value<string>("path"));
                    if (!string.IsNullOrEmpty(path))
                    {
                        var dv = add["deletionVector"] as JObject;
                        activeFiles[path] = new DeltaAddFile
                        {
                            Path = path,
                            Size = add.Value<long?>("size") ?? 0,
                            NumRecords = ExtractNumRecords(add.Value<string>("stats")),
                            HasDeletionVector = dv != null,
                            DeletionVectorCardinality = dv?.Value<long?>("cardinality") ?? 0
                        };
                        result.TrackModified(add.Value<long?>("modificationTime"));
                    }
                }

                var commitInfo = action["commitInfo"] as JObject;
                if (commitInfo != null) result.TrackModified(commitInfo.Value<long?>("timestamp"));

                var remove = action["remove"] as JObject;
                if (remove != null)
                {
                    var path = DecodePath(remove.Value<string>("path"));
                    if (!string.IsNullOrEmpty(path)) activeFiles.Remove(path);
                }

                var metaData = action["metaData"] as JObject;
                if (metaData != null) ApplyMetaData(metaData, result);

                var protocol = action["protocol"] as JObject;
                if (protocol != null) ApplyProtocol(protocol, result);

                var domainMetadata = action["domainMetadata"] as JObject;
                if (domainMetadata != null) ApplyDomainMetadata(domainMetadata, result);
            }
        }

        private void ApplyMetaData(JObject metaData, DeltaLogResult result)
        {
            // Parse the table schema first so column-mapping physical names can be translated.
            var schemaString = metaData.Value<string>("schemaString");
            if (!string.IsNullOrEmpty(schemaString)) ApplySchemaString(schemaString, result);

            var partitionCols = metaData["partitionColumns"] as JArray;
            if (partitionCols != null)
            {
                result.PartitionColumns = string.Join(", ",
                    partitionCols.Select(t => result.TranslateColumnName(t.ToString())));
            }

            var config = metaData["configuration"] as JObject;
            if (config != null) ApplyConfiguration(config, result);
        }

        /// <summary>
        /// Parses the Delta table schema JSON and, when column mapping is enabled, records the
        /// physical-name (GUID) to logical-name map so parquet column names can be resolved.
        /// </summary>
        private void ApplySchemaString(string schemaString, DeltaLogResult result)
        {
            try
            {
                var schema = JObject.Parse(schemaString);
                if (!(schema["fields"] is JArray fields)) return;
                foreach (var field in fields.OfType<JObject>())
                {
                    var logicalName = field.Value<string>("name");
                    if (string.IsNullOrEmpty(logicalName)) continue;
                    var physicalName = (field["metadata"] as JObject)?.Value<string>("delta.columnMapping.physicalName");
                    if (!string.IsNullOrEmpty(physicalName))
                    {
                        result.PhysicalToLogicalColumn[physicalName] = logicalName;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaLogReader), nameof(ApplySchemaString), "Error parsing delta schemaString for column mapping");
            }
        }

        private void ApplyConfiguration(JObject config, DeltaLogResult result)
        {
            foreach (var prop in config.Properties())
            {
                if (prop.Name.IndexOf("clusteringColumns", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.LiquidClusteringEnabled = true;
                    result.ClusteringColumns = FormatClusteringColumns(prop.Value?.ToString());
                }
            }
        }

        private void ApplyProtocol(JObject protocol, DeltaLogResult result)
        {
            var features = new List<string>();
            if (protocol["writerFeatures"] is JArray wf) features.AddRange(wf.Select(t => t.ToString()));
            if (protocol["readerFeatures"] is JArray rf) features.AddRange(rf.Select(t => t.ToString()));
            if (features.Any(f => f.IndexOf("clustering", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                result.LiquidClusteringEnabled = true;
            }
        }

        private void ApplyDomainMetadata(JObject domainMetadata, DeltaLogResult result)
        {
            var domain = domainMetadata.Value<string>("domain") ?? string.Empty;
            if (domain.IndexOf("clustering", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.LiquidClusteringEnabled = true;
                var config = domainMetadata.Value<string>("configuration");
                if (!string.IsNullOrEmpty(config) && string.IsNullOrEmpty(result.ClusteringColumns))
                {
                    result.ClusteringColumns = FormatClusteringColumns(config);
                }
            }
        }

        /// <summary>
        /// Best-effort formatting of a clustering columns value which may be a JSON array, a JSON object
        /// with a columns array, or a plain comma-delimited string.
        /// </summary>
        private static string FormatClusteringColumns(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            try
            {
                var token = JToken.Parse(raw);
                if (token is JArray arr) return string.Join(", ", arr.Select(FormatSingleColumn));
                if (token is JObject obj)
                {
                    var colsToken = obj["clusteringColumns"] ?? obj["columns"];
                    if (colsToken is JArray colsArr) return string.Join(", ", colsArr.Select(FormatSingleColumn));
                }
            }
            catch
            {
                // not JSON - fall through and return the raw string
            }
            return raw;
        }

        private static string FormatSingleColumn(JToken token)
        {
            // Clustering columns are sometimes serialized as arrays of physical name parts, e.g. [["col"]].
            if (token is JArray arr) return string.Join(".", arr.Select(t => t.ToString()));
            return token.ToString();
        }

        private static long? ExtractNumRecords(string stats)
        {
            if (string.IsNullOrWhiteSpace(stats)) return null;
            try
            {
                var obj = JObject.Parse(stats);
                return obj.Value<long?>("numRecords");
            }
            catch
            {
                return null;
            }
        }

        private static string DecodePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try { return Uri.UnescapeDataString(path); }
            catch { return path; }
        }

        private static string AppendError(string existing, string message)
        {
            return string.IsNullOrEmpty(existing) ? message : existing + "; " + message;
        }

        /// <summary>
        /// Reads a checkpoint parquet file. Scalar add/remove fields are aligned by row index to
        /// reconstruct the active file set; list/map fields (partitionColumns, features, configuration)
        /// are read as flattened value sets for best-effort clustering / partition detection.
        /// </summary>
        private async Task ReadCheckpointAsync(string url, Dictionary<string, DeltaAddFile> activeFiles, DeltaLogResult result, CancellationToken ct)
        {
            using (var stream = HttpRangeStream.Create(_client, url, ct))
            using (var reader = await ParquetReader.CreateAsync(stream, null, false, ct).ConfigureAwait(false))
            {
                var dataFields = reader.Schema.GetDataFields();
                var addPathField = FindField(dataFields, "add", "path");
                var addSizeField = FindField(dataFields, "add", "size");
                var addStatsField = FindField(dataFields, "add", "stats");
                var addModTimeField = FindField(dataFields, "add", "modificationtime");
                var addDvStorageField = FindField(dataFields, "add", "deletionvector", "storagetype");
                var addDvCardField = FindField(dataFields, "add", "deletionvector", "cardinality");
                var removePathField = FindField(dataFields, "remove", "path");

                var partitionFields = dataFields.Where(f => PathContains(f, "partitioncolumns")).ToList();
                var featureFields = dataFields.Where(f => PathContains(f, "writerfeatures") || PathContains(f, "readerfeatures")).ToList();
                var configKeyFields = dataFields.Where(f => PathContains(f, "configuration") && LeafIs(f, "key")).ToList();

                var partitionValues = new List<string>();
                var featureValues = new List<string>();
                var configKeyValues = new List<string>();

                for (int rg = 0; rg < reader.RowGroupCount; rg++)
                {
                    ct.ThrowIfCancellationRequested();
                    using (var rowGroup = reader.OpenRowGroupReader(rg))
                    {
                        var addPath = await ReadDataAsync(rowGroup, addPathField, ct).ConfigureAwait(false);
                        var addSize = await ReadDataAsync(rowGroup, addSizeField, ct).ConfigureAwait(false);
                        var addStats = await ReadDataAsync(rowGroup, addStatsField, ct).ConfigureAwait(false);
                        var addModTime = await ReadDataAsync(rowGroup, addModTimeField, ct).ConfigureAwait(false);
                        var addDvStorage = await ReadDataAsync(rowGroup, addDvStorageField, ct).ConfigureAwait(false);
                        var addDvCard = await ReadDataAsync(rowGroup, addDvCardField, ct).ConfigureAwait(false);
                        var removePath = await ReadDataAsync(rowGroup, removePathField, ct).ConfigureAwait(false);

                        if (addPath != null)
                        {
                            for (int i = 0; i < addPath.Length; i++)
                            {
                                var p = GetAt(addPath, i) as string;
                                if (string.IsNullOrEmpty(p)) continue;
                                var decoded = DecodePath(p);
                                var dvStorage = GetAt(addDvStorage, i) as string;
                                var hasDv = !string.IsNullOrEmpty(dvStorage);
                                activeFiles[decoded] = new DeltaAddFile
                                {
                                    Path = decoded,
                                    Size = ToLong(addSize, i),
                                    NumRecords = ExtractNumRecords(GetAt(addStats, i) as string),
                                    HasDeletionVector = hasDv,
                                    DeletionVectorCardinality = hasDv ? ToLong(addDvCard, i) : 0
                                };
                                result.TrackModified(ToLong(addModTime, i));
                            }
                        }

                        if (removePath != null)
                        {
                            foreach (var r in removePath)
                            {
                                var p = r as string;
                                if (!string.IsNullOrEmpty(p)) activeFiles.Remove(DecodePath(p));
                            }
                        }

                        CollectStrings(partitionFields, rowGroup, partitionValues, ct);
                        CollectStrings(featureFields, rowGroup, featureValues, ct);
                        CollectStrings(configKeyFields, rowGroup, configKeyValues, ct);
                    }
                }

                if (partitionValues.Count > 0 && string.IsNullOrEmpty(result.PartitionColumns))
                {
                    result.PartitionColumns = string.Join(", ", partitionValues.Distinct());
                }
                if (featureValues.Any(f => f.IndexOf("clustering", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    result.LiquidClusteringEnabled = true;
                }
                if (configKeyValues.Any(k => k.IndexOf("clusteringColumns", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    result.LiquidClusteringEnabled = true;
                }
            }
        }

        private static void CollectStrings(List<DataField> fields, Parquet.ParquetRowGroupReader rowGroup, List<string> target, CancellationToken ct)
        {
            foreach (var f in fields)
            {
                var data = ReadDataAsync(rowGroup, f, ct).GetAwaiter().GetResult();
                if (data == null) continue;
                foreach (var v in data)
                {
                    if (v is string s && !string.IsNullOrEmpty(s)) target.Add(s);
                }
            }
        }

        private static async Task<Array> ReadDataAsync(Parquet.ParquetRowGroupReader rowGroup, DataField field, CancellationToken ct)
        {
            if (field == null) return null;
            try
            {
                var col = await rowGroup.ReadColumnAsync(field, ct).ConfigureAwait(false);
                return col?.Data;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DeltaLogReader), nameof(ReadDataAsync), $"Error reading checkpoint column {field?.Path}");
                return null;
            }
        }

        private static object GetAt(Array array, int index)
        {
            if (array == null || index < 0 || index >= array.Length) return null;
            return array.GetValue(index);
        }

        private static long ToLong(Array array, int index)
        {
            var v = GetAt(array, index);
            if (v == null) return 0;
            try { return Convert.ToInt64(v); } catch { return 0; }
        }

        private static DataField FindField(DataField[] fields, params string[] parts)
        {
            var target = string.Join(".", parts).ToLowerInvariant();
            foreach (var f in fields)
            {
                var path = NormalizePath(f);
                if (path == target || path.EndsWith("." + target, StringComparison.Ordinal)) return f;
            }
            return null;
        }

        private static bool PathContains(DataField field, string fragment)
        {
            return NormalizePath(field).IndexOf(fragment, StringComparison.Ordinal) >= 0;
        }

        private static bool LeafIs(DataField field, string leaf)
        {
            var name = field?.Name ?? string.Empty;
            return string.Equals(name, leaf, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(DataField field)
        {
            var raw = field?.Path?.ToString() ?? field?.Name ?? string.Empty;
            return raw.Replace('/', '.').Trim('.').ToLowerInvariant();
        }
    }
}
