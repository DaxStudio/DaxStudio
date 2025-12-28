using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DaxStudio.Common.Enums;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Serilog;

namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for enriching raw query plan data with timing metrics,
    /// resolved column names, and detected performance issues.
    /// </summary>
    public class PlanEnrichmentService : IPlanEnrichmentService
    {
        private readonly IPerformanceIssueDetector _issueDetector;

        public PlanEnrichmentService()
            : this(new PerformanceIssueDetector())
        {
        }

        public PlanEnrichmentService(IPerformanceIssueDetector issueDetector)
        {
            _issueDetector = issueDetector ?? throw new ArgumentNullException(nameof(issueDetector));
        }

        /// <summary>
        /// Enriches a physical query plan with timing and metadata.
        /// </summary>
        public async Task<EnrichedQueryPlan> EnrichPhysicalPlanAsync(
            IEnumerable<PhysicalQueryPlanRow> rawPlan,
            IEnumerable<TraceStorageEngineEvent> timingEvents,
            IColumnNameResolver columnResolver,
            string activityId)
        {
            // Run on background thread to keep UI responsive (FR-016)
            return await Task.Run(() =>
            {
                var plan = new EnrichedQueryPlan
                {
                    ActivityID = activityId,
                    PlanType = PlanType.Physical,
                    State = PlanState.Raw
                };

                try
                {
                    var rows = rawPlan?.ToList();
                    if (rows == null || rows.Count == 0)
                    {
                        Log.Debug("PlanEnrichmentService: No rows in physical plan");
                        return plan;
                    }

                    // Step 1: Parse and build tree structure
                    var nodes = BuildTreeFromRows(rows);
                    plan.AllNodes = nodes;
                    plan.RootNode = GetEffectiveRootNode(nodes);
                    plan.State = PlanState.Parsed;

                    // Step 2: Resolve column names
                    if (columnResolver?.IsInitialized == true)
                    {
                        ResolveColumnNames(nodes, columnResolver);
                    }

                    // Step 3: Correlate timing data
                    if (timingEvents != null)
                    {
                        CorrelateTimingData(plan, timingEvents.ToList());
                    }

                    // Step 4: Calculate cost percentages
                    CalculateCostPercentages(plan);

                    // Step 5: Determine engine types
                    AssignEngineTypes(nodes);

                    // Step 6: Detect performance issues
                    var issues = _issueDetector.DetectIssues(plan);
                    foreach (var issue in issues)
                    {
                        plan.Issues.Add(issue);
                        var affectedNode = plan.FindNodeById(issue.AffectedNodeId);
                        affectedNode?.Issues.Add(issue);
                    }

                    plan.State = PlanState.Enriched;
                    Log.Debug("PlanEnrichmentService: Enriched physical plan with {NodeCount} nodes and {IssueCount} issues",
                        plan.NodeCount, plan.IssueCount);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "PlanEnrichmentService: Error enriching physical plan");
                    plan.State = PlanState.Error;
                    plan.ErrorMessage = ex.Message;
                }

                return plan;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Enriches a logical query plan with metadata.
        /// </summary>
        public async Task<EnrichedQueryPlan> EnrichLogicalPlanAsync(
            IEnumerable<LogicalQueryPlanRow> rawPlan,
            IEnumerable<TraceStorageEngineEvent> timingEvents,
            IColumnNameResolver columnResolver,
            string activityId)
        {
            return await Task.Run(() =>
            {
                var plan = new EnrichedQueryPlan
                {
                    ActivityID = activityId,
                    PlanType = PlanType.Logical,
                    State = PlanState.Raw
                };

                try
                {
                    var rows = rawPlan?.ToList();
                    if (rows == null || rows.Count == 0)
                    {
                        Log.Debug("PlanEnrichmentService: No rows in logical plan");
                        return plan;
                    }

                    // Build tree from rows (logical plans have same structure)
                    var nodes = BuildTreeFromQueryPlanRows(rows);
                    plan.AllNodes = nodes;
                    plan.RootNode = GetEffectiveRootNode(nodes);
                    plan.State = PlanState.Parsed;

                    // Resolve column names
                    if (columnResolver?.IsInitialized == true)
                    {
                        ResolveColumnNames(nodes, columnResolver);
                    }

                    // Correlate timing data (xmSQL, durations) from SE events
                    CorrelateTimingData(plan, timingEvents?.ToList());

                    // Assign engine types based on operator dictionary
                    AssignEngineTypes(nodes);

                    // Detect performance issues (same as physical plan)
                    var issues = _issueDetector.DetectIssues(plan);
                    foreach (var issue in issues)
                    {
                        plan.Issues.Add(issue);
                        var affectedNode = plan.FindNodeById(issue.AffectedNodeId);
                        affectedNode?.Issues.Add(issue);
                    }

                    plan.State = PlanState.Enriched;
                    Log.Debug("PlanEnrichmentService: Enriched logical plan with {NodeCount} nodes and {IssueCount} issues",
                        plan.NodeCount, plan.IssueCount);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "PlanEnrichmentService: Error enriching logical plan");
                    plan.State = PlanState.Error;
                    plan.ErrorMessage = ex.Message;
                }

                return plan;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the effective root node for the plan. When there are multiple Level 0 nodes,
        /// creates a synthetic "Query" root that contains all Level 0 nodes as children.
        /// For single root, returns that root directly.
        /// </summary>
        private EnrichedPlanNode GetEffectiveRootNode(List<EnrichedPlanNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return null;

            var rootNodes = nodes.Where(n => n.Level == 0).ToList();

            if (rootNodes.Count == 0)
                return null;

            if (rootNodes.Count == 1)
                return rootNodes[0];

            // Multiple root nodes - create a synthetic "Query" root to contain them all
            // This occurs with DEFINE VAR patterns where multiple subtrees exist
            Log.Debug("PlanEnrichmentService: Found {Count} root-level nodes, creating synthetic Query root", rootNodes.Count);

            // Find the minimum NodeId to create a unique ID for the synthetic root
            var minNodeId = nodes.Min(n => n.NodeId);
            var syntheticNodeId = minNodeId - 1; // Ensure unique ID below all other nodes

            var syntheticRoot = new EnrichedPlanNode
            {
                NodeId = syntheticNodeId,
                Level = -1, // Above Level 0
                Operation = "Query",
                ResolvedOperation = "Query"
            };

            // Add all root nodes as children of the synthetic root
            foreach (var rootNode in rootNodes)
            {
                rootNode.Parent = syntheticRoot;
                syntheticRoot.Children.Add(rootNode);
            }

            // Add synthetic root to the nodes list so it's included in BuildTree processing
            nodes.Insert(0, syntheticRoot);

            return syntheticRoot;
        }

        private List<EnrichedPlanNode> BuildTreeFromRows(List<PhysicalQueryPlanRow> rows)
        {
            var nodes = new List<EnrichedPlanNode>();
            var nodeStack = new Stack<EnrichedPlanNode>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var node = new EnrichedPlanNode
                {
                    NodeId = i + 1,
                    RowNumber = row.RowNumber,
                    Level = row.Level,
                    Operation = row.Operation,
                    ResolvedOperation = row.Operation,
                    Records = row.Records,
                    NextSiblingRowNumber = row.NextSiblingRowNumber
                };

                // Pop nodes from stack until we find parent level
                while (nodeStack.Count > 0 && nodeStack.Peek().Level >= node.Level)
                {
                    nodeStack.Pop();
                }

                // Assign parent
                if (nodeStack.Count > 0)
                {
                    node.Parent = nodeStack.Peek();
                    node.Parent.Children.Add(node);
                }

                nodeStack.Push(node);
                nodes.Add(node);
            }

            return nodes;
        }

        private List<EnrichedPlanNode> BuildTreeFromQueryPlanRows(List<LogicalQueryPlanRow> rows)
        {
            var nodes = new List<EnrichedPlanNode>();
            var nodeStack = new Stack<EnrichedPlanNode>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var node = new EnrichedPlanNode
                {
                    NodeId = i + 1,
                    RowNumber = row.RowNumber,
                    Level = row.Level,
                    Operation = row.Operation,
                    ResolvedOperation = row.Operation,
                    NextSiblingRowNumber = row.NextSiblingRowNumber
                };

                while (nodeStack.Count > 0 && nodeStack.Peek().Level >= node.Level)
                {
                    nodeStack.Pop();
                }

                if (nodeStack.Count > 0)
                {
                    node.Parent = nodeStack.Peek();
                    node.Parent.Children.Add(node);
                }

                nodeStack.Push(node);
                nodes.Add(node);
            }

            return nodes;
        }

        private void ResolveColumnNames(List<EnrichedPlanNode> nodes, IColumnNameResolver resolver)
        {
            foreach (var node in nodes)
            {
                node.ResolvedOperation = resolver.ResolveOperationString(node.Operation);
            }
        }

        private void CorrelateTimingData(EnrichedQueryPlan plan, List<TraceStorageEngineEvent> timingEvents)
        {
            Log.Information(">>> PlanEnrichmentService.CorrelateTimingData() ENTRY - TimingEvents={Count}", timingEvents?.Count ?? 0);

            if (timingEvents == null || timingEvents.Count == 0)
            {
                Log.Information(">>> PlanEnrichmentService: No timing events to correlate - RETURNING EARLY");
                return;
            }

            // Aggregate timing data from storage engine events
            long totalSeDuration = 0;
            long totalSeCpu = 0;
            long totalDqDuration = 0;
            int cacheHits = 0;

            // Separate Storage Engine and DirectQuery events
            var seEvents = timingEvents.Where(e => !e.IsDirectQuery).ToList();
            var dqEvents = timingEvents.Where(e => e.IsDirectQuery).ToList();

            Log.Information(">>> PlanEnrichmentService: {SeCount} SE events, {DqCount} DirectQuery events",
                seEvents.Count, dqEvents.Count);

            // Collect ALL Storage Engine nodes (Vertipaq operators)
            // IMPORTANT: Only match operators that START with a Vertipaq operator name,
            // not those that just reference a Vertipaq logical op via LogOp=
            // e.g., "Scan_Vertipaq: RelLogOp..." is SE, but
            //       "Spool_Iterator: IterPhyOp LogOp=Scan_Vertipaq" is FE
            var seNodes = plan.AllNodes.Where(n => n.Operation != null &&
                IsStorageEngineNode(n.Operation)).ToList();

            // Collect DirectQueryResult nodes separately
            var dqNodes = plan.AllNodes.Where(n => n.Operation != null &&
                n.Operation.Contains("DirectQueryResult")).ToList();

            Log.Information(">>> PlanEnrichmentService: Collected {SeCount} SE nodes, {DqCount} DQ nodes for correlation",
                seNodes.Count, dqNodes.Count);

            // Pre-extract columns from SE nodes (RequiredCols pattern)
            var seNodeColumns = new Dictionary<EnrichedPlanNode, HashSet<string>>();
            foreach (var node in seNodes)
            {
                var columns = ExtractColumnsFromRequiredCols(node.Operation);
                seNodeColumns[node] = columns;
                var nodeTable = ExtractTableNameFromOperation(node.Operation);
                Log.Debug(">>> PlanEnrichmentService: SE Node {NodeId} table='{Table}' columns=[{Columns}] op='{Op}'",
                    node.NodeId, nodeTable ?? "(null)", string.Join(", ", columns),
                    node.Operation?.Substring(0, Math.Min(80, node.Operation?.Length ?? 0)));
            }

            // Pre-extract columns from DQ nodes (Fields pattern)
            var dqNodeColumns = new Dictionary<EnrichedPlanNode, HashSet<string>>();
            foreach (var node in dqNodes)
            {
                var columns = ExtractColumnsFromDirectQueryResult(node.Operation);
                dqNodeColumns[node] = columns;
                Log.Debug(">>> PlanEnrichmentService: DQ Node {NodeId} has columns: [{Columns}]",
                    node.NodeId, string.Join(", ", columns));
            }

            Log.Information(">>> PlanEnrichmentService: Collected {SeCount} SE nodes, {DqCount} DQ nodes for correlation",
                seNodes.Count, dqNodes.Count);

            // Track which nodes have been matched to avoid double-assignment
            var matchedNodes = new HashSet<int>();

            // === CORRELATE STORAGE ENGINE (xmSQL) EVENTS ===
            foreach (var evt in seEvents)
            {
                if (evt.Duration.HasValue) totalSeDuration += evt.Duration.Value;
                if (evt.CpuTime.HasValue) totalSeCpu += evt.CpuTime.Value;

                bool isCacheHit = evt.Subclass == DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch;
                if (isCacheHit) cacheHits++;

                var xmSql = evt.Query ?? evt.TextData;
                var xmSqlColumns = ExtractColumnsFromXmSql(xmSql);
                var xmSqlTable = ExtractTableNameFromXmSql(xmSql);

                // Fallback to ObjectName when xmSQL is empty (common when TextData isn't captured by trace)
                if (string.IsNullOrEmpty(xmSqlTable) && !string.IsNullOrEmpty(evt.ObjectName))
                {
                    xmSqlTable = evt.ObjectName;
                    Log.Debug(">>> PlanEnrichmentService: Using ObjectName fallback for SE event table: '{Table}'", xmSqlTable);
                }

                Log.Information(">>> PlanEnrichmentService: SE Event table='{Table}', columns=[{Columns}], objectName='{ObjectName}', query='{Query}'",
                    xmSqlTable ?? "(null)", string.Join(", ", xmSqlColumns), evt.ObjectName ?? "(null)",
                    xmSql?.Substring(0, Math.Min(100, xmSql?.Length ?? 0)) ?? "(null)");

                // Find the best matching SE node by column overlap
                EnrichedPlanNode bestMatch = null;
                int bestOverlap = 0;

                foreach (var node in seNodes)
                {
                    if (matchedNodes.Contains(node.NodeId))
                        continue;

                    var nodeColumns = seNodeColumns[node];
                    var nodeTable = ExtractTableNameFromOperation(node.Operation);

                    int overlap = CalculateColumnOverlap(nodeColumns, xmSqlColumns);

                    // Table name matching: +10 if both have table names that match
                    // This enables matching even when xmSQL columns are empty (e.g., ObjectName fallback)
                    if (!string.IsNullOrEmpty(xmSqlTable) && !string.IsNullOrEmpty(nodeTable) &&
                        string.Equals(xmSqlTable, nodeTable, StringComparison.OrdinalIgnoreCase))
                    {
                        overlap += 10;
                    }

                    if (overlap > bestOverlap)
                    {
                        bestOverlap = overlap;
                        bestMatch = node;
                    }
                }

                if (bestMatch != null && bestOverlap > 0)
                {
                    matchedNodes.Add(bestMatch.NodeId);
                    ApplyTimingToNode(bestMatch, evt, xmSqlTable, isCacheHit, EngineType.StorageEngine);
                    Log.Information(">>> PlanEnrichmentService: MATCHED SE event to node {NodeId} (overlap={Overlap}, table='{Table}')",
                        bestMatch.NodeId, bestOverlap, xmSqlTable ?? "(null)");
                }
                else
                {
                    Log.Warning(">>> PlanEnrichmentService: NO MATCH for SE event table='{Table}', bestOverlap={Overlap}, seNodes.Count={Count}",
                        xmSqlTable ?? "(null)", bestOverlap, seNodes.Count);
                }
            }

            // === CORRELATE DIRECTQUERY (T-SQL) EVENTS ===
            foreach (var evt in dqEvents)
            {
                if (evt.Duration.HasValue) totalDqDuration += evt.Duration.Value;

                var tSql = evt.Query ?? evt.TextData;
                var tSqlColumns = ExtractColumnsFromTSql(tSql);
                var tSqlTable = ExtractTableNameFromTSql(tSql);

                Log.Debug(">>> PlanEnrichmentService: DQ Event T-SQL table='{Table}', columns=[{Columns}]",
                    tSqlTable ?? "(null)", string.Join(", ", tSqlColumns));

                // Find the best matching DirectQueryResult node
                EnrichedPlanNode bestMatch = null;
                int bestOverlap = 0;

                foreach (var node in dqNodes)
                {
                    if (matchedNodes.Contains(node.NodeId))
                        continue;

                    var nodeColumns = dqNodeColumns[node];
                    var nodeTable = ExtractTableNameFromDirectQueryResult(node.Operation);

                    // Calculate overlap - for DQ, column names should match directly
                    int overlap = CalculateColumnOverlap(nodeColumns, tSqlColumns);

                    // Table name bonus - compare normalized names
                    if (!string.IsNullOrEmpty(tSqlTable) && !string.IsNullOrEmpty(nodeTable) &&
                        NormalizeTableName(tSqlTable).Equals(NormalizeTableName(nodeTable), StringComparison.OrdinalIgnoreCase))
                    {
                        overlap += 10;
                    }

                    if (overlap > bestOverlap)
                    {
                        bestOverlap = overlap;
                        bestMatch = node;
                    }
                }

                if (bestMatch != null && bestOverlap > 0)
                {
                    matchedNodes.Add(bestMatch.NodeId);
                    ApplyTimingToNode(bestMatch, evt, tSqlTable, false, EngineType.DirectQuery);
                    Log.Information(">>> PlanEnrichmentService: MATCHED DQ event to node {NodeId} (overlap={Overlap}, table='{Table}')",
                        bestMatch.NodeId, bestOverlap, tSqlTable);
                }
                else
                {
                    Log.Debug(">>> PlanEnrichmentService: No DQ node match for T-SQL, table='{Table}', columns=[{Columns}]",
                        tSqlTable ?? "(null)", string.Join(", ", tSqlColumns.Take(3)));
                }
            }

            plan.StorageEngineDurationMs = totalSeDuration;
            plan.StorageEngineCpuMs = totalSeCpu;
            plan.DirectQueryDurationMs = totalDqDuration;
            plan.CacheHits = cacheHits;
            plan.StorageEngineQueryCount = seEvents.Count; // Count all SE events (user + internal)
            plan.DirectQueryCount = dqEvents.Count;

            if (plan.TotalDurationMs > 0)
            {
                plan.FormulaEngineDurationMs = Math.Max(0, plan.TotalDurationMs - totalSeDuration - totalDqDuration);
            }

            Log.Information(">>> PlanEnrichmentService: Correlation complete. SE={SeDuration}ms, DQ={DqDuration}ms, Cache hits={CacheHits}, Matched={MatchedCount}",
                totalSeDuration, totalDqDuration, cacheHits, matchedNodes.Count);

            // === PROPAGATE TIMING TO CACHE NODES ===
            // Cache nodes don't have column references, so propagate timing from ancestor Spool/Vertipaq nodes
            PropagateTimingToCacheNodes(plan);
        }

        /// <summary>
        /// Propagates timing data from ancestor nodes to Cache nodes.
        /// Cache operators don't have column references in their operation strings,
        /// so they can't be directly correlated with xmSQL events.
        /// Instead, they inherit timing data from their nearest ancestor that has timing.
        /// </summary>
        private void PropagateTimingToCacheNodes(EnrichedQueryPlan plan)
        {
            // Find all Cache nodes that don't have timing data
            var cacheNodes = plan.AllNodes.Where(n =>
                n.Operation != null &&
                n.Operation.StartsWith("Cache:", StringComparison.OrdinalIgnoreCase) &&
                !n.DurationMs.HasValue).ToList();

            if (cacheNodes.Count == 0)
            {
                Log.Debug(">>> PlanEnrichmentService: No Cache nodes without timing data");
                return;
            }

            Log.Debug(">>> PlanEnrichmentService: Found {Count} Cache nodes to propagate timing to", cacheNodes.Count);

            foreach (var cacheNode in cacheNodes)
            {
                // Walk up the tree to find an ancestor with timing data
                var ancestor = cacheNode.Parent;
                while (ancestor != null)
                {
                    if (ancestor.DurationMs.HasValue && ancestor.DurationMs > 0)
                    {
                        // Found an ancestor with timing - propagate relevant data
                        cacheNode.XmSql = ancestor.XmSql;
                        cacheNode.ResolvedXmSql = ancestor.ResolvedXmSql;
                        cacheNode.DurationMs = ancestor.DurationMs;
                        cacheNode.CpuTimeMs = ancestor.CpuTimeMs;
                        cacheNode.EstimatedRows = ancestor.EstimatedRows;
                        cacheNode.EstimatedKBytes = ancestor.EstimatedKBytes;
                        cacheNode.IsCacheHit = ancestor.IsCacheHit;
                        cacheNode.EngineType = ancestor.EngineType;
                        cacheNode.ObjectName = ancestor.ObjectName;
                        cacheNode.Parallelism = ancestor.Parallelism;
                        cacheNode.NetParallelDurationMs = ancestor.NetParallelDurationMs;

                        Log.Debug(">>> PlanEnrichmentService: Propagated timing from node {AncestorId} to Cache node {CacheId} (Duration={Duration}ms)",
                            ancestor.NodeId, cacheNode.NodeId, ancestor.DurationMs);
                        break;
                    }
                    ancestor = ancestor.Parent;
                }
            }
        }

        /// <summary>
        /// Applies timing data from an event to a plan node.
        /// </summary>
        private void ApplyTimingToNode(EnrichedPlanNode node, TraceStorageEngineEvent evt, string tableName, bool isCacheHit, EngineType engineType)
        {
            node.XmSql = evt.TextData ?? evt.Query;
            node.ResolvedXmSql = evt.Query ?? evt.TextData;
            node.DurationMs = evt.Duration;
            node.CpuTimeMs = evt.CpuTime;
            node.EstimatedRows = evt.EstimatedRows;
            node.EstimatedKBytes = evt.EstimatedKBytes;
            node.IsCacheHit = isCacheHit;
            node.EngineType = engineType;
            node.ObjectName = tableName;

            // Reconcile row counts
            if ((!node.Records.HasValue || node.Records == 0) && evt.EstimatedRows.HasValue && evt.EstimatedRows > 0)
            {
                node.Records = evt.EstimatedRows;
                node.RecordsSource = "ServerTiming";
            }

            // Calculate parallelism
            if (evt.Duration.HasValue && evt.NetParallelDuration.HasValue && evt.NetParallelDuration > 0)
            {
                node.NetParallelDurationMs = evt.NetParallelDuration;
                node.Parallelism = (int)Math.Max(1, Math.Round((double)evt.Duration.Value / evt.NetParallelDuration.Value));
            }
        }

        /// <summary>
        /// Extracts columns from DirectQueryResult Fields() pattern.
        /// E.g., "DirectQueryResult : IterPhyOp ... Fields('Sales SalesOrderDetail'[SalesOrderID])"
        /// Returns: {"SalesOrderID"}
        /// </summary>
        private HashSet<string> ExtractColumnsFromDirectQueryResult(string operation)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(operation)) return columns;

            // Match Fields(...) section and extract columns within
            var fieldsMatch = Regex.Match(operation, @"Fields\(([^)]*)\)");
            if (fieldsMatch.Success)
            {
                var fieldsContent = fieldsMatch.Groups[1].Value;
                // Match 'Table'[Column] patterns within Fields()
                var colMatches = Regex.Matches(fieldsContent, @"'[^']*'\[([^\]]+)\]");
                foreach (Match match in colMatches)
                {
                    columns.Add(match.Groups[1].Value);
                }
            }

            return columns;
        }

        /// <summary>
        /// Extracts table name from DirectQueryResult Fields() pattern.
        /// E.g., "DirectQueryResult : ... Fields('Sales SalesOrderDetail'[SalesOrderID])"
        /// Returns: "Sales SalesOrderDetail"
        /// </summary>
        private string ExtractTableNameFromDirectQueryResult(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return null;

            // Match first 'TableName' in Fields section
            var fieldsMatch = Regex.Match(operation, @"Fields\([^)]*'([^']+)'");
            if (fieldsMatch.Success)
            {
                return fieldsMatch.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Normalizes table name for comparison by removing spaces and converting to lowercase.
        /// Handles differences like "Sales SalesOrderDetail" vs "SalesSalesOrderDetail".
        /// </summary>
        private string NormalizeTableName(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return string.Empty;
            return tableName.Replace(" ", "").ToLowerInvariant();
        }

        /// <summary>
        /// Extracts column names from RequiredCols pattern in operation string.
        /// E.g., "RequiredCols(0, 48, 56)('Product'[Brand], 'Sales'[RowNumber-GUID], 'Sales'[Quantity])"
        /// Returns: {"Brand", "RowNumber", "Quantity"}
        /// </summary>
        private HashSet<string> ExtractColumnsFromRequiredCols(string operation)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(operation)) return columns;

            // Match all 'Table'[Column] patterns
            var matches = Regex.Matches(operation, @"'[^']*'\[([^\]]+)\]");
            foreach (Match match in matches)
            {
                var colName = match.Groups[1].Value;
                // Strip GUID suffix from RowNumber columns (e.g., "RowNumber-2662979B-1795-...")
                var dashIdx = colName.IndexOf('-');
                if (dashIdx > 0 && colName.StartsWith("RowNumber", StringComparison.OrdinalIgnoreCase))
                {
                    colName = colName.Substring(0, dashIdx);
                }
                columns.Add(colName);
            }

            return columns;
        }

        /// <summary>
        /// Extracts column names from xmSQL SELECT clause.
        /// Handles both readable format: "SELECT 'Sales'[RowNumber] FROM 'Sales'"
        /// And raw format: "SELECT [Sales (28)].[RowNumber (123)] FROM [Sales (28)]"
        /// Returns: {"RowNumber"}
        /// </summary>
        private HashSet<string> ExtractColumnsFromXmSql(string xmSql)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(xmSql)) return columns;

            // Match all 'Table'[Column] patterns in SELECT (before FROM)
            var fromIdx = xmSql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
            var selectPart = fromIdx > 0 ? xmSql.Substring(0, fromIdx) : xmSql;

            // Pattern 1: Readable format - 'Table'[Column]
            var matches = Regex.Matches(selectPart, @"'[^']*'\[([^\]]+)\]");
            foreach (Match match in matches)
            {
                columns.Add(match.Groups[1].Value);
            }

            // Pattern 2: Raw format - [Table (ID)].[Column (ID)] or [Table].[Column]
            // Extract column name, stripping the (ID) suffix if present
            var rawMatches = Regex.Matches(selectPart, @"\[[^\]]+\]\s*\.\s*\[([^\]]+?)(?:\s*\(\d+\))?\]");
            foreach (Match match in rawMatches)
            {
                var colName = match.Groups[1].Value.Trim();
                // Strip (ID) suffix if present
                var parenIdx = colName.IndexOf('(');
                if (parenIdx > 0)
                {
                    colName = colName.Substring(0, parenIdx).Trim();
                }
                if (!string.IsNullOrEmpty(colName))
                {
                    columns.Add(colName);
                }
            }

            return columns;
        }

        /// <summary>
        /// Calculates the overlap between two sets of column names.
        /// </summary>
        private int CalculateColumnOverlap(HashSet<string> nodeColumns, HashSet<string> xmSqlColumns)
        {
            int overlap = 0;
            foreach (var xmCol in xmSqlColumns)
            {
                // Check for exact match or prefix match (for RowNumber columns)
                if (nodeColumns.Contains(xmCol))
                {
                    overlap++;
                }
                else if (xmCol.StartsWith("RowNumber", StringComparison.OrdinalIgnoreCase))
                {
                    // RowNumber in xmSQL matches RowNumber in plan (after GUID stripping)
                    if (nodeColumns.Any(nc => nc.StartsWith("RowNumber", StringComparison.OrdinalIgnoreCase)))
                    {
                        overlap++;
                    }
                }
            }
            return overlap;
        }

        /// <summary>
        /// Extracts the table name from an operation string.
        /// Examples:
        ///   "Scan_Vertipaq: ... ('Customer'[First Name]) ..." → "Customer"
        ///   "Spool_Iterator: ... IterCols(0)('Internet Sales'[Margin]) ..." → "Internet Sales"
        /// </summary>
        private string ExtractTableNameFromOperation(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return null;

            // Match 'TableName'[ColumnName] pattern
            var match = Regex.Match(operation, @"'([^']+)'\s*\[");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Extracts the table name from an xmSQL query's FROM clause.
        /// Handles both readable format: FROM 'Internet Sales'
        /// And raw format: FROM [Internet Sales (28)]
        /// </summary>
        private string ExtractTableNameFromXmSql(string xmSql)
        {
            if (string.IsNullOrEmpty(xmSql)) return null;

            // Pattern 1: Readable format - FROM 'TableName'
            var match = Regex.Match(xmSql, @"\bFROM\s+'([^']+)'", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Pattern 2: Raw format - FROM [TableName (ID)] or FROM [TableName]
            var rawMatch = Regex.Match(xmSql, @"\bFROM\s+\[([^\]]+?)(?:\s*\(\d+\))?\]", RegexOptions.IgnoreCase);
            if (rawMatch.Success)
            {
                var tableName = rawMatch.Groups[1].Value.Trim();
                // Strip (ID) suffix if present
                var parenIdx = tableName.IndexOf('(');
                if (parenIdx > 0)
                {
                    tableName = tableName.Substring(0, parenIdx).Trim();
                }
                return tableName;
            }

            return null;
        }

        /// <summary>
        /// Extracts table name from T-SQL query's FROM clause.
        /// Matches patterns like: FROM [Schema].[Table] or FROM [Table]
        /// </summary>
        private string ExtractTableNameFromTSql(string tSql)
        {
            if (string.IsNullOrEmpty(tSql)) return null;

            // Match FROM [Schema].[Table] or FROM [Table] pattern
            // Also handle "FROM [Schema].[Table] AS [alias]"
            var match = Regex.Match(tSql, @"\bFROM\s+(?:\[([^\]]+)\]\s*\.\s*)?\[([^\]]+)\]", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var schema = match.Groups[1].Value;
                var table = match.Groups[2].Value;
                // Return "Schema Table" (without dots) to match DAX naming like 'Sales SalesOrderDetail'
                if (!string.IsNullOrEmpty(schema))
                {
                    return $"{schema} {table}";
                }
                return table;
            }

            return null;
        }

        /// <summary>
        /// Extracts column names from T-SQL SELECT clause.
        /// Matches patterns like: SELECT [Column1], [Column2] FROM ...
        /// Also handles: [Schema].[Table].[Column] or [alias].[Column]
        /// </summary>
        private HashSet<string> ExtractColumnsFromTSql(string tSql)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(tSql)) return columns;

            // Get the SELECT part (before FROM)
            var fromIdx = tSql.IndexOf(" FROM ", StringComparison.OrdinalIgnoreCase);
            var selectPart = fromIdx > 0 ? tSql.Substring(0, fromIdx) : tSql;

            // Match [Column] patterns - could be standalone or after alias like [t0].[Column]
            // The column is always the last bracketed identifier before a comma or end
            var matches = Regex.Matches(selectPart, @"\[([^\]]+)\](?:\s*(?:,|$|\s+AS\s+))", RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var colName = match.Groups[1].Value;
                // Skip if it looks like a table alias (typically t0, t1, etc.)
                if (!Regex.IsMatch(colName, @"^t\d+$", RegexOptions.IgnoreCase))
                {
                    columns.Add(colName);
                }
            }

            // Also match the pattern [alias].[Column] to get column names
            var aliasColMatches = Regex.Matches(selectPart, @"\[[^\]]+\]\s*\.\s*\[([^\]]+)\]");
            foreach (Match match in aliasColMatches)
            {
                columns.Add(match.Groups[1].Value);
            }

            return columns;
        }

        private void CalculateCostPercentages(EnrichedQueryPlan plan)
        {
            // Calculate based on Records if timing not available
            var totalRecords = plan.AllNodes.Where(n => n.Records.HasValue).Sum(n => n.Records.Value);

            if (totalRecords > 0)
            {
                foreach (var node in plan.AllNodes.Where(n => n.Records.HasValue))
                {
                    node.CostPercentage = (double)node.Records.Value / totalRecords * 100;
                }
            }
        }

        private void AssignEngineTypes(List<EnrichedPlanNode> nodes)
        {
            foreach (var node in nodes)
            {
                // Skip if engine type is already set (e.g., from xmSQL correlation)
                if (node.EngineType != EngineType.Unknown)
                    continue;

                // Extract operator name from operation string
                var operatorName = ExtractOperatorName(node.Operation);
                if (string.IsNullOrEmpty(operatorName))
                    continue;

                // First, try to get engine type from dictionary (most accurate)
                var operatorInfo = DaxOperatorDictionary.GetOperatorInfo(operatorName);
                if (operatorInfo != null && operatorInfo.Engine != EngineType.Unknown)
                {
                    node.EngineType = operatorInfo.Engine;
                    continue;
                }

                // Fallback: pattern-based heuristic for operators not in dictionary
                // node.Operation is guaranteed non-null here (ExtractOperatorName would have returned null otherwise)
                var op = node.Operation;
                var opUpper = op.ToUpperInvariant();

                if (opUpper.Contains("DIRECTQUERY"))
                {
                    node.EngineType = EngineType.DirectQuery;
                }
                // Physical operator suffixes indicate Formula Engine (except VertipaqResult)
                // IterPhyOp, LookupPhyOp, SpoolPhyOp are all FE physical operators
                // The LogOp= reference to Vertipaq is the LOGICAL operation, not the physical
                else if (op.Contains("IterPhyOp") || op.Contains("LookupPhyOp") || op.Contains("SpoolPhyOp"))
                {
                    // VertipaqResult is the only SE physical operator with IterPhyOp suffix
                    if (operatorName == "VertipaqResult")
                    {
                        node.EngineType = EngineType.StorageEngine;
                    }
                    else
                    {
                        node.EngineType = EngineType.FormulaEngine;
                    }
                }
                // Logical operators with _Vertipaq suffix are Storage Engine
                else if (opUpper.Contains("_VERTIPAQ") && !opUpper.Contains("LOGOP="))
                {
                    node.EngineType = EngineType.StorageEngine;
                }
                else
                {
                    // Default to Formula Engine when operator type is unknown
                    // (most query plan operations are FE-coordinated)
                    node.EngineType = EngineType.FormulaEngine;
                }
            }
        }

        /// <summary>
        /// Extracts the operator name from an operation string.
        /// Handles formats like "Operator: details", "'Table'[Col]: Operator details", etc.
        /// </summary>
        private string ExtractOperatorName(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                return null;

            // Check if this is a column reference format: 'Table'[Column]: Operator
            if (operation.StartsWith("'"))
            {
                // Find the colon that separates the column reference from the operator
                int bracketDepth = 0;
                bool inQuote = false;

                for (int i = 0; i < operation.Length; i++)
                {
                    char c = operation[i];
                    if (c == '\'' && bracketDepth == 0)
                        inQuote = !inQuote;
                    else if (!inQuote)
                    {
                        if (c == '[') bracketDepth++;
                        else if (c == ']') bracketDepth--;
                        else if (c == ':' && bracketDepth == 0)
                        {
                            // Extract operator name after the colon
                            var afterColon = operation.Substring(i + 1).TrimStart();
                            var spaceIndex = afterColon.IndexOf(' ');
                            return spaceIndex > 0 ? afterColon.Substring(0, spaceIndex) : afterColon;
                        }
                    }
                }
                return null; // Column reference with no operator
            }

            // Standard format: "Operator: Details" or "Operator Details"
            // Also handle variable prefix pattern: "__DS0Core: Union: details"
            var colonIdx = operation.IndexOf(':');
            if (colonIdx > 0)
            {
                var firstSpace = operation.IndexOf(' ');
                if (firstSpace > 0 && firstSpace < colonIdx)
                    return operation.Substring(0, firstSpace);

                var beforeColon = operation.Substring(0, colonIdx);

                // Check for variable name pattern: __VarName or _VarName
                // These are variable names, not operators - look for real operator after second colon
                if (beforeColon.StartsWith("__") || (beforeColon.StartsWith("_") && !beforeColon.StartsWith("_Vertipaq")))
                {
                    var afterFirstColon = operation.Substring(colonIdx + 1).TrimStart();
                    var secondColonIdx = afterFirstColon.IndexOf(':');
                    if (secondColonIdx > 0)
                    {
                        // Extract the actual operator (between first and second colon)
                        return afterFirstColon.Substring(0, secondColonIdx).Trim();
                    }
                }

                return beforeColon;
            }

            // No colon, use first space
            var idx = operation.IndexOf(' ');
            return idx > 0 ? operation.Substring(0, idx) : operation;
        }

        /// <summary>
        /// Cross-references logical plan nodes with physical plan nodes to infer
        /// engine types and row counts for logical nodes that have no direct metrics.
        /// </summary>
        public void CrossReferenceLogicalWithPhysical(EnrichedQueryPlan logicalPlan, EnrichedQueryPlan physicalPlan)
        {
            if (logicalPlan?.AllNodes == null || physicalPlan?.AllNodes == null)
            {
                Log.Debug("PlanEnrichmentService: Cannot cross-reference - one or both plans are null");
                return;
            }

            Log.Debug("PlanEnrichmentService: Cross-referencing {LogicalCount} logical nodes with {PhysicalCount} physical nodes",
                logicalPlan.AllNodes.Count, physicalPlan.AllNodes.Count);

            // Build lookup of physical nodes by operation pattern
            var physicalByPattern = new Dictionary<string, EnrichedPlanNode>();
            foreach (var physNode in physicalPlan.AllNodes.Where(n => n.EngineType != EngineType.Unknown))
            {
                var key = ExtractOperatorKey(physNode.Operation);
                if (!string.IsNullOrEmpty(key) && !physicalByPattern.ContainsKey(key))
                {
                    physicalByPattern[key] = physNode;
                }
            }

            int matchedNodes = 0;
            foreach (var logicalNode in logicalPlan.AllNodes)
            {
                var key = ExtractOperatorKey(logicalNode.Operation);
                if (!string.IsNullOrEmpty(key) && physicalByPattern.TryGetValue(key, out var physicalNode))
                {
                    // Inherit engine type from matching physical node
                    if (logicalNode.EngineType == EngineType.Unknown)
                    {
                        logicalNode.EngineType = physicalNode.EngineType;
                    }

                    // Inherit row counts if logical has none
                    if ((!logicalNode.Records.HasValue || logicalNode.Records == 0) &&
                        physicalNode.Records.HasValue && physicalNode.Records > 0)
                    {
                        logicalNode.Records = physicalNode.Records;
                        logicalNode.RecordsSource = "Physical";
                    }

                    // Inherit timing data if available
                    if (!logicalNode.DurationMs.HasValue && physicalNode.DurationMs.HasValue)
                    {
                        logicalNode.DurationMs = physicalNode.DurationMs;
                        logicalNode.CpuTimeMs = physicalNode.CpuTimeMs;
                        logicalNode.Parallelism = physicalNode.Parallelism;
                    }

                    // Inherit xmSQL if available
                    if (string.IsNullOrEmpty(logicalNode.XmSql) && !string.IsNullOrEmpty(physicalNode.XmSql))
                    {
                        logicalNode.XmSql = physicalNode.XmSql;
                        logicalNode.ResolvedXmSql = physicalNode.ResolvedXmSql;
                    }

                    matchedNodes++;
                }
            }

            Log.Debug("PlanEnrichmentService: Cross-reference matched {MatchedCount} logical nodes with physical nodes",
                matchedNodes);
        }

        /// <summary>
        /// Extracts a key pattern from an operation string for matching between logical and physical plans.
        /// </summary>
        private string ExtractOperatorKey(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return string.Empty;

            // Extract key patterns: "Sum_Vertipaq", "Scan_Vertipaq", "GroupBy_Vertipaq", etc.
            var match = Regex.Match(operation, @"(\w+_Vertipaq|\w+LogOp|\w+PhyOp)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }

            // Fall back to first word before colon or space
            var colonIndex = operation.IndexOf(':');
            if (colonIndex > 0)
            {
                return operation.Substring(0, colonIndex).Trim().ToUpperInvariant();
            }

            var spaceIndex = operation.IndexOf(' ');
            if (spaceIndex > 0)
            {
                return operation.Substring(0, spaceIndex).Trim().ToUpperInvariant();
            }

            return operation.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Determines if an operation string represents a Storage Engine node.
        /// Only matches operators that START with a Vertipaq operator name,
        /// not those that reference a Vertipaq logical op via LogOp= in the middle.
        /// </summary>
        private static bool IsStorageEngineNode(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return false;

            // Extract the operator name (text before the first colon or space)
            var colonIdx = operation.IndexOf(':');
            var operatorPart = colonIdx > 0 ? operation.Substring(0, colonIdx) : operation;

            // Also check for space as separator (some operations don't have colon)
            var spaceIdx = operatorPart.IndexOf(' ');
            if (spaceIdx > 0)
            {
                operatorPart = operatorPart.Substring(0, spaceIdx);
            }

            // Now check if the operator name ends with _Vertipaq
            // This correctly identifies: "Scan_Vertipaq", "Sum_Vertipaq", etc.
            // But NOT: "Spool_Iterator<SpoolIterator>" even if LogOp=Scan_Vertipaq follows
            return operatorPart.EndsWith("_Vertipaq", StringComparison.OrdinalIgnoreCase) ||
                   operatorPart.Equals("VertipaqResult", StringComparison.OrdinalIgnoreCase);
        }
    }
}
