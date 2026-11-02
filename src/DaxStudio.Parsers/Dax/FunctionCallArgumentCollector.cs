using System.Collections.Generic;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.Metadata;

namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Walks a parse tree collecting, for every call to a non-built-in function, the columns, measures and
    /// (nested) functions referenced in the <em>arguments</em> passed at that call site - e.g. the
    /// <c>'Product'[Color]</c> in <c>queryFunc ( VALUES ( 'Product'[Color] ) )</c>. Results are keyed by the
    /// called function name so the dependency tree can list a query-scoped function's call-site arguments as
    /// its children, in addition to whatever its own body references.
    /// </summary>
    internal class FunctionCallArgumentCollector : DAXParserBaseListener
    {
        private readonly Dictionary<string, List<DaxObjectReference>> _references =
            new Dictionary<string, List<DaxObjectReference>>(System.StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _seen =
            new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Called function name -&gt; de-duplicated references found in its call-site argument lists.</summary>
        public IReadOnlyDictionary<string, List<DaxObjectReference>> References => _references;

        public override void EnterFunctionCall(DAXParser.FunctionCallContext ctx)
        {
            var callName = ctx.functionCallName();
            // built-in DAX functions are matched by the builtInFunction alternative - skip them.
            if (callName == null || callName.builtInFunction() != null) return;

            var name = callName.GetText();
            if (string.IsNullOrEmpty(name)) return;

            var argumentList = ctx.argumentList();
            if (argumentList == null) return;

            if (!_references.TryGetValue(name, out var list))
            {
                list = new List<DaxObjectReference>();
                _references[name] = list;
                _seen[name] = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            }

            // collect the references contained in the argument list only (not the function name itself)
            DaxReferenceWalker.Collect(argumentList, list, _seen[name]);
        }
    }
}
