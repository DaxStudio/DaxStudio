using Caliburn.Micro;
using DaxStudio.Core.Events;
using DaxStudio.Core.Utils;
using DaxStudio.Interfaces;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.PreProcessor;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Core.Model
{
    public class QueryInfo
    {
        private string rawQuery;
        private Dictionary<string,QueryParameter> _parameters;
        private readonly List<Dictionary<string, QueryParameter>> _batchParameters =
            new List<Dictionary<string, QueryParameter>>();
        // Set (non-null) only when the ANTLR pre-processor path has successfully produced the
        // processed query text. When null the classic regex behaviour is used.
        private string _newProcessedQuery;
        // Set (non-null) only on the ANTLR pre-processor path: the query body (with any comment-script
        // "-->" command lines still intact, but with the &lt;Parameters&gt; XML block removed) that should
        // be recorded in the query-history pane. The executable ProcessedQuery strips the "-->" lines,
        // so a separate value is kept so history still shows the commands the user typed.
        private string _historyText;
        private readonly List<ScriptBatch> _scriptBatches = new List<ScriptBatch>();

        public QueryInfo(string queryText, IEventAggregator eventAggregator)
            : this(queryText, eventAggregator, null)
        {
        }

        public QueryInfo(string queryText, IEventAggregator eventAggregator, IGlobalOptions options)
        {
            NeedsParameterValues = true;
            rawQuery = queryText;
            _parameters = new Dictionary<string, QueryParameter>(StringComparer.OrdinalIgnoreCase );

            if (!(options != null && options.UseNewDaxParser && TryPreProcessWithAntlrParser(queryText, eventAggregator)))
            {
                DaxHelper.PreProcessQuery(this, rawQuery, eventAggregator);
                BuildBatchesFromRegexResult();
            }
        }
        public string ProcessedQuery { get {
                if (_newProcessedQuery != null) return _newProcessedQuery;

                var baseQuery = string.Empty;
                if (HasParameters) {
                    baseQuery += QueryText; 
                } else {
                    baseQuery += rawQuery;
                }

                return baseQuery;
            }
        }

        public string QueryText { get; set; }
        public bool HasParameters { get { return Parameters.Count > 0 || _batchParameters.Any(p => p.Count > 0); } }

        /// <summary>
        /// The text to record in the query-history pane. On the ANTLR pre-processor path this is the
        /// query body with the comment-script (<c>--&gt;</c>) commands still present (so history shows
        /// what the user typed), even though <see cref="ProcessedQuery"/> strips those command lines
        /// for execution. On the classic path it is simply <see cref="ProcessedQuery"/> (unchanged).
        /// </summary>
        public string HistoryText => _historyText ?? ProcessedQuery;
        //public string ProcessedQuery { get; set; }
        public bool NeedsParameterValues { get; set; }

        /// <summary>
        /// A helpful message describing a malformed comment-script (<c>--&gt;</c>) command (e.g. a
        /// USE with no database name). Non-null when the new pre-processor found such a command error;
        /// the query run is aborted and this message is surfaced to the user rather than silently
        /// falling back to the classic pre-processor (which would ignore the command).
        /// </summary>
        public string PreProcessError { get; private set; }

        /// <summary>The 1-based line of the command that caused <see cref="PreProcessError"/> (0 when unknown).</summary>
        public int PreProcessErrorLine { get; private set; }

        /// <summary>The 0-based character position of the command that caused <see cref="PreProcessError"/> (0 when unknown).</summary>
        public int PreProcessErrorColumn { get; private set; }

        public string QueryWithMergedParameters
        {
            get
            {
                return DaxHelper.replaceParamsInQuery(ProcessedQuery, Parameters);
            }
        }
        public Dictionary<string,QueryParameter> Parameters { get { return _parameters; } }

        public IReadOnlyDictionary<string, QueryParameter> GetBatchParameters(int batchIndex)
        {
            if (batchIndex < 0 || batchIndex >= _batchParameters.Count)
                throw new ArgumentOutOfRangeException(nameof(batchIndex));

            return _batchParameters[batchIndex];
        }

        public bool BatchNeedsParameterValues(int batchIndex)
        {
            return GetBatchParameters(batchIndex).Values.Any(p => p.Value == null);
        }

        /// <summary>
        /// The batch structures produced by the pre-processor. Both the classic (regex) and the
        /// new (ANTLR) pre-processors populate this so downstream consumers can be uniform.
        /// </summary>
        public IReadOnlyList<ScriptBatch> ScriptBatches => _scriptBatches;

        // Runs the new grammar-based pre-processor. Returns false (so the caller falls back to the
        // classic path) if the query cannot be parsed or an unexpected error occurs, so the preview
        // feature can never block a query from running.
        private bool TryPreProcessWithAntlrParser(string query, IEventAggregator eventAggregator)
        {
            try
            {
                var result = AntlrPreProcessor.Parse(query);

                // A malformed comment-script command (e.g. "--> USE" with no database) is a mistake in
                // an explicit command the user typed. Surface it as a hard, user-facing error and do NOT
                // fall back to the classic pre-processor (which would silently ignore the command). The
                // caller (DocumentViewModel.PreProcessQuery) checks PreProcessError to abort the run.
                if (result.HasCommandErrors)
                {
                    var cmdErr = result.CommandErrors.First();
                    PreProcessError = cmdErr.Msg;
                    PreProcessErrorLine = cmdErr.Line;
                    PreProcessErrorColumn = cmdErr.Column;
                    Log.Warning("{class} {method} Comment-script command error: {msg}",
                        nameof(QueryInfo), nameof(TryPreProcessWithAntlrParser), cmdErr.Msg);
                    // Return true so the constructor keeps the new path (no regex fallback); the run is
                    // aborted upstream once PreProcessError is observed.
                    return true;
                }

                if (result.HasErrors)
                {
                    var first = result.Errors.FirstOrDefault();
                    Log.Warning("{class} {method} New pre-processor reported {count} error(s); falling back to the regex pre-processor. First error: {msg}",
                        nameof(QueryInfo), nameof(TryPreProcessWithAntlrParser), result.Errors.Count, first?.Msg);
                    eventAggregator?.PublishAsync(new OutputMessage(MessageType.Warning,
                        $"The new query pre-processor could not parse the query ({first?.Msg}). Falling back to the standard pre-processor."));
                    return false;
                }

                _scriptBatches.Clear();
                _scriptBatches.AddRange(result.Batches);

                // Use the whitespace-preserving processed text (original DAX minus comment-script
                // command lines). The listener's batch Output cannot be used here because whitespace
                // is lexed on a hidden channel and would be stripped, producing invalid DAX.
                var processed = result.ProcessedText;
                _newProcessedQuery = processed;
                QueryText = processed;

                // Keep comment commands in history, but exclude batch-local XMLA parameter blocks.
                _historyText = result.HistoryText;

                BuildBatchParameters(result.Batches, eventAggregator);

                // Preserve the existing single-batch dialog/API surface. Multi-batch scripts prompt
                // independently immediately before each batch executes.
                if (_batchParameters.Count == 1)
                    _parameters = _batchParameters[0];
                else
                    _parameters.Clear();

                NeedsParameterValues = _batchParameters.Count == 1 && BatchNeedsParameterValues(0);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{class} {method} Unexpected error in the new pre-processor; falling back to the regex pre-processor",
                    nameof(QueryInfo), nameof(TryPreProcessWithAntlrParser));
                eventAggregator?.PublishAsync(new OutputMessage(MessageType.Warning,
                    "The new query pre-processor failed unexpectedly. Falling back to the standard pre-processor."));
                // Reset any partial state before the caller retries with the classic path.
                _newProcessedQuery = null;
                _historyText = null;
                _scriptBatches.Clear();
                _batchParameters.Clear();
                _parameters.Clear();
                return false;
            }
        }

        private void BuildBatchParameters(IEnumerable<ScriptBatch> batches, IEventAggregator eventAggregator)
        {
            _batchParameters.Clear();
            foreach (var batch in batches)
            {
                var parameters = new Dictionary<string, QueryParameter>(StringComparer.OrdinalIgnoreCase);
                foreach (var name in batch.DiscoveredParameters.Keys)
                    parameters.Add(name, new QueryParameter(name));

                if (!string.IsNullOrWhiteSpace(batch.ParameterXml))
                    DaxHelper.ParseParams(batch.ParameterXml, parameters, eventAggregator);

                foreach (var cmd in batch.Commands.OfType<ParameterCommand>())
                {
                    var name = cmd.ParameterName != null && cmd.ParameterName.StartsWith("@")
                        ? cmd.ParameterName.Substring(1)
                        : cmd.ParameterName;
                    parameters[name] = new QueryParameter(name, cmd.Value, NormalizeTypeName(cmd.TypeName));
                }

                _batchParameters.Add(parameters);
            }
        }

        // Maps the comment-script type names to the xsd type names understood by QueryParameter.
        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "xsd:string";
            if (typeName.StartsWith("xsd:", StringComparison.OrdinalIgnoreCase)) return typeName;

            switch (typeName.ToUpperInvariant())
            {
                case "INTEGER":
                case "INT":
                case "LONG": return "xsd:long";
                case "DOUBLE":
                case "DECIMAL":
                case "REAL": return "xsd:double";
                case "BOOLEAN":
                case "BOOL": return "xsd:boolean";
                case "DATETIME":
                case "DATE": return "xsd:dateTime";
                default: return "xsd:string";
            }
        }

        // Wraps the classic regex pre-processor output into a single batch so both paths expose
        // the same ScriptBatch structure via ScriptBatches.
        private void BuildBatchesFromRegexResult()
        {
            var batch = new ScriptBatch();
            batch.Output.Append(QueryText ?? rawQuery ?? string.Empty);
            batch.QueryText = ProcessedQuery ?? string.Empty;
            foreach (var p in _parameters.Values)
            {
                batch.Commands.Add(new ParameterCommand(p.Name, p.Value, p.TypeName));
                batch.DiscoveredParameters[p.Name] = false;
            }
            _scriptBatches.Clear();
            _scriptBatches.Add(batch);
            _batchParameters.Clear();
            _batchParameters.Add(_parameters);
        }
    }
}
