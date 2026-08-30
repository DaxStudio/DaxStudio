using DaxStudio.Common;
using DaxStudio.Core.Connections;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TOMT = Microsoft.AnalysisServices.Tabular;

namespace DaxStudio.Core.DeltaAnalyzer
{
    /// <summary>Resolved (or partially resolved) OneLake location for a single Direct Lake table.</summary>
    public class DirectLakeTableInfo
    {
        public string TableName { get; set; }
        public string SchemaName { get; set; }
        public string EntityName { get; set; }
        public string DfsBasePath { get; set; }
        public bool IsResolved { get; set; }

        /// <summary>The workspace segment (GUID or friendly name) used to build the OneLake path.</summary>
        public string WorkspaceId { get; set; }

        /// <summary>
        /// When the source is a Direct Lake on SQL model (referenced via <c>Sql.Database(...)</c>), this
        /// holds the SQL analytics endpoint id parsed from the expression. The endpoint id is NOT the
        /// OneLake item id, so it must be mapped to the underlying lakehouse item before use.
        /// </summary>
        public string SqlEndpointId { get; set; }
    }

    /// <summary>Result of attempting to resolve OneLake paths for the Direct Lake tables in a model.</summary>
    public class DirectLakeResolveResult
    {
        public List<DirectLakeTableInfo> Tables { get; } = new List<DirectLakeTableInfo>();
        public bool ModelHasDirectLakeTables { get; set; }
        public string WorkspaceName { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Derives the OneLake DFS path for each Direct Lake table in the connected model using TOM metadata
    /// and the connection server name. This resolution is inherently fragile (M expression formats vary),
    /// so all steps are best-effort and fall back cleanly to a manual override path.
    /// </summary>
    public class DirectLakePathResolver
    {
        private const string OneLakeHost = "https://onelake.dfs.fabric.microsoft.com";

        // powerbi://api.powerbi.com/v1.0/myorg/{Workspace}
        private static readonly Regex WorkspaceRegex = new Regex(@"myorg/(?<ws>[^/;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // Any onelake url embedded in an M expression: .../{workspace}/{lakehouse}...
        private static readonly Regex OneLakeUrlRegex = new Regex(@"onelake\.dfs\.fabric\.microsoft\.com/(?<ws>[^/""]+)/(?<lh>[^/""\]]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex GuidRegex = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled);

        // Direct Lake on SQL: the source is a Warehouse / SQL analytics endpoint referenced via
        // Sql.Database("<host>", "<itemGuid>"). Capture the server host and the database (item) id.
        private static readonly Regex SqlDatabaseRegex = new Regex(@"Sql\.Database\(\s*""(?<host>[^""]+)""\s*,\s*""(?<db>[^""]+)""", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // A base32-encoded GUID is 26 chars from the RFC-4648 alphabet (A-Z, 2-7). The Fabric SQL host
        // name embeds two of these: {tenantId}-{workspaceId}.<cluster>-datawarehouse.fabric.microsoft.com
        private static readonly Regex Base32SegmentRegex = new Regex(@"[A-Za-z2-7]{26}", RegexOptions.Compiled);
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        // abfss://{filesystem}@{host}/{rest} - the "Copy ABFS path" form offered by the Fabric portal.
        private static readonly Regex AbfsUrlRegex = new Regex(@"^abfss?://(?<fs>[^@/]+)@(?<host>[^/]+)/?(?<rest>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // The path group deliberately runs to the end of the string, so a '?' or '#' in a table name is
        // kept as part of the path instead of being read as a query or fragment delimiter.
        private static readonly Regex AbsoluteUrlRegex = new Regex(@"^(?<scheme>[a-z][a-z0-9+.\-]*)://(?<authority>[^/?#]+)(?<path>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Resolves Direct Lake table paths. Never throws for individual-table failures - unresolved
        /// tables are returned with <see cref="DirectLakeTableInfo.IsResolved"/> = false.
        /// </summary>
        public DirectLakeResolveResult Resolve(ConnectionManager connection, CancellationToken ct)
        {
            return Resolve(connection, null, ct);
        }

        /// <summary>
        /// Resolves Direct Lake table paths, reporting progress as each table's metadata is inspected.
        /// Never throws for individual-table failures - unresolved tables are returned with
        /// <see cref="DirectLakeTableInfo.IsResolved"/> = false.
        /// </summary>
        public DirectLakeResolveResult Resolve(ConnectionManager connection, IProgress<DeltaAnalyzerProgress> progress, CancellationToken ct, bool hasOverride = false)
        {
            var result = new DirectLakeResolveResult();
            if (connection == null)
            {
                result.Error = "No active connection.";
                return result;
            }

            var workspace = ParseWorkspaceName(connection.ServerName);
            result.WorkspaceName = workspace;

            TOMT.Server server = null;
            try
            {
                progress?.Report(new DeltaAnalyzerProgress
                {
                    Message = hasOverride ? "Connecting to OneLake..." : "Connecting to model metadata...",
                    ItemLabel = "tables"
                });

                server = new TOMT.Server();
                var token = connection.AccessToken;
                if (!string.IsNullOrEmpty(token.Token))
                {
                    server.AccessToken = new Microsoft.AnalysisServices.AccessToken(token.Token, token.ExpirationTime, null);
                }
                server.Connect(connection.ConnectionStringWithInitialCatalog);

                var db = server.Databases.FindByName(connection.DatabaseName)
                         ?? (server.Databases.Count > 0 ? server.Databases[0] : null);
                var model = db?.Model;
                if (model == null)
                {
                    result.Error = "Unable to load the tabular model metadata.";
                    return result;
                }

                long totalTables = model.Tables.Count;
                long tableIndex = 0;

                foreach (TOMT.Table table in model.Tables)
                {
                    ct.ThrowIfCancellationRequested();
                    tableIndex++;
                    progress?.Report(new DeltaAnalyzerProgress
                    {
                        Message = $"Determining OneLake location for '{table.Name}'...",
                        FilesProcessed = tableIndex,
                        TotalFiles = totalTables,
                        ItemLabel = "tables"
                    });

                    var dlPartition = table.Partitions
                        .Cast<TOMT.Partition>()
                        .FirstOrDefault(p => IsDirectLake(p));
                    if (dlPartition == null) continue;

                    result.ModelHasDirectLakeTables = true;

                    var info = new DirectLakeTableInfo { TableName = table.Name };
                    try
                    {
                        var entitySource = dlPartition.Source as TOMT.EntityPartitionSource;
                        info.EntityName = entitySource?.EntityName ?? table.Name;
                        info.SchemaName = entitySource?.SchemaName;

                        // Extract the workspace and lakehouse from the SAME OneLake URL in the M
                        // expression so they are consistent. OneLake rejects mixing a friendly
                        // workspace name with a GUID artifact id (or vice versa), so we must not
                        // combine the (friendly) server-name workspace with an expression GUID
                        // lakehouse. Only fall back to the server-name workspace when the
                        // expression does not contain a OneLake URL.
                        var (exprWorkspace, lakehouse, isSqlEndpoint) = ExtractWorkspaceAndLakehouse(entitySource);
                        var wsSegment = !string.IsNullOrEmpty(exprWorkspace)
                            ? exprWorkspace
                            : workspace;

                        info.WorkspaceId = wsSegment;
                        if (isSqlEndpoint)
                        {
                            // Provisional only: the SQL endpoint id is not a valid OneLake item, so this
                            // path will be rewritten once the underlying lakehouse item is resolved.
                            info.SqlEndpointId = lakehouse;
                        }

                        if (!string.IsNullOrEmpty(wsSegment) && !string.IsNullOrEmpty(lakehouse))
                        {
                            info.DfsBasePath = BuildTablePath(wsSegment, lakehouse, info.SchemaName, info.EntityName);
                            info.IsResolved = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, Constants.LogMessageTemplate, nameof(DirectLakePathResolver), nameof(Resolve), $"Error resolving path for table {table.Name}");
                    }

                    result.Tables.Add(info);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Constants.LogMessageTemplate, nameof(DirectLakePathResolver), nameof(Resolve), "Error resolving Direct Lake paths from TOM");
                result.Error = ex.Message;
            }
            finally
            {
                try { server?.Disconnect(); } catch { /* ignore */ }
                try { server?.Dispose(); } catch { /* ignore */ }
            }

            return result;
        }

        private static bool IsDirectLake(TOMT.Partition partition)
        {
            try
            {
                return partition.Mode == TOMT.ModeType.DirectLake || partition.Source is TOMT.EntityPartitionSource;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Parses the workspace name from a Power BI XMLA server name.</summary>
        public static string ParseWorkspaceName(string serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName)) return null;
            var m = WorkspaceRegex.Match(serverName);
            if (!m.Success) return null;
            var ws = m.Groups["ws"].Value.Trim();
            try { ws = Uri.UnescapeDataString(ws); } catch { /* keep raw */ }
            return ws;
        }

        /// <summary>
        /// Extracts the workspace and lakehouse segments from the OneLake URL embedded in the
        /// partition's M expression. Returned as a pair so callers can keep them consistent
        /// (both GUIDs, or both friendly names) as required by the OneLake DFS API.
        /// </summary>
        private static (string workspace, string lakehouse, bool isSqlEndpoint) ExtractWorkspaceAndLakehouse(TOMT.EntityPartitionSource entitySource)
        {
            var expression = GetExpressionText(entitySource);
            if (string.IsNullOrEmpty(expression)) return (null, null, false);

            // Direct Lake on OneLake: the workspace + lakehouse are in an embedded OneLake URL.
            var m = OneLakeUrlRegex.Match(expression);
            if (m.Success) return (m.Groups["ws"].Value.Trim(), m.Groups["lh"].Value.Trim(), false);

            // Direct Lake on SQL: the source is a Warehouse / SQL analytics endpoint referenced via
            // Sql.Database("<host>", "<itemGuid>"). The workspace GUID is base32-encoded in the host
            // (2nd segment; the 1st is the tenant), and the item GUID is the database argument. Both
            // come out as GUIDs, which OneLake accepts as a consistent workspace/item pair.
            var sql = SqlDatabaseRegex.Match(expression);
            if (sql.Success)
            {
                var workspaceGuid = ExtractWorkspaceGuidFromSqlHost(sql.Groups["host"].Value);
                var itemGuid = sql.Groups["db"].Value.Trim();
                if (!string.IsNullOrEmpty(workspaceGuid) && !string.IsNullOrEmpty(itemGuid))
                    return (workspaceGuid, itemGuid, true);
            }

            return (null, null, false);
        }

        /// <summary>
        /// Derives the workspace GUID from a Fabric SQL endpoint host name of the form
        /// <c>{tenantId-b32}-{workspaceId-b32}.&lt;cluster&gt;-datawarehouse.fabric.microsoft.com</c>.
        /// The two 26-char base32 segments are the tenant id and workspace id respectively.
        /// </summary>
        public static string ExtractWorkspaceGuidFromSqlHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return null;
            var segs = Base32SegmentRegex.Matches(host);
            if (segs.Count < 2) return null;
            return DecodeBase32Guid(segs[1].Value);
        }

        /// <summary>Decodes a 26-character RFC-4648 base32 string into a GUID string, or null on failure.</summary>
        public static string DecodeBase32Guid(string value)
        {
            try
            {
                if (string.IsNullOrEmpty(value)) return null;
                var s = value.ToUpperInvariant();
                var bits = new System.Text.StringBuilder(s.Length * 5);
                foreach (var c in s)
                {
                    int idx = Base32Alphabet.IndexOf(c);
                    if (idx < 0) return null;
                    bits.Append(Convert.ToString(idx, 2).PadLeft(5, '0'));
                }
                if (bits.Length < 128) return null;
                var bytes = new byte[16];
                for (int i = 0; i < 16; i++)
                {
                    bytes[i] = Convert.ToByte(bits.ToString(i * 8, 8), 2);
                }
                return new Guid(bytes).ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string GetExpressionText(TOMT.EntityPartitionSource entitySource)
        {
            try
            {
                return entitySource?.ExpressionSource?.Expression;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Builds the OneLake DFS table path.
        /// <c>https://onelake.dfs.fabric.microsoft.com/{workspace}/{lakehouse}.Lakehouse/Tables/{schema?}/{table}</c>
        /// </summary>
        public static string BuildTablePath(string workspace, string lakehouse, string schema, string table)
        {
            var ws = Uri.EscapeDataString(workspace);
            var lh = NormalizeLakehouse(lakehouse);
            var root = $"{OneLakeHost}/{ws}/{lh}/Tables";
            return BuildFromOverrideRoot(root, schema, table);
        }

        private static string NormalizeLakehouse(string lakehouse)
        {
            var lh = lakehouse.Trim();
            // Already includes an item-type suffix (e.g. ".Lakehouse") - use as-is.
            if (lh.IndexOf(".Lakehouse", StringComparison.OrdinalIgnoreCase) >= 0) return lh;
            // A bare GUID is a valid OneLake item reference without a suffix.
            if (GuidRegex.IsMatch(lh)) return lh;
            // Otherwise assume it's a lakehouse display name.
            return $"{lh}.Lakehouse";
        }

        /// <summary>
        /// Cleans up a OneLake path typed or pasted by the user so it can be used as a DFS URL. Handles
        /// the forms most commonly copied out of the Fabric portal, a notebook or a DAX/M expression:
        /// surrounding whitespace and quotes, the <c>abfss://{workspace}@{host}/{item}/...</c> ABFS form
        /// (rewritten to the equivalent https DFS URL), and a trailing slash. Every path segment is
        /// re-encoded so characters that are not legal raw in a URL - notably spaces and square brackets -
        /// are percent-escaped exactly once.
        /// <para>
        /// No characters are ever removed. A OneLake folder name can genuinely contain square brackets, so
        /// whether a bracketed name is quoted or literal cannot be decided here - the alternatives are
        /// tried at request time instead.
        /// </para>
        /// </summary>
        public static string NormalizeOneLakePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return path;

            var text = path.Trim().Trim('"', '\'').Trim();

            var abfs = AbfsUrlRegex.Match(text);
            if (abfs.Success)
            {
                text = $"https://{abfs.Groups["host"].Value}/{abfs.Groups["fs"].Value}/{abfs.Groups["rest"].Value}";
            }

            text = text.TrimEnd('/');

            // Only rewrite something we can parse - anything else is handed back untouched so the caller's
            // validation can report it rather than this method silently mangling it.
            if (!TrySplitUrl(text, out var origin, out var rawSegments)) return text;

            var segments = rawSegments.Select(NormalizeSegment).ToArray();
            if (segments.Length == 0) return origin;
            return $"{origin}/{string.Join("/", segments)}";
        }

        /// <summary>
        /// Splits an absolute URL into its origin (scheme and authority) and its raw, still-encoded path
        /// segments.
        /// <para>
        /// This deliberately does not use <see cref="Uri.AbsolutePath"/>. A table name can contain a literal
        /// <c>?</c> or <c>#</c>, and in an unencoded path <see cref="Uri"/> reads those as the start of the
        /// query or fragment - so <c>AbsolutePath</c> would silently truncate the name at the first one.
        /// Splitting textually keeps the whole path, and every segment is percent-escaped afterwards so
        /// those characters cannot be misread again.
        /// </para>
        /// </summary>
        private static bool TrySplitUrl(string text, out string origin, out string[] segments)
        {
            origin = null;
            segments = null;

            var match = AbsoluteUrlRegex.Match(text);
            if (!match.Success) return false;

            origin = $"{match.Groups["scheme"].Value}://{match.Groups["authority"].Value}";
            segments = match.Groups["path"].Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return true;
        }

        /// <summary>
        /// Re-encodes a single path segment. The segment is decoded first so an already-encoded path is not
        /// double-encoded, then escaped - which turns a space into <c>%20</c> and a square bracket into
        /// <c>%5B</c>/<c>%5D</c> rather than leaving it raw in the URL, where it is not valid.
        /// </summary>
        private static string NormalizeSegment(string segment)
        {
            try { return Uri.EscapeDataString(Uri.UnescapeDataString(segment)); }
            catch { return segment; }
        }

        /// <summary>
        /// Determines whether a OneLake path already identifies a single table folder (i.e. it has at
        /// least one segment below a <c>Tables</c> folder) rather than being the <c>.../Tables</c> root
        /// that every table path should be rebuilt from. Returns the decoded folder name when it does.
        /// <para>
        /// Note this is the name as it appears in the path supplied, which may still carry SQL identifier
        /// quoting if the path was copied from warehouse table properties. Once the analysis has found the
        /// folder that actually exists, prefer the name taken from that resolved path.
        /// </para>
        /// </summary>
        public static bool TryGetTableFromPath(string path, out string tableName)
        {
            tableName = null;
            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!TrySplitUrl(path.Trim().TrimEnd('/'), out _, out var segments)) return false;

            var tablesIndex = Array.FindLastIndex(segments, s => string.Equals(s, "Tables", StringComparison.OrdinalIgnoreCase));
            // No Tables segment, or nothing below it, means this is a root rather than a table.
            if (tablesIndex < 0 || tablesIndex >= segments.Length - 1) return false;

            var leaf = segments[segments.Length - 1];
            try { leaf = Uri.UnescapeDataString(leaf); } catch { /* keep raw */ }
            tableName = leaf;
            return true;
        }

        /// <summary>
        /// Builds a full table path from a user-supplied override root (which should point at the
        /// <c>.../Tables</c> folder), appending the optional schema and the entity/table name.
        /// </summary>
        public static string BuildFromOverrideRoot(string overrideRoot, string schema, string table)
        {
            var root = (overrideRoot ?? string.Empty).TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(schema))
            {
                return $"{root}/{Uri.EscapeDataString(schema)}/{Uri.EscapeDataString(table)}";
            }
            return $"{root}/{Uri.EscapeDataString(table)}";
        }
    }
}
