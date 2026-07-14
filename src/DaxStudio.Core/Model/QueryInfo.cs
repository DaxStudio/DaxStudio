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

            if (!(options != null && options.UseNewPreprocessor && TryPreProcessWithAntlrParser(queryText, eventAggregator)))
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
        public bool HasParameters { get { return Parameters.Count > 0; } }

        /// <summary>
        /// The text to record in the query-history pane. On the ANTLR pre-processor path this is the
        /// query body with the comment-script (<c>--&gt;</c>) commands still present (so history shows
        /// what the user typed), even though <see cref="ProcessedQuery"/> strips those command lines
        /// for execution. On the classic path it is simply <see cref="ProcessedQuery"/> (unchanged).
        /// </summary>
        public string HistoryText => _historyText ?? ProcessedQuery;
        //public string ProcessedQuery { get; set; }
        public bool NeedsParameterValues { get; set; }

        public string QueryWithMergedParameters
        {
            get
            {
                return DaxHelper.replaceParamsInQuery(ProcessedQuery, Parameters);
            }
        }
        public Dictionary<string,QueryParameter> Parameters { get { return _parameters; } }

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
                // The XMLA <Parameters> block is handled by the proven regex/XML code for both paths
                // (the grammar's XMLA tokens are case-sensitive), so only the query body is fed to ANTLR.
                DaxHelper.SplitParametersBlock(query, out var body, out var paramsBlock);

                var result = AntlrPreProcessor.Parse(body);
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

                // Record the body WITH the comment-script commands intact for the history pane
                // (the <Parameters> block has already been split off by SplitParametersBlock above).
                _historyText = body;

                // Discovered @name parameters -> populate so the parameter-values dialog still triggers.
                foreach (var name in result.DiscoveredParameters.Keys)
                {
                    if (!_parameters.ContainsKey(name))
                        _parameters.Add(name, new QueryParameter(name));
                }

                // Supplied values from an XMLA <Parameters> block (reuse the proven parser).
                if (paramsBlock.Length > 0)
                    DaxHelper.ParseParams(paramsBlock, _parameters, eventAggregator);

                // Execute the PARAMETER command subset: apply their supplied values.
                var hasParameterCommand = ApplyParameterCommands(result.Batches);

                var hasSuppliedValues = paramsBlock.Length > 0 || hasParameterCommand;
                NeedsParameterValues = _parameters.Count > 0 && !hasSuppliedValues;

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
                _parameters.Clear();
                return false;
            }
        }

        // Applies any PARAMETER commands produced by the new pre-processor to the parameter
        // dictionary. Returns true if at least one PARAMETER command supplied a value.
        private bool ApplyParameterCommands(IEnumerable<ScriptBatch> batches)
        {
            var applied = false;
            foreach (var batch in batches)
            {
                foreach (var cmd in batch.Commands.OfType<ParameterCommand>())
                {
                    // The comment-script PARAMETER command keeps the leading '@' on the name; strip it
                    // so it lines up with the discovered parameters and the substitution regex.
                    var name = cmd.ParameterName != null && cmd.ParameterName.StartsWith("@")
                        ? cmd.ParameterName.Substring(1)
                        : cmd.ParameterName;
                    _parameters[name] = new QueryParameter(name, cmd.Value, NormalizeTypeName(cmd.TypeName));
                    applied = true;
                }
            }
            return applied;
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
            }
            _scriptBatches.Clear();
            _scriptBatches.Add(batch);
        }
    }
}
