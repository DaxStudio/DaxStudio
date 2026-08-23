using System.Collections.Generic;
using DaxStudio.Parsers.Grammars.Generated;

namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Walks a parse tree collecting the names of every non-built-in function that is actually
    /// <em>called</em> in the query outside of any <c>DEFINE FUNCTION</c> body (i.e. from the EVALUATE
    /// statement or from DEFINE MEASURE/COLUMN/TABLE/VAR expressions). Calls that appear only inside a
    /// function definition's own body are ignored here - those are surfaced separately as the references
    /// of that function. This lets callers exclude query-scoped functions that are declared but never
    /// invoked from the dependency tree.
    /// </summary>
    internal class ReferencedFunctionCollector : DAXParserBaseListener
    {
        private int _functionDefinitionDepth;

        public HashSet<string> FunctionNames { get; } =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public override void EnterFunctionDefinition(DAXParser.FunctionDefinitionContext ctx)
        {
            _functionDefinitionDepth++;
        }

        public override void ExitFunctionDefinition(DAXParser.FunctionDefinitionContext ctx)
        {
            if (_functionDefinitionDepth > 0) _functionDefinitionDepth--;
        }

        public override void EnterFunctionCall(DAXParser.FunctionCallContext ctx)
        {
            // only consider calls made outside of a DEFINE FUNCTION body
            if (_functionDefinitionDepth > 0) return;

            var callName = ctx.functionCallName();
            // built-in DAX functions are matched by the builtInFunction alternative - skip them.
            if (callName == null || callName.builtInFunction() != null) return;

            var name = callName.GetText();
            if (!string.IsNullOrEmpty(name)) FunctionNames.Add(name);
        }
    }
}
