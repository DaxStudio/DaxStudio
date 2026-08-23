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

        // Variables in scope at the last variable declaration that precedes the cursor. Captured
        // incrementally during the walk (in ExitVariableDefinition) so it excludes variables declared
        // at or after the cursor. Used as a fallback source of in-scope variables when the cursor state
        // has to be determined from the token stream, or when a tree-resolved state came back with no
        // variables (both happen under error recovery on incomplete input).
        private List<string> _cursorScopeVariables;

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
            // The editor can contain several independent query "chunks" (e.g. an "EVALUATE ..." for
            // one query followed by a "DEFINE ... EVALUATE ..." for another). A DAX query requires
            // DEFINE to precede EVALUATE, so an earlier EVALUATE makes a following DEFINE a syntax
            // error and the parser's error recovery loses the DEFINE block's measures/variables. To
            // give correct completions we restrict parsing to the chunk that contains the cursor.
            var chunk = ExtractStatementChunk(input, cursorOffset);
            input = chunk.Text;
            cursorOffset = chunk.CursorOffset;

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
                // Under error recovery on incomplete input, a tree-resolved expression state can end up
                // with no in-scope variables even though the cursor sits after variable declarations.
                // Fall back to the snapshot captured during the walk in that case.
                if ((walker._result.Variables == null || walker._result.Variables.Count == 0)
                    && walker._cursorScopeVariables != null && walker._cursorScopeVariables.Count > 0)
                {
                    walker._result.Variables = walker._cursorScopeVariables;
                }
                return walker.RefinePartialReference(walker._result);
            }

            // Fallback: determine state from the token at/before the cursor. The token-based path does
            // not have scope context, so apply the variables/measures/functions collected during the
            // walk (variables use the snapshot captured at the cursor's enclosing VAR/RETURN block, or
            // the DEFINE-level variables when the cursor is not inside a VAR/RETURN block).
            var fallbackState = walker.DetermineStateFromTokens(input, tokenStream);
            if (fallbackState.Variables == null || fallbackState.Variables.Count == 0)
                fallbackState.Variables = walker._cursorScopeVariables ?? new List<string>(walker._defineVariables);
            if (fallbackState.DefinedMeasures == null || fallbackState.DefinedMeasures.Count == 0)
                fallbackState.DefinedMeasures = walker._definedMeasures.ToList();
            if (fallbackState.DefinedFunctions == null || fallbackState.DefinedFunctions.Count == 0)
                fallbackState.DefinedFunctions = walker._definedFunctions.ToList();
            return fallbackState;
        }

        /// <summary>
        /// The text of a query chunk together with the cursor offset translated into that chunk.
        /// </summary>
        internal struct StatementChunk
        {
            public string Text;
            public int CursorOffset;
        }

        /// <summary>
        /// Returns the slice of the editor text that should be parsed to determine the completion
        /// state at the cursor: the text from the start of the query containing the cursor up to the
        /// cursor itself. A DAX query is "[DEFINE ...] EVALUATE ... [EVALUATE ...]*", and DEFINE must be
        /// the first keyword, so every top-level DEFINE keyword starts a new query; the slice therefore
        /// begins at the DEFINE at or before the cursor (or the start of the text when the cursor
        /// precedes the first DEFINE). The slice always ends at the cursor so that text after the cursor
        /// - which is frequently invalid while the user edits the middle of a query - is never parsed.
        /// Comments and string literals are ignored because their "DEFINE" text is not tokenised as a
        /// DEFINE keyword.
        /// </summary>
        internal static StatementChunk ExtractStatementChunk(string input, int cursorOffset)
        {
            if (string.IsNullOrEmpty(input))
                return new StatementChunk { Text = input ?? string.Empty, CursorOffset = cursorOffset };

            // Only ever parse up to the cursor. Text after the cursor is irrelevant to the completion
            // state at the cursor, and while the user edits in the middle of an existing query the
            // trailing text is frequently (temporarily) invalid - parsing it would derail the parser's
            // error recovery and lose the in-scope variables/measures we need. Clamp the cursor into
            // the valid range first.
            if (cursorOffset < 0) cursorOffset = 0;
            if (cursorOffset > input.Length) cursorOffset = input.Length;

            var defineOffsets = new List<int>();
            try
            {
                ICharStream chars = new DAXCharStream(input);
                var lexer = new DAXLexer(chars);
                lexer.RemoveErrorListeners();
                var tokenStream = new CommonTokenStream(lexer);
                tokenStream.Fill();
                foreach (var token in tokenStream.GetTokens())
                {
                    if (token.Type == DAXLexer.Eof) break;
                    if (token.Channel != 0) continue;
                    if (token.Type == DAXLexer.DEFINE)
                        defineOffsets.Add(token.StartIndex);
                }
            }
            catch
            {
                // If lexing fails for any reason fall back to parsing the text up to the cursor.
                return new StatementChunk { Text = input.Substring(0, cursorOffset), CursorOffset = cursorOffset };
            }

            // Chunk starts at the last DEFINE at or before the cursor (0 if the cursor precedes them all).
            // A DAX query is "[DEFINE ...] EVALUATE ...", and DEFINE must be the first keyword, so every
            // top-level DEFINE keyword starts a new query. Comments and string literals are ignored
            // because their "DEFINE" text is not tokenised as a DEFINE keyword.
            int chunkStart = 0;
            foreach (var offset in defineOffsets)
            {
                if (offset <= cursorOffset) chunkStart = offset;
                else break;
            }

            // The chunk ends at the cursor. Because the cursor is at or before any later DEFINE, this
            // also naturally excludes any following DEFINE-separated query.
            return new StatementChunk
            {
                Text = input.Substring(chunkStart, cursorOffset - chunkStart),
                CursorOffset = cursorOffset - chunkStart
            };
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
                if (_scopeStack.Count > 0)
                {
                    // Inside a VAR/RETURN block (this includes a VAR/RETURN that forms the body of a
                    // measure/column definition) — the variable is only visible within that block.
                    _scopeStack.Peek().Add(name);
                }
                else
                {
                    // DEFINE-level variable declared directly in the DEFINE block (not inside a
                    // VAR/RETURN expression) — always available after declaration.
                    _defineVariables.Add(name);
                }
            }

            // Snapshot the variables in scope once the cursor is strictly PAST this whole declaration.
            // Under error recovery on incomplete input the cursor's tokens are sometimes parsed outside
            // the enclosing VAR/RETURN context, so this per-declaration capture (last one before the
            // cursor wins) is a robust fallback source of in-scope variables. The cursor must be beyond
            // (not merely adjacent to) the declaration: a variable is only in scope after its full
            // "VAR name = expr", so while its name/expression is still being typed — e.g. "VAR vte" with
            // the caret right after "vte" — it must not offer itself as a completion.
            //
            // Additionally require a real (default-channel) token between this declaration and the cursor.
            // When only whitespace follows, the user is still authoring THIS variable's value (its
            // expression can end in a dangling operator, e.g. "VAR v = 1 + " where the "+" is parsed as
            // part of the declaration), so the variable is a self-reference that must NOT be in scope yet.
            // A following VAR/RETURN/EVALUATE token means the declaration is complete and the variable is
            // genuinely in scope. This mirrors ResetCursorScopeIfPastDefinition's "trailing whitespace
            // alone does not count" rule.
            if (ctx.Stop != null && _cursorOffset > ctx.Stop.StopIndex + 1
                && HasDefaultChannelTokenBetween(ctx.Stop.StopIndex, _cursorOffset))
            {
                _cursorScopeVariables = GetInScopeVariables();
            }
        }

        // --- Measure definitions ---
        public override void ExitMeasureDefinition(DAXParser.MeasureDefinitionContext ctx)
        {
            if (ctx.COLUMN_OR_MEASURE() != null)
            {
                _definedMeasures.Add(ctx.COLUMN_OR_MEASURE().GetText());
            }

            ResetCursorScopeIfPastDefinition(ctx);
        }

        // Variables declared inside a measure/column/function definition's VAR/RETURN body are local to
        // that definition. Once the definition ends they go out of scope, so if the cursor sits in a
        // *later* construct (a following DEFINE definition, or the EVALUATE block) reset the fallback
        // variable snapshot to the query-level (DEFINE) variables so the definition-local variables do
        // not leak. The boundary is the whole definition rather than the inner VAR/RETURN block, because
        // grammar/error-recovery can truncate the VAR/RETURN context early (e.g. "RETURN a + par" binds
        // "+ par" outside the varReturnExpr) yet the variable is still legitimately in scope in the tail
        // of the same definition. "Later construct" is detected by a real (default-channel) token between
        // this definition and the cursor; trailing whitespace alone - e.g. an empty "RETURN " still being
        // typed in this same measure - does not count, so the definition's own RETURN completions remain.
        private void ResetCursorScopeIfPastDefinition(ParserRuleContext ctx)
        {
            if (ctx?.Stop == null) return;
            if (_cursorOffset <= ctx.Stop.StopIndex + 1) return;
            if (!HasDefaultChannelTokenBetween(ctx.Stop.StopIndex, _cursorOffset)) return;
            _cursorScopeVariables = GetInScopeVariables();
        }

        private bool HasDefaultChannelTokenBetween(int afterStopIndex, int beforeCursorOffset)
        {
            _tokenStream.Fill();
            foreach (var token in _tokenStream.GetTokens())
            {
                if (token.Type == DAXLexer.Eof) break;
                if (token.Channel != 0) continue;
                if (token.StartIndex > afterStopIndex && token.StopIndex < beforeCursorOffset)
                    return true;
            }
            return false;
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

            ResetCursorScopeIfPastDefinition(ctx);
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

        // A partial quoted-table ('Tab) or partial bracketed-column/measure ([Col) token at the cursor
        // unambiguously identifies a table or column/measure reference, even when the cursor also sits
        // inside a function argument or another generic expression context. The tree walk resolves those
        // enclosing contexts to an expression state (which also offers functions), so refine the result
        // to the specific partial-reference state. This matches the legacy line-based provider, which
        // keyed purely off the open quote/bracket and never offered functions once one was typed.
        private Metadata.DaxState RefinePartialReference(Metadata.DaxState state)
        {
            if (state == null) return state;

            switch (state.State)
            {
                case Metadata.EditState.FunctionArgument:
                case Metadata.EditState.NextArgument:
                case Metadata.EditState.ExpressionStart:
                case Metadata.EditState.AfterOperator:
                case Metadata.EditState.ReturnExpression:
                case Metadata.EditState.Identifier:
                case Metadata.EditState.Unknown:
                    break;
                default:
                    return state;
            }

            var token = GetEffectiveTokenAtCursor();
            if (token == null) return state;

            if (token.Type == DAXLexer.PARTIAL_TABLE)
            {
                return new Metadata.DaxState(Metadata.EditState.PartialTable, partialText: token.Text);
            }

            if (token.Type == DAXLexer.PARTIAL_COLUMN_OR_MEASURE)
            {
                _tokenStream.Fill();
                var precedingTable = FindPrecedingTableRef(_tokenStream.GetTokens(), token);
                var pcState = new Metadata.DaxState(Metadata.EditState.PartialColumn, partialText: token.Text);
                pcState.CurrentTable = precedingTable;
                return pcState;
            }

            return state;
        }

        // Returns the default-channel token that contains the cursor, or the last token before it.
        private IToken GetEffectiveTokenAtCursor()
        {
            _tokenStream.Fill();
            var tokens = _tokenStream.GetTokens();

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

            return tokenAtCursor ?? tokenBeforeCursor;
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
