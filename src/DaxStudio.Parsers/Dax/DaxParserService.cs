using Antlr4.Runtime;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Metadata;
using System.Collections.Generic;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Main facade for the DAX parser, wrapping all intellisense functionality.
    /// </summary>
    public class DaxParserService
    {
        private readonly IModelMetadataProvider _metadata;
        private readonly DaxCompletionProvider _completionProvider;

        public DaxParserService(IModelMetadataProvider metadata)
        {
            _metadata = metadata;
            _completionProvider = metadata != null ? new DaxCompletionProvider(metadata) : null;
        }

        /// <summary>
        /// Parses DAX input and returns the parse result with any errors.
        /// </summary>
        public ParseResult Parse(string input)
        {
            ICharStream chars = new DAXCharStream(input);
            var lexer = new DAXLexer(chars);
            var errorListener = new DaxIntellisenseErrorListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(errorListener);

            var tokenStream = new CommonTokenStream(lexer);
            var parser = new DAXParser(tokenStream);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(errorListener);

            var strategy = new DaxIntellisenseErrorStrategy();
            parser.ErrorHandler = strategy;

            var tree = parser.daxQuery();

            return new ParseResult
            {
                Tree = tree,
                Errors = errorListener.Errors,
                Success = errorListener.Errors.Count == 0
            };
        }

        /// <summary>
        /// Gets the editing state at the cursor position for intellisense.
        /// </summary>
        public Metadata.DaxState GetEditState(string input, int cursorOffset)
        {
            return DaxCursorStateWalker.GetStateAtCursor(input, cursorOffset);
        }

        /// <summary>
        /// Gets completion items at the cursor position.
        /// </summary>
        public IReadOnlyList<CompletionItem> GetCompletions(string input, int cursorOffset)
        {
            if (_completionProvider == null)
                return new List<CompletionItem>();

            var state = GetEditState(input, cursorOffset);
            return _completionProvider.GetCompletions(state);
        }

        /// <summary>
        /// Gets completion items for a pre-computed DaxState.
        /// </summary>
        public IReadOnlyList<CompletionItem> GetCompletions(Metadata.DaxState state)
        {
            if (_completionProvider == null)
                return new List<CompletionItem>();

            return _completionProvider.GetCompletions(state);
        }

        /// <summary>
        /// Gets signature help at the cursor position.
        /// </summary>
        public SignatureHelpResult GetSignatureHelp(string input, int cursorOffset)
        {
            return DaxSignatureHelper.GetSignatureHelp(input, cursorOffset, _metadata);
        }

        /// <summary>
        /// Returns all functions defined in the query itself via DEFINE FUNCTION, including their
        /// parameter names, so signature/insight help can be provided for them.
        /// </summary>
        public IReadOnlyList<DefinedFunctionInfo> GetDefinedFunctions(string input)
        {
            var result = Parse(input);
            if (result.Tree == null) return new List<DefinedFunctionInfo>();

            var collector = new DefinedFunctionCollector();
            Antlr4.Runtime.Tree.ParseTreeWalker.Default.Walk(collector, result.Tree);
            return collector.Functions;
        }

        /// <summary>
        /// Returns the case-insensitive set of non-built-in function names that are actually called in the
        /// query outside of any <c>DEFINE FUNCTION</c> body (from the EVALUATE statement or DEFINE
        /// MEASURE/COLUMN/TABLE/VAR expressions). Used to exclude query-scoped functions that are declared
        /// but never invoked from a dependency tree.
        /// </summary>
        public ISet<string> GetReferencedFunctionNames(string input)
        {
            var result = Parse(input);
            if (result.Tree == null) return new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            var collector = new ReferencedFunctionCollector();
            Antlr4.Runtime.Tree.ParseTreeWalker.Default.Walk(collector, result.Tree);
            return collector.FunctionNames;
        }


        /// <summary>
        /// Returns, for every non-built-in function call in the query, the columns, measures and functions
        /// referenced in the arguments passed at that call site, keyed by the called function name. Lets a
        /// dependency tree list a query-scoped function's call-site arguments (e.g. <c>'Product'[Color]</c>
        /// in <c>queryFunc(VALUES('Product'[Color]))</c>) as children of the function.
        /// </summary>
        public IReadOnlyDictionary<string, List<DaxObjectReference>> GetFunctionCallArgumentReferences(string input)
        {
            var result = Parse(input);
            if (result.Tree == null) return new Dictionary<string, List<DaxObjectReference>>(System.StringComparer.OrdinalIgnoreCase);

            var collector = new FunctionCallArgumentCollector();
            Antlr4.Runtime.Tree.ParseTreeWalker.Default.Walk(collector, result.Tree);
            return (IReadOnlyDictionary<string, List<DaxObjectReference>>)collector.References;
        }

        /// <summary>
        /// Validates variable scoping rules in DAX input.
        /// Returns a list of scope errors (forward references, undefined variables).
        /// </summary>
        public List<DaxScopeValidator.ScopeError> ValidateScope(string input, IModelMetadataProvider metadata = null)
        {
            var result = Parse(input);
            if (!result.Success || result.Tree == null)
                return new List<DaxScopeValidator.ScopeError>();
            return DaxScopeValidator.Validate(result.Tree, metadata);
        }

        /// <summary>
        /// Parses DAX input and returns the structural foldable regions (DEFINE block, definitions,
        /// VAR/RETURN, EVALUATE, ORDER BY, function calls and table constructors) as character offset
        /// ranges. Uses parser error recovery so partial folds are returned for incomplete input.
        /// </summary>
        public IReadOnlyList<FoldRange> GetFoldings(string input)
        {
            var result = Parse(input);
            if (result.Tree == null) return new List<FoldRange>();

            var collector = new FoldingCollector();
            Antlr4.Runtime.Tree.ParseTreeWalker.Default.Walk(collector, result.Tree);
            return collector.Foldings;
        }

        /// <summary>
        /// Tokenizes DAX input and returns all tokens.
        /// </summary>
        public IList<IToken> Tokenize(string input)
        {
            ICharStream chars = new DAXCharStream(input);
            var lexer = new DAXLexer(chars);
            lexer.RemoveErrorListeners();
            return lexer.GetAllTokens();
        }
    }

    /// <summary>
    /// Result of a parse operation.
    /// </summary>
    public class ParseResult
    {
        public DAXParser.DaxQueryContext Tree { get; set; }
        public List<string> Errors { get; set; }
        public bool Success { get; set; }

        public ParseResult()
        {
            Errors = new List<string>();
        }
    }
}
