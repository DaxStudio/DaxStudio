using System.Collections.Generic;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Grammars.Generated;
using DaxStudio.Parsers.Metadata;

namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Shared helper that walks a parse-tree fragment collecting every column reference
    /// (<c>Table[Column]</c> or bare <c>[Name]</c>) and every call to a non-built-in function, so callers
    /// can extend the dependency tree with the objects an expression depends on. Built-in DAX functions
    /// are skipped. Used both for the body of a <c>DEFINE FUNCTION</c> and for the arguments passed to a
    /// query-scoped function at its call site.
    /// </summary>
    internal static class DaxReferenceWalker
    {
        public static IReadOnlyList<DaxObjectReference> Collect(IParseTree node)
        {
            var references = new List<DaxObjectReference>();
            var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            Collect(node, references, seen);
            return references;
        }

        public static void Collect(IParseTree node, List<DaxObjectReference> references, HashSet<string> seen)
        {
            if (node == null) return;

            switch (node)
            {
                case DAXParser.ColumnRefContext columnRef:
                    AddColumnReference(columnRef, references, seen);
                    break;
                case DAXParser.FunctionCallContext functionCall:
                    AddFunctionReference(functionCall, references, seen);
                    break;
            }

            for (int i = 0; i < node.ChildCount; i++)
            {
                Collect(node.GetChild(i), references, seen);
            }
        }

        /// <summary>Adds a column (qualified) or column/measure (bare) reference, de-duplicated.</summary>
        private static void AddColumnReference(DAXParser.ColumnRefContext ctx, List<DaxObjectReference> references, HashSet<string> seen)
        {
            var column = ctx.COLUMN_OR_MEASURE()?.GetText();
            if (string.IsNullOrEmpty(column)) return;

            string table = null;
            if (ctx.tableRef() != null) table = ctx.tableRef().GetText();
            else if (ctx.identifierOrKeyword() != null) table = ctx.identifierOrKeyword().GetText();
            else if (ctx.builtInFunction() != null) table = ctx.builtInFunction().GetText();

            if (string.IsNullOrEmpty(table))
            {
                if (seen.Add($"CM|{column}"))
                    references.Add(new DaxObjectReference(DaxReferenceKind.ColumnOrMeasure, column));
            }
            else
            {
                if (seen.Add($"C|{table}|{column}"))
                    references.Add(new DaxObjectReference(DaxReferenceKind.Column, column, table));
            }
        }

        /// <summary>Adds a call to a non-built-in function (a potential model / query-scoped UDF), de-duplicated.</summary>
        private static void AddFunctionReference(DAXParser.FunctionCallContext ctx, List<DaxObjectReference> references, HashSet<string> seen)
        {
            var callName = ctx.functionCallName();
            // Built-in DAX functions are matched by the builtInFunction alternative - skip them.
            if (callName == null || callName.builtInFunction() != null) return;

            var name = callName.GetText();
            if (string.IsNullOrEmpty(name)) return;

            if (seen.Add($"F|{name}"))
                references.Add(new DaxObjectReference(DaxReferenceKind.Function, name));
        }
    }
}
