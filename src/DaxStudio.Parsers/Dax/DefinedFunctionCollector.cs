using System.Collections.Generic;
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

            return new DefinedFunctionInfo(name, parameters);
        }
    }
}
