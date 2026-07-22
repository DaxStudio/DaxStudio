using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.CommentScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static DaxStudio.Parsers.Grammars.Generated.PreProcessorParser;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{

    public class PreProcessorListener : PreProcessorParserBaseListener
    {
        
        private Dictionary<string, List<string>> _arrayParameters;
        private ScriptBatch _currentBatch;
        private List<ScriptBatch> _scriptBatches;
        public PreProcessorListener( Dictionary<string,List<string>> arrayParameters, List<ScriptBatch> scriptBatchs)
        {
            
            _arrayParameters = arrayParameters;
            _scriptBatches = scriptBatchs;
            _currentBatch = new ScriptBatch();
            _scriptBatches.Add(_currentBatch);
        }


        public override void EnterOther([NotNull] PreProcessorParser.OtherContext context)
        {
            _currentBatch.Output.Append(context.GetText());
            base.EnterOther(context);
        }

        public override void ExitDaxParameter([NotNull] DaxParameterContext context)
        {
            // capture parameter
            var paramName = context.children[0].GetText();
            if (!_arrayParameters.ContainsKey(paramName))
            {
                _arrayParameters.Add(paramName, new List<string>());
            }
            // add it to the output
            _currentBatch.Output.Append(context.GetText());
        }

        #region RSCustomDaxFilter Methods
        public override void ExitRSCustomDaxFilter([NotNull] PreProcessorParser.RSCustomDaxFilterContext context)
        {
            var functionContext = context.children[0];
            var customDaxFilterParts = (PreProcessorParser.RscustomdaxfilterContext)functionContext.Payload;
            var parameter = customDaxFilterParts.children[2].GetText();
            var table = customDaxFilterParts.children[6].GetText();
            var column = customDaxFilterParts.children[8].GetText();
            var condition = customDaxFilterParts.children[4].GetText();
            var datatype = customDaxFilterParts.children[10].GetText();

            var daxColumn = $"'{table}'[{column}]";

            var predicate = GeneratePredicate(daxColumn, parameter, condition, datatype == "String");

            var output = _currentBatch.Output;

            output.Append("FILTER(ALL(");
            output.Append(daxColumn);
            output.Append("),");
            output.Append(predicate);
            output.Append(')');

            base.ExitRSCustomDaxFilter(context);
        }

        public override void ExitRdlcustomdaxparameter([NotNull] RdlcustomdaxparameterContext context)
        {
            var functionContext = context.children[0];
            //var customDaxparameterParts = (PreProcessorParser.RdlcustomdaxparameterContext)functionContext.Payload;
            var parameter = context.children[2].GetText();
            var datatype = context.children[4].GetText();

            

            if (!_arrayParameters.ContainsKey(parameter)) throw new ArgumentException($"The parameter {parameter} was not supplied with any values");

            var values = _arrayParameters[parameter];

            // generate a comma separated list of values
            var result = values.Select(v => $"{(datatype == "String" ? $"\"{v.Replace("\"", "\"\"")}\"" : v)}").Aggregate(string.Empty, (current, next) => { return string.IsNullOrEmpty(current) ? next : $"{current},{next}"; });

            var output = _currentBatch.Output;
            output.Append(result);
            
            base.ExitRdlcustomdaxparameter(context);

        }

        private string GeneratePredicate(string daxColumn, string parameter, string condition, bool isString)
        {
            var equivalence = "=";
            var logicalOperator = "||";
            if (condition != "EqualToCondition")
            {
                equivalence = "<>";
                logicalOperator = "&&";
            }

            if (!_arrayParameters.ContainsKey(parameter)) throw new ArgumentException($"The parameter {parameter} was not supplied with any values");

            var values = _arrayParameters[parameter];

            return values.Select(v => $"{daxColumn} {equivalence} {(isString ? $"\"{v.Replace("\"", "\"\"")}\"" : v)}").Aggregate(string.Empty, (current, next) => { return string.IsNullOrEmpty(current) ? next : $"{current} {logicalOperator} {next}"; });
        }

        #endregion

        #region CommentScript Methods

        public override void ExitUse([NotNull] PreProcessorParser.UseContext context)
        {
            if (context.ChildCount != 2) throw new CommentScriptCommandException("Invalid number of arguments for USE command. This command should be in the form of: '--> USE [\"]<DatabaseName>[\"]'", context.Start.Line, context.Start.Column);

            var database = GetCommandValueText(context.children[1]);
            var cmd = new UseCommand(database);
            _currentBatch.Commands.Add(cmd);
            OutputCommand( _currentBatch.Output, context);
            base.ExitUse(context);
        }

        public override void ExitConnect([NotNull] PreProcessorParser.ConnectContext context)
        {
            if (context.ChildCount != 3) throw new CommentScriptCommandException("Invalid number of arguments for CONNECT command. This command should be in the form of: '--> CONNECT <ConnectionType> <ConnectionName>'", context.Start.Line, context.Start.Column);

            var serverType = context.children[1].GetText();
            var serverName = GetCommandValueText(context.children[2]);

            var cmd = new ConnectCommand(serverType, serverName);
            _currentBatch.Commands.Add(cmd);
            OutputCommand( _currentBatch.Output, context);
            base.ExitConnect(context);
        }

        // Returns the text of a CONNECT/USE value node. A quoted value is a CS_STRING_LITERAL terminal
        // whose text the lexer has already un-quoted, so its GetText() is used directly. An unquoted
        // value is the 'unquoted_value' rule, which may span several tokens (e.g. "AW Internet Sales");
        // because the lexer skips the whitespace between those tokens, the original source text - with
        // its internal spaces preserved - is recovered from the node's char interval rather than by
        // concatenating the child tokens.
        private static string GetCommandValueText(IParseTree node)
        {
            if (node is ParserRuleContext ruleCtx && ruleCtx.Start != null && ruleCtx.Stop != null)
            {
                var interval = new Interval(ruleCtx.Start.StartIndex, ruleCtx.Stop.StopIndex);
                return ruleCtx.Start.InputStream.GetText(interval);
            }
            return node.GetText();
        }

        public override void ExitScript_parameter([NotNull] PreProcessorParser.Script_parameterContext context)
        {
            var typename = "String";
            object value = null;
            string name = string.Empty;

            var node2 = context.children[1] as ITerminalNode;
            var node3 = context.children[2] as ITerminalNode;
            if (node2?.Symbol.Type == CS_PARAMETER)
            {
                name = context.children[1].GetText();
                value = GetParameterValue( context.children[3]);
            }
            if (node3?.Symbol.Type == CS_PARAMETER)
            {
                typename = context.children[1].GetText();
                name = context.children[2].GetText();
                value = GetParameterValue(context.children[4]);
            }
            
            if (value is List<string> arr)
            {
                _arrayParameters.Add(name, arr);
            }
            else
            { 
                _arrayParameters.Add(name, new List<string>() { value as string }); 
            }

            var cmd = new ParameterCommand(name, value, typename);
            _currentBatch.Commands.Add(cmd);

            OutputCommand( _currentBatch.Output, context);
            base.ExitScript_parameter(context);
        }

        public override void ExitSet_variable([NotNull] PreProcessorParser.Set_variableContext context)
        {
            // "--> SET <name> = <value>". children: [0]=SET keyword, [1]=name (CS_IDENTIFIER),
            // [2]='=', [3]=value. GetText() on a CS_STRING_LITERAL terminal returns the value already
            // un-quoted by the lexer; identifiers/integers/reals return their literal text. Any $(...)
            // references in the raw value are left intact and expanded at run time by
            // ScriptVariableExpander (eager/capture-time semantics).
            if (context.ChildCount != 4)
                throw new CommentScriptCommandException("Invalid SET command. This command should be in the form of: '--> SET <name> = <value>'", context.Start.Line, context.Start.Column);

            var name = context.children[1].GetText();
            var rawValue = context.children[3].GetText();

            var cmd = new VariableCommand(name, rawValue);
            _currentBatch.Commands.Add(cmd);

            OutputCommand( _currentBatch.Output, context);
            base.ExitSet_variable(context);
        }

        public override void ExitSaveas([NotNull] PreProcessorParser.SaveasContext context)
        {
            // "--> SAVEAS <filename>" where the filename may be quoted ("out\report.daxx") or an
            // unquoted path. The quoted form is a single CS_STRING_LITERAL terminal (already un-quoted
            // by the lexer); the unquoted form is the 'unquoted_value' rule whose original source text
            // is recovered from its char interval (so any $(...) references survive intact for run-time
            // expansion).
            if (context.ChildCount != 2)
                throw new CommentScriptCommandException("Invalid SAVEAS command. This command should be in the form of: '--> SAVEAS [\"]<FileName>[\"]'", context.Start.Line, context.Start.Column);

            var fileName = GetCommandValueText(context.children[1]);
            var cmd = new SaveAsCommand(fileName);
            _currentBatch.Commands.Add(cmd);
            OutputCommand( _currentBatch.Output, context);
            base.ExitSaveas(context);
        }

        private object GetParameterValue(IParseTree parseTree)
        {
            if (parseTree is PreProcessorParser.Parameter_array_valuesContext arr)
            {
                var arrResult = new List<string>();
                for (int i = 1;i< arr.ChildCount; i+=2)
                {
                    var value = arr.children[i];

                        arrResult.Add( value.GetText());

                }
                return arrResult;
            }

            return parseTree.GetText();

        }

        public override void ExitTest([NotNull] TestContext context)
        {
            // "--> TEST <name>" where the name may be quoted ("Sales Measure") or unquoted
            // (Sales Measure). The quoted form is a single CS_STRING_LITERAL terminal (already
            // un-quoted by the lexer); the unquoted form is the 'unquoted_value' rule whose original
            // source text - internal spaces preserved - is recovered from its char interval.
            if (context.ChildCount != 2)
                throw new CommentScriptCommandException("Invalid TEST command. This command should be in the form of: '--> TEST [\"]<TestName>[\"]'", context.Start.Line, context.Start.Column);

            var name = GetCommandValueText(context.children[1]);
            var cmd = new TestCommand(name);
            _currentBatch.Commands.Add(cmd);
            OutputCommand( _currentBatch.Output, context);
            base.ExitTest(context);
        }

        public override void ExitAssert([NotNull] AssertContext context)
        {
            int integerValue = 0;
            double doubleValue = 0.0;
            var property = string.Empty;
            var comparison = string.Empty;

            var valueNode = context.children[3] as ITerminalNode;

            property = context.children[1].GetText();
            comparison = context.children[2].GetText();

            if (valueNode.Symbol.Type == CS_INTEGER_LITERAL) { 
                integerValue = int.Parse(context.children[3].GetText());
            }
            if (valueNode.Symbol.Type == CS_REAL_LITERAL)
            {
                doubleValue = double.Parse(context.children[3].GetText());
            }

            var cmd = new AssertCommand(property, comparison, integerValue , doubleValue);
            _currentBatch.Commands.Add(cmd);
            OutputCommand( _currentBatch.Output, context);
            base.ExitAssert(context);
        }

        public override void ExitAssert_rowcount([NotNull] Assert_rowcountContext context)
        {
            var comparison = context.children[2].GetText();
            var value = int.Parse(context.children[3].GetText());

            var cmd = new AssertRowcountCommand(comparison, value);
            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitAssert_rowcount(context);
        }

        public override void ExitAssert_table_header([NotNull] Assert_table_headerContext context)
        {
            var mode = AssertTableMode.Ordered;
            if (context.ChildCount > 2)
            {
                var modeNode = context.children[2] as ITerminalNode;
                if (modeNode?.Symbol.Type == CS_UNORDERED) mode = AssertTableMode.Unordered;
                else if (modeNode?.Symbol.Type == CS_PARTIAL) mode = AssertTableMode.Partial;
            }

            var cmd = new AssertTableCommand(mode)
            {
                Line = context.Start.Line,
                Column = context.Start.Column,
            };

            var fileCtx = context.assert_table_file();
            if (fileCtx != null)
            {
                cmd.Format = MapAssertTableFormat(fileCtx);
                // CS_STRING_LITERAL is already unquoted/unescaped by the lexer action.
                cmd.FilePath = fileCtx.CS_STRING_LITERAL().GetText();
            }

            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitAssert_table_header(context);
        }

        private static AssertTableFormat MapAssertTableFormat(Assert_table_fileContext fileCtx)
        {
            if (fileCtx.CS_CSV() != null) return AssertTableFormat.Csv;
            if (fileCtx.CS_TXT() != null) return AssertTableFormat.Txt;
            if (fileCtx.CS_MD() != null) return AssertTableFormat.Md;
            if (fileCtx.CS_PARQUET() != null) return AssertTableFormat.Parquet;
            return AssertTableFormat.Inline;
        }

        public override void ExitTable_data_row([NotNull] Table_data_rowContext context)
        {
            var assertTableCmd = FindCurrentAssertTable();
            if (assertTableCmd == null) return; // orphan row - reported in ExitTable_row

            if (assertTableCmd.Format != AssertTableFormat.Inline)
            {
                throw new CommentScriptCommandException(
                    "'--> ASSERT TABLE' cannot combine inline '-->>' rows with a file (CSV/TXT/MD/PARQUET). Use one or the other.",
                    context.Start.Line, context.Start.Column);
            }

            var cells = new List<string>();
            foreach (var child in context.children)
            {
                if (child is Table_cellContext cellCtx)
                {
                    var cellText = string.Join(" ",
                        cellCtx.children?.Select(c => c.GetText()) ?? Enumerable.Empty<string>());
                    cells.Add(cellText);
                }
            }

            assertTableCmd.AddRow(cells.ToArray());

            base.ExitTable_data_row(context);
        }

        /// <summary>
        /// Every comment-script table row ("--&gt;&gt; | ... |") must belong to a preceding
        /// "--&gt; ASSERT TABLE" command in the same batch. A run of "--&gt;&gt;" rows with no leading
        /// ASSERT TABLE is a user mistake and is surfaced as a command error rather than silently ignored.
        /// </summary>
        public override void ExitTable_row([NotNull] Table_rowContext context)
        {
            if (FindCurrentAssertTable() == null)
            {
                throw new CommentScriptCommandException(
                    "Table rows ('-->>') must be preceded by an '--> ASSERT TABLE' command.",
                    context.Start.Line, context.Start.Column);
            }
            base.ExitTable_row(context);
        }

        // Returns the most recent AssertTableCommand in the current batch, or null when the batch has none.
        private AssertTableCommand FindCurrentAssertTable()
        {
            for (int i = _currentBatch.Commands.Count - 1; i >= 0; i--)
            {
                if (_currentBatch.Commands[i] is AssertTableCommand atc) return atc;
            }
            return null;
        }

        public override void ExitClear_cache([NotNull] Clear_cacheContext context)
        {
            var cmd = new ClearCacheCommand();
            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitClear_cache(context);
        }

        public override void ExitResults([NotNull] ResultsContext context)
        {
            // The grammar requires an ON/OFF flag. If it is missing the parser recovers with a
            // partial tree, so guard against it here and surface a helpful error.
            if (context.ChildCount != 2)
                throw new CommentScriptCommandException("Invalid RESULTS command. This command should be in the form of: '--> RESULTS <ON|OFF>'", context.Start.Line, context.Start.Column);

            var enabledNode = context.children[1] as ITerminalNode;
            var enabled = enabledNode?.Symbol.Type == CS_ON;
            var cmd = new ResultsCommand(enabled);
            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitResults(context);
        }

        public override void ExitTrace([NotNull] TraceContext context)
        {
            // The grammar requires a trace type and an ON/OFF flag. If either is missing the parser
            // recovers with a partial tree, so guard against it here and surface a helpful error
            // (rather than crashing with an index-out-of-range on the missing child).
            if (context.ChildCount != 3)
                throw new CommentScriptCommandException("Invalid TRACE command. This command should be in the form of: '--> TRACE <SERVERTIMINGS|QUERYPLAN|ALLQUERIES> <ON|OFF>'", context.Start.Line, context.Start.Column);

            var traceType = context.children[1].GetText();
            var enabledNode = context.children[2] as ITerminalNode;
            var enabled = enabledNode?.Symbol.Type == CS_ON;

            TraceCommand cmd;
            try
            {
                cmd = new TraceCommand(traceType, enabled);
            }
            catch (CommentScriptCommandException ex)
            {
                // Re-throw with the position of the offending command so the error can be marked.
                throw new CommentScriptCommandException(ex.Message, context.Start.Line, context.Start.Column);
            }
            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitTrace(context);
        }

        public override void ExitMetrics([NotNull] MetricsContext context)
        {
            var subContext = context.children[1];

            if (subContext is Metrics_exportContext exportCtx)
            {
                var fileName = exportCtx.children[1].GetText();
                var cmd = new MetricsCommand(MetricsAction.Export, fileName);
                _currentBatch.Commands.Add(cmd);
            }
            else if (subContext is Metrics_viewContext)
            {
                var cmd = new MetricsCommand(MetricsAction.View);
                _currentBatch.Commands.Add(cmd);
            }

            OutputCommand(_currentBatch.Output, context);
            base.ExitMetrics(context);
        }

        public override void ExitShow([NotNull] ShowContext context)
        {
            var typeNode = context.children[1] as ITerminalNode;
            var showType = ShowType.Dependencies;
            switch (typeNode?.Symbol.Type)
            {
                case CS_LAST_UPDATED:
                    showType = ShowType.LastUpdated;
                    break;
                case CS_MAX_UPDATED:
                    showType = ShowType.MaxUpdated;
                    break;
                case CS_DEPENDENCIES:
                default:
                    showType = ShowType.Dependencies;
                    break;
            }

            var cmd = new ShowCommand(showType);
            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitShow(context);
        }

        private void OutputCommand(StringBuilder output, ParserRuleContext context)
        {
            output.Append("-->");
            OutputChildTokens(output, context, 0);
            output.AppendLine();
        }

        private static void OutputChildTokens(StringBuilder output, ParserRuleContext context, int depth)
        {
            foreach (var child in context.children)
            {
                if (depth == 0) output.Append(' ');
                if (child.ChildCount > 0 && child is ParserRuleContext innerChild)
                {
                    OutputChildTokens(output, innerChild , depth+1);
                    continue;
                }
                
                var isScriptStringLiteral = ((child.Payload as CommonToken)?.Type ?? -1) == PreProcessorLexer.CS_STRING_LITERAL;
                if (isScriptStringLiteral)
                {
                    output.Append('\"');
                }
                output.Append(child.GetText());
                if (isScriptStringLiteral)
                {
                    output.Append('\"');
                }
            }
        }

        public override void ExitParameter_scalar_values([NotNull] Parameter_scalar_valuesContext context)
        {
            base.ExitParameter_scalar_values(context);
        }
        #endregion

        #region XMLA Parameters

        public override void ExitXmla_parameter([NotNull] PreProcessorParser.Xmla_parameterContext context)
        {
            var nameCtxt = context.children[1];
            var valueCtxt = context.children[2];

            var name = ((Xmla_nameContext)nameCtxt.Payload).children[1].GetText();
            var value = ((Xmla_valueContext)valueCtxt.Payload).children[2].GetText();
            var typename = ((Xmla_valueContext)valueCtxt.Payload).children[1].GetText();

            var cmd = new ParameterCommand(name, value, typename);
            _currentBatch.Commands.Add(cmd);
            base.ExitXmla_parameter(context);
        }

        #endregion
        public override void ExitQuery([NotNull] QueryContext context)
        {
            base.ExitQuery(context);
        }

        public override void ExitBlock([NotNull] BlockContext context)
        {
            base.ExitBlock(context);
        }

        // Finalizes a batch once it is closed (at "--> GO" or end of document): validates each
        // ASSERT TABLE has a table definition and infers the column types for its rows. Runs exactly
        // once per batch so InferColumnTypes is never applied twice.
        private void FinalizeBatch(ScriptBatch batch)
        {
            if (batch == null) return;
            foreach (var cmd in batch.Commands)
            {
                if (!(cmd is AssertTableCommand atc)) continue;

                if (!atc.HasTableDefinition)
                {
                    throw new CommentScriptCommandException(
                        "'--> ASSERT TABLE' must be followed by a table definition (one or more '-->>' rows) or a file (CSV/TXT/MD/PARQUET).",
                        atc.Line, atc.Column);
                }

                if (atc.Data.Rows.Count > 0)
                {
                    atc.InferColumnTypes();
                }
            }
        }

        public override void ExitDocument([NotNull] DocumentContext context)
        {
            FinalizeBatch(_currentBatch);
            base.ExitDocument(context);
        }

        public override void ExitGo_command([NotNull] Go_commandContext context)
        {
            FinalizeBatch(_currentBatch);
            _currentBatch = new ScriptBatch();
            _scriptBatches.Add(_currentBatch);
        }

    }

}
