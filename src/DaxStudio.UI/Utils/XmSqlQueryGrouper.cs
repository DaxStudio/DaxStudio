using Antlr4.Runtime;
using DaxStudio.UI.Grammars.Generated;
using DaxStudio.UI.Model;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

                var fingerprint = ComputeFingerprint(xmSql);
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
