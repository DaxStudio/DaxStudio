using System.Collections.Generic;
using DaxStudio.Parsers.Grammars.Generated;
using Antlr4.Runtime;

namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// A foldable region discovered in the parse tree, expressed as raw character offsets into the
    /// parsed source. <see cref="StartOffset"/> is the start index of the construct's first token and
    /// <see cref="EndOffset"/> is one past the last token. The consumer is responsible for turning
    /// this into an editor fold (e.g. collapsing everything after the first line) and for discarding
    /// single-line regions.
    /// </summary>
    public struct FoldRange
    {
        public int StartOffset { get; }
        public int EndOffset { get; }
        public string Kind { get; }

        public FoldRange(int startOffset, int endOffset, string kind)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            Kind = kind;
        }
    }

    /// <summary>
    /// Walks a DAX parse tree collecting <see cref="FoldRange"/>s for the structural constructs that
    /// should be foldable in the editor: the DEFINE block and its definitions, VAR/RETURN blocks,
    /// EVALUATE blocks and their ORDER BY clause, and multi-line bracket pairs (function calls and
    /// table constructors). Ranges are emitted for every matching construct regardless of whether it
    /// spans multiple lines; the caller filters out single-line regions.
    /// </summary>
    internal class FoldingCollector : DAXParserBaseListener
    {
        public List<FoldRange> Foldings { get; } = new List<FoldRange>();

        private void Add(ParserRuleContext ctx, string kind)
        {
            if (ctx?.Start == null || ctx.Stop == null) return;
            var start = ctx.Start.StartIndex;
            var end = ctx.Stop.StopIndex + 1;
            if (end <= start) return;
            Foldings.Add(new FoldRange(start, end, kind));
        }

        public override void ExitDefineBlock(DAXParser.DefineBlockContext ctx) => Add(ctx, "Define");
        public override void ExitMeasureDefinition(DAXParser.MeasureDefinitionContext ctx) => Add(ctx, "Measure");
        public override void ExitColumnDefinition(DAXParser.ColumnDefinitionContext ctx) => Add(ctx, "Column");
        public override void ExitTableDefinition(DAXParser.TableDefinitionContext ctx) => Add(ctx, "Table");
        public override void ExitFunctionDefinition(DAXParser.FunctionDefinitionContext ctx) => Add(ctx, "Function");
        public override void ExitVariableDefinition(DAXParser.VariableDefinitionContext ctx) => Add(ctx, "Var");
        public override void ExitVarReturnExpr(DAXParser.VarReturnExprContext ctx) => Add(ctx, "VarReturn");
        public override void ExitEvaluateBlock(DAXParser.EvaluateBlockContext ctx) => Add(ctx, "Evaluate");
        public override void ExitOrderByClause(DAXParser.OrderByClauseContext ctx) => Add(ctx, "OrderBy");
        public override void ExitFunctionCall(DAXParser.FunctionCallContext ctx) => Add(ctx, "FunctionCall");
        public override void ExitTableConstructor(DAXParser.TableConstructorContext ctx) => Add(ctx, "TableConstructor");
        public override void ExitParenExpr(DAXParser.ParenExprContext ctx) => Add(ctx, "Paren");
    }
}
