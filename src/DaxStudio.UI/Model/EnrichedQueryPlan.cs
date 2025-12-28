using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Represents a complete enriched execution plan with aggregated metrics.
    /// </summary>
    public class EnrichedQueryPlan
    {
        /// <summary>
        /// File format version for serialization compatibility.
        /// </summary>
        public int FileFormatVersion => 4;

        /// <summary>
        /// Trace correlation identifier.
        /// </summary>
        public string ActivityID { get; set; }

        /// <summary>
        /// Request correlation identifier.
        /// </summary>
        public string RequestID { get; set; }

        /// <summary>
        /// Original DAX query text.
        /// </summary>
        public string QueryText { get; set; }

        /// <summary>
        /// Query parameters.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Query start timestamp.
        /// </summary>
        public DateTime StartDateTime { get; set; }

        /// <summary>
        /// Whether this is a Physical or Logical plan.
        /// </summary>
        public PlanType PlanType { get; set; }

        /// <summary>
        /// Total query duration in milliseconds.
        /// </summary>
        public long TotalDurationMs { get; set; }

        /// <summary>
        /// Storage Engine time component in milliseconds.
        /// </summary>
        public long StorageEngineDurationMs { get; set; }

        /// <summary>
        /// Formula Engine time component in milliseconds.
        /// </summary>
        public long FormulaEngineDurationMs { get; set; }

        /// <summary>
        /// Storage Engine CPU time in milliseconds.
        /// </summary>
        public long StorageEngineCpuMs { get; set; }

        /// <summary>
        /// Number of VertiPaq cache hits.
        /// </summary>
        public int CacheHits { get; set; }

        /// <summary>
        /// Number of Storage Engine queries executed.
        /// </summary>
        public int StorageEngineQueryCount { get; set; }

        /// <summary>
        /// DirectQuery time component in milliseconds.
        /// </summary>
        public long DirectQueryDurationMs { get; set; }

        /// <summary>
        /// Number of DirectQuery queries executed.
        /// </summary>
        public int DirectQueryCount { get; set; }

        /// <summary>
        /// Root node of the plan tree.
        /// </summary>
        public EnrichedPlanNode RootNode { get; set; }

        /// <summary>
        /// Flattened list of all nodes for efficient iteration.
        /// </summary>
        public List<EnrichedPlanNode> AllNodes { get; set; } = new List<EnrichedPlanNode>();

        /// <summary>
        /// All performance issues detected across all nodes.
        /// </summary>
        public List<PerformanceIssue> Issues { get; set; } = new List<PerformanceIssue>();

        /// <summary>
        /// Current state in the enrichment pipeline.
        /// </summary>
        public PlanState State { get; set; } = PlanState.Raw;

        /// <summary>
        /// Error message if State is Error.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Total number of nodes in the plan.
        /// </summary>
        public int NodeCount => AllNodes?.Count ?? 0;

        /// <summary>
        /// Total number of issues detected.
        /// </summary>
        public int IssueCount => Issues?.Count ?? 0;

        /// <summary>
        /// Number of warning-level issues.
        /// </summary>
        public int WarningCount => Issues?.Count(i => i.Severity == IssueSeverity.Warning) ?? 0;

        /// <summary>
        /// Number of error-level issues.
        /// </summary>
        public int ErrorCount => Issues?.Count(i => i.Severity == IssueSeverity.Error) ?? 0;

        /// <summary>
        /// Whether this plan has any issues.
        /// </summary>
        public bool HasIssues => IssueCount > 0;

        /// <summary>
        /// Formatted SE/FE/DQ time split for display.
        /// </summary>
        public string DisplayTimeSplit =>
            DirectQueryDurationMs > 0
                ? $"SE: {StorageEngineDurationMs:N0} ms | DQ: {DirectQueryDurationMs:N0} ms | FE: {FormulaEngineDurationMs:N0} ms"
                : $"SE: {StorageEngineDurationMs:N0} ms | FE: {FormulaEngineDurationMs:N0} ms";

        /// <summary>
        /// Find a node by its NodeId.
        /// </summary>
        public EnrichedPlanNode FindNodeById(int nodeId)
        {
            return AllNodes?.FirstOrDefault(n => n.NodeId == nodeId);
        }

        /// <summary>
        /// Get all issues of a specific type.
        /// </summary>
        public IEnumerable<PerformanceIssue> GetIssuesByType(IssueType issueType)
        {
            return Issues?.Where(i => i.IssueType == issueType) ?? Enumerable.Empty<PerformanceIssue>();
        }
    }

    /// <summary>
    /// State of the plan in the enrichment pipeline.
    /// </summary>
    public enum PlanState
    {
        /// <summary>
        /// Raw plan data received.
        /// </summary>
        Raw,

        /// <summary>
        /// Parsed into tree structure.
        /// </summary>
        Parsed,

        /// <summary>
        /// Enriched with timing, column names, and issue detection.
        /// </summary>
        Enriched,

        /// <summary>
        /// Layout positions calculated, ready for rendering.
        /// </summary>
        LayoutComplete,

        /// <summary>
        /// An error occurred during enrichment.
        /// </summary>
        Error
    }
}
