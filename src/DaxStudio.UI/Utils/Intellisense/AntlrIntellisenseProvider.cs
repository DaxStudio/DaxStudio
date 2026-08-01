using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using ADOTabular;
using Caliburn.Micro;
using DAXEditorControl;
using DaxStudio.Interfaces;
using DaxStudio.Core.Events;
using DaxStudio.Parsers.Dax;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Serilog;

namespace DaxStudio.UI.Utils.Intellisense
{
    /// <summary>
    /// Preview code-completion provider backed by the new ANTLR grammar-based DAX parser
    /// (<see cref="DaxParserService"/>) and the comment-script command grammar. It is used in place of
    /// <see cref="DaxIntellisenseProvider"/> when the <c>UseAntlrCodeCompletion</c> preview option is enabled.
    /// </summary>
    public class AntlrIntellisenseProvider :
        IDaxIntellisenseProvider,
        IInsightProvider,
        IHandle<MetadataLoadedEvent>,
        IHandle<DmvsLoadedEvent>,
        IHandle<FunctionsLoadedEvent>,
        IHandle<ConnectionPendingEvent>
    {
        private IEditor _editor;
        private DaxLineState _daxState;
        private bool _spacePressed;
        private readonly IEventAggregator _eventAggregator;
        private readonly IGlobalOptions _options;
        private DaxParserService _parserService;
        private const double BaseCompletionWindowWidth = 300;

        public AntlrIntellisenseProvider(IDaxDocument activeDocument, IEventAggregator eventAggregator, IGlobalOptions options)
        {
            Document = activeDocument;
            _eventAggregator = eventAggregator;
            _options = options;
        }

        #region Properties
        public ADOTabularModel Model { get; private set; }
        public IEditor Editor { get => _editor; set => _editor = value; }
        public IDaxDocument Document { get; }
        public ADOTabularDynamicManagementViewCollection DMVs { get; private set; }
        public ADOTabularFunctionGroupCollection FunctionGroups { get; private set; }
        public bool MetadataIsCached => Model != null && FunctionGroups != null && DMVs != null;
        #endregion

        private void RebuildParserService()
        {
            _parserService = Model != null
                ? new DaxParserService(new AdoTabularMetadataProvider(Model, FunctionGroups))
                : null;
        }

        #region Public IIntellisenseProvider Interface
        public void ProcessTextEntered(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow)
        {
            try
            {
                _daxState = ParseLine();

                // On a comment-script line a space is a meaningful separator between the command and
                // its sub-commands/arguments, so treat it as a trigger to (re)open the command list.
                // Space is not a normal completion trigger character, so this must be handled before the
                // generic completion-window handling below (which would otherwise filter to an empty list
                // and close the window).
                if (e.Text == " " && CommentScriptCompletionProvider.IsCommentScriptLine(GetCurrentLine()))
                {
                    CloseCompletionWindow();

                    completionWindow = CreateCompletionWindow(sender);
                    IList<ICompletionData> commentScriptData = completionWindow.CompletionList.CompletionData;
                    PopulateCompletionData(commentScriptData, true);

                    if (commentScriptData.Count > 0)
                    {
                        if (_editor.InsightWindow != null && _editor.InsightWindow.IsVisible)
                        {
                            _editor.InsightWindow.Visibility = Visibility.Collapsed;
                        }
                        completionWindow.Show();
                    }
                    else
                    {
                        CloseCompletionWindow();
                        completionWindow = null;
                    }
                    return;
                }

                // A '$' on a comment-script line starts a $(...) script-variable reference. It is handled
                // explicitly (rather than via the generic trigger handling below) so the completion list
                // can be anchored immediately after the '$' - the items are the bare variable names, so
                // that anchor is what lets the list filter on the name as the user keeps typing.
                if ((e.Text == "$" || (e.Text == "(" && IsPrecededByVariableDollar()))
                    && CommentScriptCompletionProvider.IsCommentScriptLine(GetCurrentLine()))
                {
                    CloseCompletionWindow();

                    completionWindow = CreateCompletionWindow(sender);
                    completionWindow.StartOffset = _editor.CaretOffset;
                    IList<ICompletionData> variableData = completionWindow.CompletionList.CompletionData;
                    PopulateCompletionData(variableData, true);

                    if (variableData.Count > 0)
                    {
                        if (_editor.InsightWindow != null && _editor.InsightWindow.IsVisible)
                        {
                            _editor.InsightWindow.Visibility = Visibility.Collapsed;
                        }
                        completionWindow.Show();
                    }
                    else
                    {
                        CloseCompletionWindow();
                        completionWindow = null;
                    }
                    return;
                }

                // A space typed after a DAX keyword (DEFINE, EVALUATE, ORDER BY, etc.) should open the
                // completion list showing what can validly follow. Space is not a normal trigger
                // character, so - like the comment-script case above - this is handled explicitly before
                // the generic window handling. The parser (via PopulateCompletionData) determines the
                // valid completions for the caret position; if it returns nothing the window is not shown.
                if (e.Text == " " && IsPrecededByDaxKeyword(GetCurrentLine()))
                {
                    CloseCompletionWindow();

                    var lineState = _daxState.LineState;
                    if (lineState != LineState.String && !_editor.IsInComment())
                    {
                        completionWindow = CreateCompletionWindow(sender);
                        IList<ICompletionData> keywordData = completionWindow.CompletionList.CompletionData;
                        PopulateCompletionData(keywordData, false);

                        if (keywordData.Count > 0)
                        {
                            if (_editor.InsightWindow != null && _editor.InsightWindow.IsVisible)
                            {
                                _editor.InsightWindow.Visibility = Visibility.Collapsed;
                            }
                            completionWindow.Show();
                        }
                        else
                        {
                            CloseCompletionWindow();
                            completionWindow = null;
                        }
                    }
                    return;
                }

                // Typing '.' immediately after $SYSTEM begins a DMV query, so show the list of Dynamic
                // Management Views. This must run before the generic completion-window handling below,
                // which would otherwise filter the currently-open window (e.g. the one that just
                // suggested $SYSTEM) to an empty list and close it without opening the DMV list.
                if (e.Text == "." && _daxState.LineState == LineState.Dmv)
                {
                    CloseCompletionWindow();
                    ShowDmvCompletionWindow(sender, ref completionWindow);
                    return;
                }

                // A '[' begins a new column/measure reference and an opening quote begins a new table
                // reference - both are definitive changes of completion context. If a completion window
                // is already open (which happens routinely while backspacing and re-typing) the generic
                // handling below would merely filter the previous context's list, so - for example -
                // typing '[' after a table name could show the model's measures instead of that table's
                // columns. Close any open window here and fall through to repopulate a fresh list for
                // the new context.
                if (completionWindow != null && (e.Text == "[" || e.Text == "'"))
                {
                    CloseCompletionWindow();
                    completionWindow = null;
                }

                if (completionWindow != null
                    && !string.IsNullOrWhiteSpace(e.Text)
                    && completionWindow.StartOffset == completionWindow.EndOffset)
                {
                    CloseCompletionWindow();
                }

                if (completionWindow != null)
                {
                    if (!completionWindow.CompletionList.ListBox.HasItems || !completionWindow.IsVisible)
                    {
                        CloseCompletionWindow();
                        return;
                    }

                    var document = ((TextArea)sender).Document;
                    var startOffset = completionWindow.StartOffset;
                    var endOffset = completionWindow.EndOffset;
                    if (startOffset > document.TextLength) startOffset = document.TextLength;
                    if (endOffset > document.TextLength) endOffset = document.TextLength;

                    var txt = document.GetText(new TextSegment() { StartOffset = startOffset, EndOffset = endOffset });
                    var selectedItem = completionWindow.CompletionList.SelectedItem;
                    if (selectedItem != null
                        && (string.Compare(selectedItem.Text, txt, true) == 0
                            || string.Compare(selectedItem.Content.ToString(), txt, true) == 0))
                    {
                        CloseCompletionWindow();
                    }
                    return;
                }

                var isCommentScript = CommentScriptCompletionProvider.IsCommentScriptLine(GetCurrentLine());

                if (char.IsLetterOrDigit(e.Text[0]) || "\'[$".Contains(e.Text[0]) || (isCommentScript && e.Text[0] == '>')
                    || (e.Text[0] == '.' && _daxState.LineState == LineState.Dmv))
                {
                    // exit if the completion window is already showing
                    if (completionWindow != null) return;

                    // exit if we are inside a string or comment (comment-script lines are handled explicitly)
                    if (!isCommentScript)
                    {
                        var lineState = _daxState.LineState;
                        if (lineState == LineState.String || _editor.IsInComment()) return;

                        // Don't show completions while typing a numeric literal. A token that starts with a
                        // digit and is not inside quotes/brackets is a number, not an identifier (table
                        // names that start with a digit must be single-quoted, in which case the token
                        // starts with the quote). For qualified/bracketed tokens the partial word starts
                        // with ' or [ so this check correctly leaves them alone.
                        if (char.IsDigit(e.Text[0]))
                        {
                            var partialToken = _editor.DocumentGetText(new TextSegment() { StartOffset = _daxState.StartOffset, EndOffset = _daxState.EndOffset });
                            if (!string.IsNullOrEmpty(partialToken) && char.IsDigit(partialToken[0])) return;
                        }
                    }

                    completionWindow = CreateCompletionWindow(sender);

                    if (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] == '[' || e.Text[0] == '$')
                    {
                        // Anchor the match segment at the start of the token so the text the completion
                        // list pre-selects/filters on matches the item text. Column and measure items
                        // include the surrounding brackets (e.g. "[Sales]"), so the '[' must be part of
                        // the match text - otherwise the typed prefix (without the bracket) won't
                        // prefix-match the items and an arbitrary item ends up pre-selected. A DMV
                        // query's "$SYSTEM" keyword includes the leading '$' (DaxLineParser keeps it as
                        // part of the word) so it also prefix-matches correctly.
                        completionWindow.StartOffset = _daxState.StartOffset;
                    }
                    else if (e.Text[0] == '\'')
                    {
                        // A quoted table reference. Anchor the filter AFTER the opening quote so the
                        // completion list filters on the (unquoted) name being typed and matches every
                        // table - both those that require quotes and those that don't. Anchoring on the
                        // quote itself would only ever match names that must be quoted (they are the only
                        // items whose text contains a quote), which is why previously typing a quote hid
                        // all the tables that don't need one.
                        completionWindow.StartOffset = _daxState.StartOffset + 1;
                    }

                    IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                    PopulateCompletionData(data, isCommentScript);

                    if (data.Count > 0)
                    {
                        // Pre-select using the same anchor the list uses for ongoing filtering so the
                        // initial selection and subsequent keystrokes stay consistent (an inconsistent
                        // anchor filtered the initial list differently from later keystrokes).
                        var matchStart = (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] == '[' || e.Text[0] == '\'' || e.Text[0] == '$')
                            ? completionWindow.StartOffset
                            : _daxState.StartOffset;
                        var txt = _editor.DocumentGetText(new TextSegment() { StartOffset = matchStart, EndOffset = _daxState.EndOffset });
                        completionWindow.CompletionList.SelectItem(txt);
                        if (completionWindow.CompletionList.ListBox.HasItems)
                        {
                            if (_editor.InsightWindow != null && _editor.InsightWindow.IsVisible)
                            {
                                _editor.InsightWindow.Visibility = Visibility.Collapsed;
                            }
                            completionWindow.Show();
                        }
                        else
                        {
                            CloseCompletionWindow();
                        }
                    }
                    else
                    {
                        CloseCompletionWindow();
                    }
                }

                if (e.Text[0] == '(')
                {
                    CloseCompletionWindow();
                    var funcName = DaxLineParser.GetPreceedingWord(GetCurrentLine().TrimEnd('(').Trim()).ToLower();
                    ShowInsight(funcName);
                }
                else if (e.Text[0] == ',')
                {
                    // A comma moves to the next argument of the enclosing function, so re-open the insight
                    // window for that function showing its argument list.
                    if (TryGetEnclosingFunctionName(out var funcName, out _))
                    {
                        ShowInsight(funcName);
                    }
                    else if (_editor?.InsightWindow?.IsVisible ?? false)
                    {
                        _editor.InsightWindow.Close();
                    }
                }
                else
                {
                    if (_editor?.InsightWindow?.IsVisible ?? false) _editor.InsightWindow.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, DaxStudio.Common.Constants.LogMessageTemplate, nameof(AntlrIntellisenseProvider), nameof(ProcessTextEntered), ex.Message);
                Document.OutputError($"Intellisense Disabled for this window - {ex.Message}");
            }
        }

        private void PopulateCompletionData(IList<ICompletionData> data, bool isCommentScript)
        {
            IReadOnlyList<CompletionItem> items;

            if (isCommentScript)
            {
                items = CommentScriptCompletionProvider.GetCompletions(GetCurrentLine(), GetTextBeforeCaret());
            }
            else
            {
                // DMV query: after "$SYSTEM." show the list of Dynamic Management Views rather than DAX
                // metadata (the DAX parser cannot parse the DMV/SQL syntax so would otherwise return
                // built-in functions).
                if (_daxState != null && _daxState.LineState == LineState.Dmv)
                {
                    PopulateDmvCompletionData(data);
                    return;
                }

                if (_parserService == null) return;
                var state = _parserService.GetEditState(_editor.Text, _editor.CaretOffset);
                var completions = _parserService.GetCompletions(state);

                // DMV/SQL keywords ($SYSTEM, SELECT, FROM, WHERE) start a DMV query, which is an
                // alternative to a whole DAX query. They are valid at the start of a statement
                // (TopLevel) and while the user is still typing the first word of that statement
                // (Identifier) - the same places DEFINE/EVALUATE are offered. As soon as a character
                // is typed the state becomes Identifier, so restricting to TopLevel alone hid them.
                // The keywords are added unfiltered; the completion window filters the visible list
                // against the text typed since its StartOffset (which, for "$SYSTEM", is anchored on
                // the '$'). Pre-filtering here on the ANTLR PartialText would drop "$SYSTEM" because
                // the lexer reports the partial as "SYSTEM" (without the leading '$').
                if (ShouldOfferDmvKeywords(state.State))
                {
                    completions = completions.Concat(_dmvKeywords).ToList();
                }
                items = completions;
            }

            var tmpData = items
                .OrderBy(item => KindSortRank(item.Kind))
                .ThenBy(item => item.Label, System.StringComparer.OrdinalIgnoreCase)
                .Select(item => (ICompletionData)new DaxCompletionData(this, item, isCommentScript));

            foreach (var itm in tmpData)
            {
                data.Add(itm);
            }
        }

        // DMV/SQL keywords surfaced alongside the normal DAX completions so a user can start a DMV query
        // (e.g. "select * from $SYSTEM.<dmv>"). Mirrors the keywords the legacy provider exposed.
        internal static readonly IReadOnlyList<CompletionItem> _dmvKeywords = new List<CompletionItem>
        {
            new CompletionItem("$SYSTEM", CompletionItemKind.Keyword, "Query the engine's Dynamic Management Views"),
            new CompletionItem("SELECT",  CompletionItemKind.Keyword, "DMV query SELECT clause"),
            new CompletionItem("FROM",    CompletionItemKind.Keyword, "DMV query FROM clause"),
            new CompletionItem("WHERE",   CompletionItemKind.Keyword, "DMV query WHERE clause"),
        };

        // DMV/SQL keywords are offered at the start of a statement (TopLevel) and while the user is
        // still typing the first word of that statement (Identifier). Typing any character moves the
        // parser from TopLevel to Identifier, so both states must be accepted for the keywords to
        // remain visible while typing (e.g. "SE" should still show SELECT).
        internal static bool ShouldOfferDmvKeywords(DaxStudio.Parsers.Metadata.EditState state)
        {
            return state == DaxStudio.Parsers.Metadata.EditState.TopLevel
                || state == DaxStudio.Parsers.Metadata.EditState.Identifier;
        }

        // Opens the completion window populated with the connection's DMVs (used when a DMV query is
        // started via "$SYSTEM.").
        private void ShowDmvCompletionWindow(object sender, ref CompletionWindow completionWindow)
        {
            completionWindow = CreateCompletionWindow(sender);
            IList<ICompletionData> dmvData = completionWindow.CompletionList.CompletionData;
            PopulateDmvCompletionData(dmvData);

            if (dmvData.Count > 0)
            {
                if (_editor.InsightWindow != null && _editor.InsightWindow.IsVisible)
                {
                    _editor.InsightWindow.Visibility = Visibility.Collapsed;
                }
                completionWindow.Show();
            }
            else
            {
                CloseCompletionWindow();
                completionWindow = null;
            }
        }

        // Populates the completion list with the connection's Dynamic Management Views (used after
        // "$SYSTEM." in a DMV query). Sorted alphabetically by name.
        private void PopulateDmvCompletionData(IList<ICompletionData> data)
        {
            var dmvs = Document?.Connection?.DynamicManagementViews;
            if (dmvs == null) return;

            foreach (var dmv in dmvs.OrderBy(d => d.Caption))
            {
                data.Add(new DaxCompletionData(this, dmv));
            }
        }

        // Called after the $SYSTEM keyword is completed from the list: inserts the '.' that always
        // follows it, then re-opens the completion window showing the list of DMVs. The window is opened
        // via the dispatcher so it runs after the completion that triggered this has finished closing its
        // own window (opening a new window synchronously mid-completion causes the two to conflict).
        private void InsertDmvSeparatorAndShowList()
        {
            var caret = _editor.CaretOffset;
            if (caret > _editor.Text.Length) caret = _editor.Text.Length;

            // insert the separator only if it isn't already there
            if (caret >= _editor.Text.Length || _editor.Text[caret] != '.')
            {
                _editor.DocumentReplace(caret, 0, ".");
                _editor.Select(caret + 1, 0);
            }

            _editor.TextArea?.Dispatcher.BeginInvoke(new System.Action(() =>
            {
                _daxState = ParseLine();
                if (_daxState.LineState != LineState.Dmv) return;

                var window = CreateCompletionWindow(_editor.TextArea);
                PopulateDmvCompletionData(window.CompletionList.CompletionData);
                if (window.CompletionList.CompletionData.Count > 0)
                {
                    _editor.ShowCompletionWindow(window);
                }
            }));
        }

        // Controls the order completion items appear in the list. Lower rank sorts higher. This groups
        // the more specific/local suggestions (variables, columns, measures, tables) above the generic
        // built-in functions and keywords so e.g. a table sorts above a similarly named function.
        private static int KindSortRank(CompletionItemKind kind)
        {
            switch (kind)
            {
                case CompletionItemKind.Variable: return 0;
                case CompletionItemKind.Measure: return 1;
                case CompletionItemKind.Column: return 2;
                case CompletionItemKind.Table: return 3;
                case CompletionItemKind.Calendar: return 4;
                case CompletionItemKind.Keyword: return 5;
                case CompletionItemKind.Function: return 6;
                default: return 6;
            }
        }

        // DAX keywords after which a space should re-open the completion list. Includes the multi-word
        // keywords (ORDER BY, START AT) which are matched against the last two words on the line.
        private static readonly HashSet<string> _keywordSpaceTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DEFINE", "EVALUATE", "MEASURE", "VAR", "RETURN", "COLUMN", "TABLE",
            "FUNCTION", "ORDER BY", "START AT", "ASC", "DESC",
        };

        // Returns true when the text up to the caret ends with a DAX keyword (single or two-word),
        // meaning a following space should trigger the completion list.
        private static bool IsPrecededByDaxKeyword(string lineUpToCaret)
        {
            if (string.IsNullOrWhiteSpace(lineUpToCaret)) return false;
            var words = lineUpToCaret.TrimEnd().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return false;
            if (_keywordSpaceTriggers.Contains(words[words.Length - 1])) return true;
            if (words.Length >= 2
                && _keywordSpaceTriggers.Contains(words[words.Length - 2] + " " + words[words.Length - 1])) return true;
            return false;
        }

        // Scans backwards from the caret to find the function whose argument list currently encloses the
        // caret, correctly skipping over nested (balanced) parentheses, string literals and line breaks.
        // Returns the lower-cased function name plus the zero-based index of the argument the caret is
        // currently positioned in (the count of top-level commas since the opening parenthesis).
        private bool TryGetEnclosingFunctionName(out string funcName, out int argumentIndex)
        {
            funcName = null;
            argumentIndex = 0;
            string text = _editor.Text;
            if (string.IsNullOrEmpty(text)) return false;

            int caret = _editor.CaretOffset;
            if (caret > text.Length) caret = text.Length;

            int depth = 0;
            int commaCount = 0;
            bool inString = false;
            for (int i = caret - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString) continue;

                if (c == ')') { depth++; continue; }
                if (c == ',') { if (depth == 0) commaCount++; continue; }
                if (c != '(') continue;

                if (depth > 0) { depth--; continue; }

                // Found the opening paren of the enclosing function - read the identifier before it.
                int end = i;
                while (end > 0 && char.IsWhiteSpace(text[end - 1])) end--;
                int start = end;
                while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '.' || text[start - 1] == '_'))
                    start--;

                if (start < end)
                {
                    funcName = text.Substring(start, end - start).ToLowerInvariant();
                    argumentIndex = commaCount;
                    return true;
                }
                return false;
            }
            return false;
        }

        public void ProcessTextEntering(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow)
        {
            if (e.Text.Length <= 0 || completionWindow == null) return;
            if (e.Text[0] == '(')
            {
                completionWindow.CompletionList.RequestInsertion(e);
            }
        }

        public void ProcessKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;  // swallow keystroke
            }
        }

        public string GetCurrentWord(TextViewPosition pos)
        {
            try
            {
                if (_editor.IsInComment()) return string.Empty;
                var docLine = _editor.DocumentGetLineByNumber(pos.Line);
                string line = _editor.DocumentGetText(docLine.Offset, docLine.Length);
                var lineState = DaxLineParser.ParseLine(line, pos.Column, 0);
                return line.Substring(lineState.StartOffset, lineState.EndOffset - lineState.StartOffset);
            }
            catch
            {
                return string.Empty;
            }
        }
        #endregion

        #region Insight (signature help) windows
        public void ShowInsight(string funcName)
        {
            ShowInsight(funcName, -1);
        }

        public string GetTableAssertionFromResults()
        {
            return TableAssertionInsertHelper.BuildFromResults(Document);
        }

        public void ShowInsight(string funcName, int offset)
        {
            funcName = funcName.TrimEnd('(');

            // The $SYSTEM keyword is only ever followed by ".<DMV>". When it is completed from the list,
            // append the '.' separator and immediately open the list of Dynamic Management Views so the
            // user doesn't have to type the dot themselves.
            if (string.Equals(funcName, "$SYSTEM", StringComparison.OrdinalIgnoreCase))
            {
                InsertDmvSeparatorAndShowList();
                return;
            }

            ADOTabularFunction f = Document?.Connection?.FunctionGroups?.GetByName(funcName);
            if (f != null)
            {
                ShowFunctionInsightWindow(offset, f);
                return;
            }

            if (Document?.Connection?.Keywords.Contains(funcName, StringComparer.OrdinalIgnoreCase) ?? false)
            {
                ShowKeywordInsightWindow(offset, funcName);
                return;
            }

            // Fall back to functions defined in the query itself via DEFINE FUNCTION - these are not part
            // of the connected model's metadata, so we read their parameter names from the parse tree.
            var definedFunctions = _parserService?.GetDefinedFunctions(_editor.Text);
            var definedFunction = definedFunctions?.FirstOrDefault(
                d => string.Equals(d.Name, funcName, StringComparison.OrdinalIgnoreCase));
            if (definedFunction != null)
            {
                ShowDefinedFunctionInsightWindow(offset, definedFunction);
            }
        }

        private void ShowFunctionInsightWindow(int offset, ADOTabularFunction f)
        {
            try
            {
                // Determine which argument the caret is currently in, but only when the caret is actually
                // inside this function's parentheses (otherwise default to highlighting the first parameter).
                int currentParameter = 0;
                if (TryGetEnclosingFunctionName(out var enclosing, out var argIndex)
                    && string.Equals(enclosing, f.Caption, StringComparison.OrdinalIgnoreCase))
                {
                    currentParameter = argIndex;
                }

                _editor.InsightWindow = null;
                _editor.InsightWindow = new InsightWindow(_editor.TextArea);
                _editor.InsightWindow.Resources.MergedDictionaries.Add(InsightWindowCustomResources);
                if (offset > -1) _editor.InsightWindow.StartOffset = offset;
                _editor.InsightWindow.Content = BuildInsightFunctionContent(f, 400, currentParameter);
                try
                {
                    _editor.InsightWindow.Show();
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning("{class} {method} {message}", nameof(AntlrIntellisenseProvider), nameof(ShowInsight), "Error calling InsightWindow.Show(): " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}", nameof(AntlrIntellisenseProvider), nameof(ShowInsight), ex.Message);
            }
        }

        private void ShowKeywordInsightWindow(int offset, string keyword)
        {
            try
            {
                _editor.InsightWindow = null;
                _editor.InsightWindow = new InsightWindow(_editor.TextArea);
                _editor.InsightWindow.Resources.MergedDictionaries.Add(InsightWindowCustomResources);
                if (offset > -1) _editor.InsightWindow.StartOffset = offset;
                _editor.InsightWindow.Content = BuildInsightKeywordContent(keyword, 400);
                try
                {
                    _editor.InsightWindow.Show();
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning("{class} {method} {message}", nameof(AntlrIntellisenseProvider), nameof(ShowInsight), "Error calling InsightWindow.Show(): " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}", nameof(AntlrIntellisenseProvider), nameof(ShowInsight), ex.Message);
            }
        }

        private void ShowDefinedFunctionInsightWindow(int offset, DaxStudio.Parsers.Metadata.DefinedFunctionInfo fn)
        {
            try
            {
                // Determine which argument the caret is in, but only when the caret is actually inside
                // this function's parentheses (otherwise default to highlighting the first parameter).
                int currentParameter = 0;
                if (TryGetEnclosingFunctionName(out var enclosing, out var argIndex)
                    && string.Equals(enclosing, fn.Name, StringComparison.OrdinalIgnoreCase))
                {
                    currentParameter = argIndex;
                }

                _editor.InsightWindow = null;
                _editor.InsightWindow = new InsightWindow(_editor.TextArea);
                _editor.InsightWindow.Resources.MergedDictionaries.Add(InsightWindowCustomResources);
                if (offset > -1) _editor.InsightWindow.StartOffset = offset;
                _editor.InsightWindow.Content = BuildInsightDefinedFunctionContent(fn, 400, currentParameter);
                try
                {
                    _editor.InsightWindow.Show();
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning("{class} {method} {message}", nameof(AntlrIntellisenseProvider), nameof(ShowInsight), "Error calling InsightWindow.Show(): " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}", nameof(AntlrIntellisenseProvider), nameof(ShowInsight), ex.Message);
            }
        }

        private UIElement BuildInsightDefinedFunctionContent(DaxStudio.Parsers.Metadata.DefinedFunctionInfo fn, int maxWidth, int currentParameter)
        {
            var grd = new Grid();
            grd.ColumnDefinitions.Add(new ColumnDefinition() { MaxWidth = maxWidth });
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };

            var parameters = fn.Parameters ?? new List<DaxStudio.Parsers.Metadata.DefinedFunctionParameter>();
            int highlightIndex = (currentParameter >= 0 && currentParameter < parameters.Count) ? currentParameter : -1;

            tb.Inlines.Add(new Bold(new Run(fn.Name)));
            tb.Inlines.Add(new Run("("));
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i > 0) tb.Inlines.Add(new Run(", "));

                var paramRun = new Run($"«{parameters[i].Name}»");
                if (i == highlightIndex)
                {
                    tb.Inlines.Add(new Bold(new Underline(paramRun)));
                }
                else
                {
                    tb.Inlines.Add(paramRun);
                }
            }
            tb.Inlines.Add(new Run(")"));

            tb.Inlines.Add("\n");
            tb.Inlines.Add("User-defined function");

            Grid.SetColumn(tb, 0);
            grd.Children.Add(tb);
            return grd;
        }

        private UIElement BuildInsightFunctionContent(ADOTabularFunction f, int maxWidth, int currentParameter = 0)
        {
            var grd = new Grid();
            grd.ColumnDefinitions.Add(new ColumnDefinition() { MaxWidth = maxWidth });
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };

            // Render the signature as "Function(param1, param2, ...)" with the parameter for the argument
            // the caret is currently in highlighted (bold + underline) so the user can see which argument
            // they are editing.
            var parameters = f.Parameters?.ToList() ?? new List<ADOTabularFunctionArgument>();

            int highlightIndex = currentParameter;
            if (highlightIndex >= parameters.Count)
            {
                // When there are more supplied arguments than declared parameters the last parameter is
                // usually repeatable (e.g. SWITCH); highlight it, otherwise highlight nothing.
                highlightIndex = (parameters.Count > 0 && parameters[parameters.Count - 1].Repeatable)
                    ? parameters.Count - 1
                    : -1;
            }

            tb.Inlines.Add(new Bold(new Run(f.Caption)));
            tb.Inlines.Add(new Run("("));
            for (int i = 0; i < parameters.Count; i++)
            {
                if (i > 0) tb.Inlines.Add(new Run(", "));

                var paramRun = new Run(FormatFunctionParameter(parameters[i]));
                if (i == highlightIndex)
                {
                    tb.Inlines.Add(new Bold(new Underline(paramRun)));
                }
                else
                {
                    tb.Inlines.Add(paramRun);
                }
            }
            tb.Inlines.Add(new Run(")"));

            tb.Inlines.Add("\n");
            tb.Inlines.Add(f.Description);

            // Show the description of the current parameter (when available) below the general description.
            if (highlightIndex >= 0 && highlightIndex < parameters.Count
                && !string.IsNullOrWhiteSpace(parameters[highlightIndex].Description))
            {
                tb.Inlines.Add("\n");
                tb.Inlines.Add(new Bold(new Run(parameters[highlightIndex].Name + ": ")));
                tb.Inlines.Add(parameters[highlightIndex].Description);
            }

            if (f.Group != "USERDEFINED")
            {
                var docLink = new Hyperlink();
                docLink.Inlines.Add($"https://dax.guide/{f.Caption}");
                docLink.NavigateUri = new Uri($"https://dax.guide/{f.Caption}/?aff=dax-studio");
                docLink.RequestNavigate += InsightHyperLinkNavigate;
                tb.Inlines.Add("\n");
                tb.Inlines.Add(docLink);
            }
            Grid.SetColumn(tb, 0);
            grd.Children.Add(tb);
            return grd;
        }

        // Formats a single function parameter using the same «name» / [«name»] / «name»,... conventions
        // used by ADOTabularFunctionArgumentCollection.ToString() for the whole signature.
        private static string FormatFunctionParameter(ADOTabularFunctionArgument arg)
        {
            if (arg.Optional && arg.Repeatable) return $"[«{arg.Name}»,...]";
            if (arg.Optional) return $"[«{arg.Name}»]";
            if (arg.Repeatable) return $"«{arg.Name}»,...";
            return $"«{arg.Name}»";
        }

        private UIElement BuildInsightKeywordContent(string keyword, int maxWidth)
        {
            var grd = new Grid();
            grd.ColumnDefinitions.Add(new ColumnDefinition() { MaxWidth = maxWidth });
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var caption = new Run(keyword);
            tb.Inlines.Add(new Bold(caption));
            tb.Inlines.Add(new Run(" «Keyword»"));

            var docLink = new Hyperlink();
            docLink.Inlines.Add($"https://dax.guide/{keyword}");
            docLink.NavigateUri = new Uri($"https://dax.guide/{keyword}/?aff=dax-studio");
            docLink.RequestNavigate += InsightHyperLinkNavigate;
            tb.Inlines.Add("\n");
            tb.Inlines.Add(docLink);
            Grid.SetColumn(tb, 0);
            grd.Children.Add(tb);
            return grd;
        }

        private void InsightHyperLinkNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
        #endregion

        #region Completion window management
        private CompletionWindow CreateCompletionWindow(object sender)
        {
            DaxStudioCompletionWindow completionWindow = new DaxStudioCompletionWindow(sender as TextArea);
            completionWindow.ResizeMode = ResizeMode.NoResize;
            completionWindow.Width = BaseCompletionWindowWidth * (_options.CodeCompletionWindowWidthIncrease / 100);
            completionWindow.CloseAutomatically = false;
            completionWindow.WindowStyle = WindowStyle.None;
            completionWindow.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
            completionWindow.AllowsTransparency = true;
            AssignResouceDictionary(completionWindow);
            AttachCompletionWindowEvents(completionWindow);
            completionWindow.DetachCompletionEvents = DetachCompletionWindowEvents;
            return completionWindow;
        }

        private static string _codeCompletionResourcesUri = "pack://application:,,,/DaxStudio.UI;Component/Resources/Styles/CompletionList.xaml";
        private static ResourceDictionary _cachedResourceDictionary;
        private static ResourceDictionary CodeCompletionCustomResources
        {
            get
            {
                if (_cachedResourceDictionary == null)
                    _cachedResourceDictionary = new ResourceDictionary() { Source = new Uri(_codeCompletionResourcesUri) };
                return _cachedResourceDictionary;
            }
        }

        private static string _insightWindowResourcesUri = "pack://application:,,,/DaxStudio.UI;Component/Resources/Styles/InsightWindow.xaml";
        private static ResourceDictionary _insightWindowCachedResourceDictionary;
        private static ResourceDictionary InsightWindowCustomResources
        {
            get
            {
                if (_insightWindowCachedResourceDictionary == null)
                    _insightWindowCachedResourceDictionary = new ResourceDictionary() { Source = new Uri(_insightWindowResourcesUri) };
                return _insightWindowCachedResourceDictionary;
            }
        }

        private static void AssignResouceDictionary(DaxStudioCompletionWindow completionWindow)
        {
            completionWindow.Resources.MergedDictionaries.Add(CodeCompletionCustomResources);
        }

        private void AttachCompletionWindowEvents(CompletionWindow completionWindow)
        {
            completionWindow.PreviewKeyUp += CompletionWindow_PreviewKeyUp;
            completionWindow.Closing += completionWindow_Closing;
            completionWindow.PreviewKeyUp += completionWindow_PreviewKeyUp;
            completionWindow.MouseEnter += completionWindow_MouseEnter;
            completionWindow.MouseLeave += completionWindow_MouseLeave;
            completionWindow.Closed += completionWindow_Closed;
        }

        private void DetachCompletionWindowEvents(CompletionWindow completionWindow)
        {
            completionWindow.PreviewKeyUp -= CompletionWindow_PreviewKeyUp;
            completionWindow.Closing -= completionWindow_Closing;
            completionWindow.PreviewKeyUp -= completionWindow_PreviewKeyUp;
            completionWindow.MouseEnter -= completionWindow_MouseEnter;
            completionWindow.MouseLeave -= completionWindow_MouseLeave;
        }

        private void completionWindow_Closed(object sender, EventArgs e)
        {
            CloseCompletionWindow();
        }

        private void completionWindow_MouseLeave(object sender, MouseEventArgs e)
        {
            _editor.IsMouseOverCompletionWindow = false;
        }

        private void completionWindow_MouseEnter(object sender, MouseEventArgs e)
        {
            _editor.IsMouseOverCompletionWindow = true;
        }

        private void CompletionWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.Right:
                case Key.OemCloseBrackets:
                case Key.Escape:
                    CloseCompletionWindow();
                    break;
            }
        }

        void completionWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            var completionWindow = (CompletionWindow)sender;
            _spacePressed = e.Key == Key.Space;
            var keyStr = e.Key.ToString();
            if (keyStr == _options.HotkeyRunQuery
                || keyStr == _options.HotkeyRunQueryAlt
                || keyStr == _options.HotkeyFormatQueryStandard
                || keyStr == _options.HotkeyFormatQueryAlternate)
            {
                CloseCompletionWindow();
            }
        }

        void completionWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var lineState = ParseLine();
            if (_spacePressed && (lineState.LineState == LineState.Column || lineState.LineState == LineState.Table))
            {
                e.Cancel = true;
            }
        }

        private readonly object _completionWindowCloseLock = new object();
        public void CloseCompletionWindow()
        {
            lock (_completionWindowCloseLock)
            {
                _editor.InsightWindow?.Close();
                _editor.DisposeCompletionWindow();
            }
        }
        #endregion

        #region Line helpers
        private DaxLineState ParseLine()
        {
            string line = GetCurrentLine();
            int pos = _editor.CaretOffset > 0 ? _editor.CaretOffset - 1 : 0;
            var loc = _editor.DocumentGetLocation(pos);
            var docLine = _editor.DocumentGetLineByOffset(pos);
            return DaxLineParser.ParseLine(line, loc.Column, docLine.Offset);
        }

        private string GetCurrentLine()
        {
            int pos = _editor.CaretOffset > 0 ? _editor.CaretOffset - 1 : 0;
            var loc = _editor.DocumentGetLocation(pos);
            var docLine = _editor.DocumentGetLineByOffset(pos);
            if (docLine.Length == 0) return "";
            string line = _editor.DocumentGetText(docLine.Offset, loc.Column);
            return line;
        }

        // The script text preceding the caret. Used by the comment-script completions to find the
        // "--> SET" variables that are in scope at the caret (a SET is only visible to the commands
        // that follow it).
        private string GetTextBeforeCaret()
        {
            var caret = _editor.CaretOffset;
            if (caret <= 0) return string.Empty;
            return _editor.DocumentGetText(0, caret);
        }

        // True when the '(' just typed completes the "$(" that opens a script-variable reference (and is
        // not the "$$(" escape sequence for a literal "$(" ).
        private bool IsPrecededByVariableDollar()
        {
            var caret = _editor.CaretOffset;
            if (caret < 2) return false;
            var before = _editor.DocumentGetText(caret - 2, 1);
            if (before != "$") return false;
            if (caret >= 3 && _editor.DocumentGetText(caret - 3, 1) == "$") return false;
            return true;
        }
        #endregion

        #region Event handlers
        public Task HandleAsync(MetadataLoadedEvent message, CancellationToken cancellationToken)
        {
            if (message.Document == Document)
            {
                Model = message.Model;
                RebuildParserService();
            }
            return Task.CompletedTask;
        }

        public Task HandleAsync(DmvsLoadedEvent message, CancellationToken cancellationToken)
        {
            DMVs = message.DmvCollection;
            return Task.CompletedTask;
        }

        public Task HandleAsync(FunctionsLoadedEvent message, CancellationToken cancellationToken)
        {
            FunctionGroups = message.FunctionGroups;
            RebuildParserService();
            return Task.CompletedTask;
        }

        public Task HandleAsync(ConnectionPendingEvent message, CancellationToken cancellationToken)
        {
            if (message.Document == Document)
            {
                FunctionGroups = null;
                DMVs = null;
                Model = null;
                _parserService = null;
            }
            return Task.CompletedTask;
        }
        #endregion
    }
}
