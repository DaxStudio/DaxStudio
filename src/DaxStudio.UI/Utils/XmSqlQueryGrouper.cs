using Antlr4.Runtime;
using DaxStudio.UI.Grammars.Generated;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// Groups xmSQL queries by structural similarity using ANTLR-based fingerprinting.
    /// Queries that share the same structure (columns, tables, joins, filter columns)
    /// but differ in filter values are grouped together.
    /// </summary>
    public class XmSqlQueryGrouper
    {
        /// <summary>
        /// Result of grouping a set of queries.
        /// </summary>
        public class GroupingResult
        {
            /// <summary>
            /// Groups based on full structural fingerprint (same SELECT + FROM + JOIN + WHERE columns).
            /// </summary>
            public List<XmSqlQueryGroup> StructuralGroups { get; set; } = new List<XmSqlQueryGroup>();

            /// <summary>
            /// Groups based on table access fingerprint (same FROM + JOIN + WHERE columns, ignoring SELECT).
            /// </summary>
            public List<XmSqlQueryGroup> TableAccessGroups { get; set; } = new List<XmSqlQueryGroup>();

            /// <summary>
            /// Mapping from query ID (RowNumber) to structural group ID.
            /// </summary>
            public Dictionary<int, int> QueryToStructuralGroup { get; set; } = new Dictionary<int, int>();

            /// <summary>
            /// Mapping from query ID (RowNumber) to table access group ID.
            /// </summary>
            public Dictionary<int, int> QueryToTableAccessGroup { get; set; } = new Dictionary<int, int>();

            /// <summary>
            /// Total number of queries processed.
            /// </summary>
            public int TotalQueries { get; set; }

            /// <summary>
            /// Number of queries that failed to fingerprint.
            /// </summary>
            public int FailedQueries { get; set; }
        }

        /// <summary>
        /// Groups a collection of xmSQL queries by structural similarity.
        /// </summary>
        /// <param name="queries">Tuples of (queryId, xmSqlText) to group.</param>
        /// <returns>Grouping results with both structural and table-access groups.</returns>
        public GroupingResult GroupQueries(IEnumerable<(int QueryId, string XmSql)> queries)
        {
            var result = new GroupingResult();
            var structuralBuckets = new Dictionary<string, List<(int QueryId, XmSqlQueryFingerprint Fingerprint)>>(StringComparer.OrdinalIgnoreCase);
            var accessBuckets = new Dictionary<string, List<(int QueryId, XmSqlQueryFingerprint Fingerprint)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (queryId, xmSql) in queries)
            {
                result.TotalQueries++;

                if (string.IsNullOrWhiteSpace(xmSql))
                {
                    result.FailedQueries++;
                    continue;
                }

                // Normalize query text: strip "Estimated size:" suffix which differs
                // between VertiPaqSEQueryEnd and VertiPaqSEQueryCacheMatch events
                var normalizedSql = NormalizeQueryText(xmSql);

                var fingerprint = ComputeFingerprint(normalizedSql);
                if (fingerprint == null)
                {
                    result.FailedQueries++;
                    continue;
                }

                // Bucket by structural hash
                if (!structuralBuckets.TryGetValue(fingerprint.FullStructuralHash, out var structList))
                {
                    structList = new List<(int, XmSqlQueryFingerprint)>();
                    structuralBuckets[fingerprint.FullStructuralHash] = structList;
                }
                structList.Add((queryId, fingerprint));

                // Bucket by table access hash
                if (!accessBuckets.TryGetValue(fingerprint.TableAccessHash, out var accessList))
                {
                    accessList = new List<(int, XmSqlQueryFingerprint)>();
                    accessBuckets[fingerprint.TableAccessHash] = accessList;
                }
                accessList.Add((queryId, fingerprint));
            }

            // Build structural groups (ordered by count descending)
            int structGroupId = 1;
            foreach (var bucket in structuralBuckets.OrderByDescending(b => b.Value.Count))
            {
                var group = new XmSqlQueryGroup
                {
                    GroupId = structGroupId,
                    FullStructuralHash = bucket.Key,
                    TableAccessHash = bucket.Value[0].Fingerprint.TableAccessHash,
                    Fingerprint = bucket.Value[0].Fingerprint,
                    MemberQueryIds = bucket.Value.Select(b => b.QueryId).ToList(),
                    GroupLabel = BuildGroupLabel(bucket.Value[0].Fingerprint)
                };

                result.StructuralGroups.Add(group);

                foreach (var member in bucket.Value)
                {
                    result.QueryToStructuralGroup[member.QueryId] = structGroupId;
                }

                structGroupId++;
            }

            // Build table access groups (ordered by count descending)
            int accessGroupId = 1;
            foreach (var bucket in accessBuckets.OrderByDescending(b => b.Value.Count))
            {
                var group = new XmSqlQueryGroup
                {
                    GroupId = accessGroupId,
                    FullStructuralHash = null,
                    TableAccessHash = bucket.Key,
                    Fingerprint = bucket.Value[0].Fingerprint,
                    MemberQueryIds = bucket.Value.Select(b => b.QueryId).ToList(),
                    GroupLabel = BuildAccessGroupLabel(bucket.Value[0].Fingerprint)
                };

                result.TableAccessGroups.Add(group);

                foreach (var member in bucket.Value)
                {
                    result.QueryToTableAccessGroup[member.QueryId] = accessGroupId;
                }

                accessGroupId++;
            }

            return result;
        }

        /// <summary>
        /// Determines the group type label for a structural group.
        /// </summary>
        /// <param name="groupResult">The full grouping result.</param>
        /// <param name="structuralGroupId">The structural group ID to classify.</param>
        /// <param name="queryTexts">Map of query ID to query text for identical-query detection.</param>
        /// <param name="visibleEventCount">Number of visible (non-cache, non-internal) events in this group.</param>
        /// <returns>A descriptive group type string.</returns>
        public static string DetermineGroupType(GroupingResult groupResult, int structuralGroupId, Dictionary<int, string> queryTexts, int visibleEventCount = -1)
        {
            // Check if all queries in this structural group are identical
            // (normalize by stripping "Estimated size:" suffix which differs between End and CacheMatch events)
            var memberIds = groupResult.StructuralGroups
                .FirstOrDefault(g => g.GroupId == structuralGroupId)?.MemberQueryIds;

            if (memberIds != null && memberIds.Count > 0)
            {
                var matchingMembers = memberIds.Where(id => queryTexts.ContainsKey(id)).ToList();
                var distinctQueries = matchingMembers
                    .Select(id => NormalizeQueryText(queryTexts[id]))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                // Use visible event count if provided, otherwise fall back to total members
                var effectiveCount = visibleEventCount >= 0 ? visibleEventCount : matchingMembers.Count;

                if (distinctQueries <= 1)
                    return effectiveCount <= 1 ? "Single query" : "Identical queries";
            }

            // Check if multiple structural groups share the same table access group
            var tableAccessGroupMembers = new Dictionary<int, HashSet<int>>();
            foreach (var structGroup in groupResult.StructuralGroups)
            {
                foreach (var queryId in structGroup.MemberQueryIds)
                {
                    if (groupResult.QueryToTableAccessGroup.TryGetValue(queryId, out int accessId))
                    {
                        if (!tableAccessGroupMembers.TryGetValue(accessId, out var structIds))
                        {
                            structIds = new HashSet<int>();
                            tableAccessGroupMembers[accessId] = structIds;
                        }
                        structIds.Add(structGroup.GroupId);
                    }
                }
            }

            // Find how many sibling structural groups share our table access group
            foreach (var accessGroup in tableAccessGroupMembers.Values)
            {
                if (accessGroup.Contains(structuralGroupId) && accessGroup.Count > 1)
                    return "Similar structure, different SELECT columns";
            }

            return "Same structure, different filter values";
        }

        private static readonly Regex EstimatedSizeRegex = new Regex(@"Estimated size:.*$", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Normalizes query text for identity comparison by stripping the 
        /// "Estimated size:" suffix (present on QueryEnd but not CacheMatch events).
        /// </summary>
        internal static string NormalizeQueryText(string query)
        {
            if (string.IsNullOrEmpty(query)) return string.Empty;
            return EstimatedSizeRegex.Replace(query, "").Trim();
        }

        /// <summary>
        /// Computes a fingerprint for a single xmSQL query string.
        /// </summary>
        internal XmSqlQueryFingerprint ComputeFingerprint(string xmSql)
        {
            try
            {
                var inputStream = new AntlrInputStream(xmSql);
                var lexer = new xmSQLLexer(inputStream);
                lexer.RemoveErrorListeners();
                lexer.AddErrorListener(new SilentLexerErrorListener());

                var tokenStream = new CommonTokenStream(lexer);
                var parser = new xmSQLParser(tokenStream);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new SilentParserErrorListener());

                var tree = parser.query();

                var visitor = new XmSqlFingerprintVisitor();
                return visitor.ComputeFingerprint(tree);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to compute fingerprint for xmSQL query");
                return null;
            }
        }

        private static string BuildGroupLabel(XmSqlQueryFingerprint fp)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(fp.SelectSignature))
                parts.Add($"SELECT [{Truncate(fp.SelectSignature, 60)}]");

            if (!string.IsNullOrEmpty(fp.FromJoinSignature))
                parts.Add($"FROM {Truncate(fp.FromJoinSignature, 40)}");

            if (!string.IsNullOrEmpty(fp.WhereColumnsSignature))
                parts.Add($"WHERE [{Truncate(fp.WhereColumnsSignature, 60)}]");

            return string.Join(" ", parts);
        }

        private static string BuildAccessGroupLabel(XmSqlQueryFingerprint fp)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(fp.FromJoinSignature))
                parts.Add($"FROM {Truncate(fp.FromJoinSignature, 40)}");

            if (!string.IsNullOrEmpty(fp.WhereColumnsSignature))
                parts.Add($"WHERE [{Truncate(fp.WhereColumnsSignature, 80)}]");

            return string.Join(" ", parts);
        }

        private static string Truncate(string s, int maxLen)
        {
            if (s == null) return "";
            return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
        }

        /// <summary>Silent error listener for ANTLR lexer errors.</summary>
        private class SilentLexerErrorListener : IAntlrErrorListener<int>
        {
            public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol,
                int line, int charPositionInLine, string msg, RecognitionException e) { }
        }

        /// <summary>Silent error listener for ANTLR parser errors.</summary>
        private class SilentParserErrorListener : IAntlrErrorListener<IToken>
        {
            public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol,
                int line, int charPositionInLine, string msg, RecognitionException e) { }
        }
    }
}
