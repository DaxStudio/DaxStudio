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
            if (context.ChildCount != 2) throw new ArgumentException("Invalid number of arguments for USE command. This command should be in the form of: '--> USE [\"]<DatabaseName>[\"]'");

            var database = context.children[1].GetText();
            var cmd = new UseCommand(database);
            _currentBatch.Commands.Add(cmd);
            OutputCommand( _currentBatch.Output, context);
            base.ExitUse(context);
        }

        public override void ExitConnect([NotNull] PreProcessorParser.ConnectContext context)
        {
            if (context.ChildCount != 3) throw new ArgumentException("Invalid number of arguments for CONNECT command. This command should be in the form of: '--> CONNECT <ConnectionType> <ConnectionName>'");

            var serverType = context.children[1].GetText();
            var serverName = context.children[2].GetText();

            var cmd = new ConnectCommand(serverType, serverName);
            _currentBatch.Commands.Add(cmd);
            OutputCommand( _currentBatch.Output, context);
            base.ExitConnect(context);
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
            var node2 = context.children[1] as ITerminalNode;
            var node3 = context.children[2] as ITerminalNode;

            if (node2?.Symbol.Type == CS_PERFORMANCE)
            {
                var typename = context.children[1].GetText();
                var name = context.children[2].GetText();
                
                var cmd = new TestCommand(typename, name);
                _currentBatch.Commands.Add(cmd);
            }
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

            var cmd = new AssertTableCommand(mode);
            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitAssert_table_header(context);
        }

        public override void ExitTable_data_row([NotNull] Table_data_rowContext context)
        {
            // Find the most recent AssertTableCommand in the current batch
            AssertTableCommand assertTableCmd = null;
            for (int i = _currentBatch.Commands.Count - 1; i >= 0; i--)
            {
                if (_currentBatch.Commands[i] is AssertTableCommand atc)
                {
                    assertTableCmd = atc;
                    break;
                }
            }
            if (assertTableCmd == null) return;

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

        public override void ExitClear_cache([NotNull] Clear_cacheContext context)
        {
            var cmd = new ClearCacheCommand();
            _currentBatch.Commands.Add(cmd);
            OutputCommand(_currentBatch.Output, context);
            base.ExitClear_cache(context);
        }

        public override void ExitTrace([NotNull] TraceContext context)
        {
            var traceType = context.children[1].GetText();
            var enabledNode = context.children[2] as ITerminalNode;
            var enabled = enabledNode?.Symbol.Type == CS_ON;

            var cmd = new TraceCommand(traceType, enabled);
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
            // After all table rows are processed, infer column types for any AssertTableCommand
            foreach (var cmd in _currentBatch.Commands)
            {
                if (cmd is AssertTableCommand atc && atc.Data.Rows.Count > 0)
                {
                    atc.InferColumnTypes();
                }
            }
            base.ExitBlock(context);
        }

        public override void ExitGo_command([NotNull] Go_commandContext context)
        {
            _currentBatch = new ScriptBatch();
            _scriptBatches.Add(_currentBatch);
        }

    }

}
