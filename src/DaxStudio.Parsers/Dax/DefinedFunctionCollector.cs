using System.Collections.Generic;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.Metadata;

namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Walks a parse tree collecting all functions defined in the query via DEFINE FUNCTION, together
    /// with their parameter names, so signature/insight help can be provided for them.
    /// </summary>
    internal class DefinedFunctionCollector : DAXParserBaseListener
    {
        public List<DefinedFunctionInfo> Functions { get; } = new List<DefinedFunctionInfo>();

        public override void ExitFunctionDefinition(DAXParser.FunctionDefinitionContext ctx)
        {
            var info = FromContext(ctx);
            if (info != null) Functions.Add(info);
        }

        /// <summary>
        /// Builds a <see cref="DefinedFunctionInfo"/> from a function definition context, extracting the
        /// name and parameter names. Returns null when the name is missing (e.g. during error recovery).
        /// </summary>
        public static DefinedFunctionInfo FromContext(DAXParser.FunctionDefinitionContext ctx)
        {
            if (ctx?.functionName() == null) return null;

            var name = ctx.functionName().GetText();
            var parameters = new List<DefinedFunctionParameter>();

            var paramList = ctx.parameterDefList();
            if (paramList != null)
            {
                foreach (var paramDef in paramList.parameterDef())
                {
                    var paramName = paramDef.identifierOrKeyword()?.GetText() ?? string.Empty;
                    var paramType = paramDef.typeAnnotation()?.GetText() ?? string.Empty;
                    parameters.Add(new DefinedFunctionParameter(paramName, paramType));
                }
            }

            return new DefinedFunctionInfo(name, parameters, GetFunctionBodyText(ctx), DaxReferenceWalker.Collect(ctx.expression()));
        }


        /// <summary>
        /// Returns the original source text of the function definition from the parameter list onwards -
        /// i.e. everything after the first <c>=</c>, in the form <c>(param1) =&gt; param1 + 10</c> -
        /// preserving whitespace and comments. Returns an empty string when it cannot be captured.
        /// </summary>
        private static string GetFunctionBodyText(DAXParser.FunctionDefinitionContext ctx)
        {
            var exprCtx = ctx?.expression();
            if (exprCtx?.Stop == null || exprCtx.Start?.InputStream == null) return string.Empty;

            // Start from the opening parenthesis of the parameter list (the text right after the '='),
            // falling back to the body expression itself if the parenthesis is missing (error recovery).
            var startToken = ctx.OPEN_PARENS()?.Symbol ?? exprCtx.Start;

            var interval = new Antlr4.Runtime.Misc.Interval(startToken.StartIndex, exprCtx.Stop.StopIndex);
            return exprCtx.Start.InputStream.GetText(interval);
        }
    }
}
