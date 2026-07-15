using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using System.Collections.Generic;
using System.Linq;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Walks the parse tree to determine the intellisense EditState at a given cursor position.
    /// Also collects in-scope variables and determines function call context.
    /// Uses a scope stack to correctly track variable visibility across nested and sibling VAR blocks.
    /// </summary>
    public class DaxCursorStateWalker : DAXParserBaseListener
    {
        private readonly int _cursorOffset;
        private readonly CommonTokenStream _tokenStream;

        // DEFINE-level variables (always in scope after declaration)
        private readonly List<string> _defineVariables = new List<string>();
        private readonly List<string> _definedMeasures = new List<string>();
        private readonly List<Metadata.DefinedFunctionInfo> _definedFunctions = new List<Metadata.DefinedFunctionInfo>();

        // Scope stack for VAR/RETURN blocks — each entry is variables declared in that scope
        private readonly Stack<List<string>> _scopeStack = new Stack<List<string>>();

        // Track whether we're inside a DEFINE block (vs expression-level VAR)
        private bool _inDefineBlock;

        private readonly Stack<FunctionCallContext> _functionCallStack = new Stack<FunctionCallContext>();

        private Metadata.DaxState _result;
        private bool _resolved;

        public DaxCursorStateWalker(int cursorOffset, CommonTokenStream tokenStream)
        {
            _cursorOffset = cursorOffset;
            _tokenStream = tokenStream;
        }

        /// <summary>
        /// Determines the DaxState at the cursor position by walking the parse tree.
        /// </summary>
        public static Metadata.DaxState GetStateAtCursor(string input, int cursorOffset)
        {
            ICharStream chars = new DAXCharStream(input);
            var lexer = new DAXLexer(chars);
            lexer.RemoveErrorListeners();
            var errorListener = new DaxIntellisenseErrorListener();
            lexer.AddErrorListener(errorListener);

            var tokenStream = new CommonTokenStream(lexer);
            var parser = new DAXParser(tokenStream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);
            parser.ErrorHandler = new DaxIntellisenseErrorStrategy();

            var tree = parser.daxQuery();

            var walker = new DaxCursorStateWalker(cursorOffset, tokenStream);
            ParseTreeWalker.Default.Walk(walker, tree);

            if (walker._result != null)
            {
                return walker._result;
            }

            // Fallback: determine state from the token at/before the cursor
            return walker.DetermineStateFromTokens(input, tokenStream);
        }

        private bool CursorIsWithin(ParserRuleContext ctx)
        {
            if (ctx == null || ctx.Start == null) return false;
            int start = ctx.Start.StartIndex;
            int stop = ctx.Stop != null ? ctx.Stop.StopIndex + 1 : start;
            return _cursorOffset >= start && _cursorOffset <= stop;
        }

        private bool CursorIsAfter(IToken token)
        {
            return token != null && _cursorOffset > token.StopIndex;
        }

        private bool CursorIsAt(IToken token)
        {
            return token != null && _cursorOffset >= token.StartIndex && _cursorOffset <= token.StopIndex + 1;
        }

        // --- DEFINE block ---
        public override void EnterDefineBlock(DAXParser.DefineBlockContext ctx)
        {
            _inDefineBlock = true;
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                // Check if cursor is right after DEFINE keyword and before any definition
                var defineNode = ctx.DEFINE();
                if (defineNode == null) return;
                var defineToken = defineNode.Symbol;
                if (CursorIsAfter(defineToken) && (ctx.definition() == null || ctx.definition().Length == 0 || _cursorOffset < ctx.definition()[0].Start.StartIndex))
                {
                    SetResult(Metadata.EditState.DefineContext);
                }
            }
        }

        public override void ExitDefineBlock(DAXParser.DefineBlockContext ctx)
        {
            _inDefineBlock = false;
        }

        public override void EnterDefinition(DAXParser.DefinitionContext ctx)
        {
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                // If we're at the very start with no specific definition chosen yet
                if (_cursorOffset == ctx.Start.StartIndex)
                {
                    SetResult(Metadata.EditState.DefineContext);
                }
            }
        }

        // --- EVALUATE block ---
        public override void EnterEvaluateBlock(DAXParser.EvaluateBlockContext ctx)
        {
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                // During error recovery on incomplete input the EVALUATE token may be missing, so guard
                // against a null terminal node before dereferencing its symbol.
                var evaluateNode = ctx.EVALUATE();
                if (evaluateNode == null) return;

                var evalToken = evaluateNode.Symbol;
                if (CursorIsAfter(evalToken) && ctx.expression() != null && _cursorOffset <= ctx.expression().Start.StartIndex)
                {
                    SetResult(Metadata.EditState.EvaluateContext);
                }
            }
        }

        // --- Variable definitions (scope-aware) ---
        public override void ExitVariableDefinition(DAXParser.VariableDefinitionContext ctx)
        {
            if (ctx.identifierOrKeyword() != null)
            {
                var name = ctx.identifierOrKeyword().GetText();
                if (_inDefineBlock || _scopeStack.Count == 0)
                {
                    // DEFINE-level variable — always available
                    _defineVariables.Add(name);
                }
                else
                {
                    // Expression-level variable — add to current scope
                    _scopeStack.Peek().Add(name);
                }
            }
        }

        // --- Measure definitions ---
        public override void ExitMeasureDefinition(DAXParser.MeasureDefinitionContext ctx)
        {
            if (ctx.COLUMN_OR_MEASURE() != null)
            {
                _definedMeasures.Add(ctx.COLUMN_OR_MEASURE().GetText());
            }
        }

        // --- Function calls ---
        public override void EnterFunctionCall(DAXParser.FunctionCallContext ctx)
        {
            if (_resolved) return;
            string funcName = ctx.functionCallName()?.GetText() ?? "";
            _functionCallStack.Push(new FunctionCallContext(funcName, 0));
        }

        public override void ExitFunctionCall(DAXParser.FunctionCallContext ctx)
        {
            if (_resolved) return;

            if (CursorIsWithin(ctx))
            {
                string funcName = ctx.functionCallName()?.GetText() ?? "";
                int argIndex = DetermineArgumentIndex(ctx);

                var state = new Metadata.DaxState(Metadata.EditState.FunctionArgument, funcName, argIndex);
                state.FunctionNestingDepth = _functionCallStack.Count;
                SetResult(state);
            }

            if (_functionCallStack.Count > 0)
                _functionCallStack.Pop();
        }

        // --- Function definitions ---
        public override void EnterFunctionDefinition(DAXParser.FunctionDefinitionContext ctx)
        {
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                SetResult(Metadata.EditState.FunctionDefinition);
            }
        }

        public override void ExitFunctionDefinition(DAXParser.FunctionDefinitionContext ctx)
        {
            // Collect the user-defined function (name + parameter names) so it can be offered as a
            // completion and provide insight help elsewhere in the query (e.g. in a following EVALUATE).
            var info = DefinedFunctionCollector.FromContext(ctx);
            if (info != null) _definedFunctions.Add(info);
        }

        // --- Type annotations ---
        public override void EnterTypeAnnotation(DAXParser.TypeAnnotationContext ctx)
        {
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                SetResult(Metadata.EditState.ParameterType);
            }
        }

        // --- Parameter defs (after colon) ---
        public override void EnterParameterDef(DAXParser.ParameterDefContext ctx)
        {
            if (_resolved) return;
            if (CursorIsWithin(ctx) && ctx.COLON() != null && CursorIsAfter(ctx.COLON().Symbol))
            {
                SetResult(Metadata.EditState.ParameterType);
            }
        }

        // --- ORDER BY ---
        public override void EnterOrderByClause(DAXParser.OrderByClauseContext ctx)
        {
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                SetResult(Metadata.EditState.OrderByContext);
            }
        }

        // --- Column references (partial table/column) ---
        public override void EnterColumnRef(DAXParser.ColumnRefContext ctx)
        {
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                // A column reference can be qualified by a quoted table ('Table'[Col]), an unquoted
                // table name (Table[Col]) or a table named like a function. Capture whichever is present.
                string table = null;
                if (ctx.tableRef() != null) table = ctx.tableRef().GetText();
                else if (ctx.identifierOrKeyword() != null) table = ctx.identifierOrKeyword().GetText();
                else if (ctx.builtInFunction() != null) table = ctx.builtInFunction().GetText();

                if (table != null)
                {
                    var state = new Metadata.DaxState(Metadata.EditState.PartialColumn);
                    state.CurrentTable = table;
                    SetResult(state);
                }
            }
        }

        // --- VAR/RETURN (scope management) ---
        public override void EnterVarReturnExpr(DAXParser.VarReturnExprContext ctx)
        {
            // Push a new scope for this VAR/RETURN block
            // Do NOT resolve cursor here — variables haven't been added yet
            _scopeStack.Push(new List<string>());
        }

        public override void ExitVarReturnExpr(DAXParser.VarReturnExprContext ctx)
        {
            // Resolve cursor BEFORE popping scope — all variables are now collected
            if (!_resolved && CursorIsWithin(ctx) && ctx.RETURN() != null && CursorIsAfter(ctx.RETURN().Symbol))
            {
                SetResult(Metadata.EditState.ReturnExpression);
            }

            // Pop scope — variables from this block are no longer visible
            if (_scopeStack.Count > 0)
                _scopeStack.Pop();
        }

        // --- Table constructors ---
        public override void ExitTableConstructor(DAXParser.TableConstructorContext ctx)
        {
            // Resolve in Exit (not Enter) so more specific child contexts resolve first
            if (_resolved) return;
            if (CursorIsWithin(ctx))
            {
                SetResult(Metadata.EditState.TableConstructor);
            }
        }

        private int DetermineArgumentIndex(DAXParser.FunctionCallContext ctx)
        {
            if (ctx.argumentList() == null) return 0;

            var args = ctx.argumentList().argument();
            if (args == null || args.Length == 0) return 0;

            for (int i = 0; i < args.Length; i++)
            {
                if (CursorIsWithin(args[i]) || (i < args.Length - 1 && _cursorOffset <= args[i + 1].Start.StartIndex))
                {
                    return i;
                }
            }
            return args.Length - 1;
        }

        private void SetResult(Metadata.EditState state)
        {
            if (_resolved) return;
            _result = new Metadata.DaxState(state);
            _result.Variables = GetInScopeVariables();
            _result.DefinedMeasures = _definedMeasures.ToList();
            _result.DefinedFunctions = _definedFunctions.ToList();
            _resolved = true;
        }

        private void SetResult(Metadata.DaxState state)
        {
            if (_resolved) return;
            state.Variables = GetInScopeVariables();
            state.DefinedMeasures = _definedMeasures.ToList();
            state.DefinedFunctions = _definedFunctions.ToList();
            _result = state;
            _resolved = true;
        }

        /// <summary>
        /// Returns all variables visible at the current point in the walk:
        /// DEFINE-level variables + all variables from the current scope chain (innermost to outermost).
        /// </summary>
        private List<string> GetInScopeVariables()
        {
            var result = new List<string>(_defineVariables);
            foreach (var scope in _scopeStack)
            {
                result.AddRange(scope);
            }
            return result;
        }

        private Metadata.DaxState DetermineStateFromTokens(string input, CommonTokenStream tokenStream)
        {
            tokenStream.Fill();
            var tokens = tokenStream.GetTokens();

            // Find the token at or just before cursor
            IToken tokenAtCursor = null;
            IToken tokenBeforeCursor = null;

            foreach (var token in tokens)
            {
                if (token.Type == DAXLexer.Eof) break;
                if (token.Channel != 0) continue;

                if (token.StartIndex <= _cursorOffset && token.StopIndex >= _cursorOffset - 1)
                {
                    tokenAtCursor = token;
                }
                if (token.StopIndex < _cursorOffset)
                {
                    tokenBeforeCursor = token;
                }
            }

            var effectiveToken = tokenAtCursor ?? tokenBeforeCursor;
            if (effectiveToken == null)
            {
                return new Metadata.DaxState(Metadata.EditState.TopLevel);
            }

            // Check token type to determine state
            switch (effectiveToken.Type)
            {
                case DAXLexer.PARTIAL_TABLE:
                    return new Metadata.DaxState(Metadata.EditState.PartialTable, partialText: effectiveToken.Text);

                case DAXLexer.PARTIAL_COLUMN_OR_MEASURE:
                    // Look back for a preceding TABLE_REF to get table context
                    string precedingTable = FindPrecedingTableRef(tokens, effectiveToken);
                    var pcState = new Metadata.DaxState(Metadata.EditState.PartialColumn, partialText: effectiveToken.Text);
                    pcState.CurrentTable = precedingTable;
                    return pcState;

                case DAXLexer.TABLE_REF:
                    return new Metadata.DaxState(Metadata.EditState.CompleteTable, currentTable: effectiveToken.Text);

                case DAXLexer.DEFINE:
                    return new Metadata.DaxState(Metadata.EditState.DefineContext);

                case DAXLexer.EVALUATE:
                    return new Metadata.DaxState(Metadata.EditState.EvaluateContext);

                case DAXLexer.EQUALS:
                case DAXLexer.STRICT_EQUALS:
                case DAXLexer.PLUS:
                case DAXLexer.MINUS:
                case DAXLexer.STAR:
                case DAXLexer.DIV:
                case DAXLexer.CARET:
                case DAXLexer.AMP:
                case DAXLexer.LT:
                case DAXLexer.GT:
                case DAXLexer.OP_LE:
                case DAXLexer.OP_GE:
                case DAXLexer.OP_NE:
                case DAXLexer.OP_AND:
                case DAXLexer.OP_OR:
                    return new Metadata.DaxState(Metadata.EditState.AfterOperator);

                case DAXLexer.OPEN_PARENS:
                case DAXLexer.COMMA:
                    return new Metadata.DaxState(Metadata.EditState.FunctionArgument);

                case DAXLexer.COLON:
                    return new Metadata.DaxState(Metadata.EditState.ParameterType);

                case DAXLexer.RETURN:
                    return new Metadata.DaxState(Metadata.EditState.ReturnExpression);

                case DAXLexer.OPEN_CURLY:
                    return new Metadata.DaxState(Metadata.EditState.TableConstructor);

                case DAXLexer.LAMBDA_ARROW:
                    return new Metadata.DaxState(Metadata.EditState.ExpressionStart);

                default:
                    if (effectiveToken.Type == DAXLexer.IDENTIFIER)
                    {
                        return new Metadata.DaxState(Metadata.EditState.Identifier, partialText: effectiveToken.Text);
                    }
                    return new Metadata.DaxState(Metadata.EditState.Unknown);
            }
        }

        private static string FindPrecedingTableRef(IList<IToken> tokens, IToken currentToken)
        {
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i] == currentToken && i > 0)
                {
                    // Look back for the closest table name (skipping whitespace/hidden channel).
                    // A table qualifying a '[' can be a quoted TABLE_REF ('Sales'[) or an unquoted
                    // IDENTIFIER (Sales[), so accept either.
                    for (int j = i - 1; j >= 0; j--)
                    {
                        if (tokens[j].Type == DAXLexer.TABLE_REF || tokens[j].Type == DAXLexer.IDENTIFIER)
                            return tokens[j].Text;
                        if (tokens[j].Channel == 0 && tokens[j].Type != DAXLexer.Eof)
                            break; // Non-hidden, non-table token — stop looking
                    }
                    break;
                }
            }
            return null;
        }

        private class FunctionCallContext
        {
            public string FunctionName { get; }
            public int ArgumentIndex { get; set; }

            public FunctionCallContext(string functionName, int argumentIndex)
            {
                FunctionName = functionName;
                ArgumentIndex = argumentIndex;
            }
        }
    }
}
