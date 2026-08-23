using DaxStudio.UI.Utils.Intellisense;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using Serilog;
using System;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IDocument = ICSharpCode.AvalonEdit.Document.IDocument;

namespace DaxStudio.UI.Utils
{
    public class DaxCompletionData : ICompletionData
    {
        private readonly string _text;
        private readonly object _content;
        private readonly string _description;
        private readonly string _imageResource;
#pragma warning disable IDE0052 // Remove unread private members
        private double _priority = 120.0;
#pragma warning restore IDE0052 // Remove unread private members
        private IInsightProvider _insightProvider;
        private readonly bool _isCommentScript;
        private readonly bool _isFromResults;
        private readonly bool _isVariableRef;

        /*
public DaxCompletionData(IInsightProvider insightProvider, string text, string content, string description, ImageSource image )
{
_text = text;
_content = content;
_description = description;
_image = image;
_insightProvider = insightProvider;
}
*/
        public DaxCompletionData(IInsightProvider insightProvider, string text, double priority)
        {
            _text = text;
            _content = text;
            _description = text;
            _imageResource = string.Empty;
            _priority = priority;
            _insightProvider = insightProvider;
        }

        public DaxCompletionData(IInsightProvider insightProvider, ADOTabular.ADOTabularColumn column, DaxLineState state)
        {
            _text = string.Format("[{0}]", column.Name); //We need to use Name as Caption may be translated;
            _content = column.Caption;
            _description = string.IsNullOrEmpty(column.Description) ? null : column.Description;
            _imageResource = GetImageResource(column.MetadataImage);
            _priority = 50.0;
            _insightProvider = insightProvider;
            //_lineState = state;
        }

        public DaxCompletionData(IInsightProvider insightProvider, ADOTabular.ADOTabularDynamicManagementView dmv)
        {
            _text = dmv.Caption;
            _content = dmv.Caption;
            _description = "";  //TODO - maybe add restrictions list??
            _imageResource = "table_dmvDrawingImage";
            _priority = 50.0;
            _insightProvider = insightProvider;
        }
        public DaxCompletionData(IInsightProvider insightProvider, ADOTabular.ADOTabularFunction function)
        {
            _text = function.DaxName;
            _content = function.Caption;
            _description = string.IsNullOrEmpty(function.Description) ? function.Caption : function.Description;
            _imageResource = "functionDrawingImage";
            _insightProvider = insightProvider;
        }

        public DaxCompletionData(IInsightProvider insightProvider, ADOTabular.ADOTabularTable table, DaxLineState state)
        {
            _text = table.DaxName;
            _content = table.Caption;
            _description = string.IsNullOrEmpty(table.Description) ? null : table.Description;
            _imageResource = GetImageResource(table.MetadataImage);
            _priority = 100.0;
            _insightProvider = insightProvider;
        }

        public DaxCompletionData(IInsightProvider insightProvider, ADOTabular.ADOTabularCalendar calendar, DaxLineState state)
        {
            _text = calendar.DaxName;
            _content = calendar.Caption;
            _description = string.IsNullOrEmpty(calendar.Description) ? null : calendar.Description;
            _imageResource = GetImageResource(ADOTabular.ADOTabularCalendar.MetadataImage);
            _priority = 100.0;
            _insightProvider = insightProvider;
        }

        public DaxCompletionData(IInsightProvider insightProvider, DaxStudio.Parsers.Dax.CompletionItem item, bool isCommentScript = false)
        {
            // The "<from Results>" table-assertion helper carries a sentinel InsertText. Keep the
            // visible label as the completion Text so the list still filters as the user types (the
            // sentinel would never match), and flag it so Complete inserts the generated block instead.
            _isFromResults = isCommentScript
                && string.Equals(item.InsertText, DaxStudio.Parsers.Dax.CommentScriptCompletionProvider.FromResultsInsertText, StringComparison.Ordinal);
            // A comment-script $(...) variable reference. Its InsertText is the bare name (so the
            // completion list filters on what is typed after the '$') and the full "$(name)" syntax is
            // rebuilt when the item is inserted.
            _isVariableRef = isCommentScript && item.Kind == DaxStudio.Parsers.Dax.CompletionItemKind.Variable;
            _text = _isFromResults
                ? item.Label
                : (string.IsNullOrEmpty(item.InsertText) ? item.Label : item.InsertText);
            _content = item.Label;
            _description = string.IsNullOrEmpty(item.Description) ? null : item.Description;
            _imageResource = GetImageResource(item.Kind);
            _priority = GetPriority(item.Kind);
            _insightProvider = insightProvider;
            _isCommentScript = isCommentScript;
        }

        private static string GetImageResource(DaxStudio.Parsers.Dax.CompletionItemKind kind)
        {
            switch (kind)
            {
                case DaxStudio.Parsers.Dax.CompletionItemKind.Function:
                    return "functionDrawingImage";
                case DaxStudio.Parsers.Dax.CompletionItemKind.Table:
                    return "tableDrawingImage";
                case DaxStudio.Parsers.Dax.CompletionItemKind.Column:
                    return "columnDrawingImage";
                case DaxStudio.Parsers.Dax.CompletionItemKind.Measure:
                    return "measureDrawingImage";
                case DaxStudio.Parsers.Dax.CompletionItemKind.Calendar:
                    return "datetimeDrawingImage";
                case DaxStudio.Parsers.Dax.CompletionItemKind.Variable:
                case DaxStudio.Parsers.Dax.CompletionItemKind.Keyword:
                default:
                    return string.Empty;
            }
        }

        private static double GetPriority(DaxStudio.Parsers.Dax.CompletionItemKind kind)
        {
            switch (kind)
            {
                case DaxStudio.Parsers.Dax.CompletionItemKind.Column:
                case DaxStudio.Parsers.Dax.CompletionItemKind.Measure:
                    return 50.0;
                case DaxStudio.Parsers.Dax.CompletionItemKind.Table:
                case DaxStudio.Parsers.Dax.CompletionItemKind.Calendar:
                    return 100.0;
                case DaxStudio.Parsers.Dax.CompletionItemKind.Function:
                    return 120.0;
                case DaxStudio.Parsers.Dax.CompletionItemKind.Keyword:
                    return 200.0;
                default:
                    return 120.0;
            }
        }

        public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            CompleteInternal(textArea.Document, completionSegment, insertionRequestEventArgs);
        }

        public void CompleteInternal(ICSharpCode.AvalonEdit.Document.IDocument document, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            Log.Debug("{class} {method} {start}-{end}({length})", "DaxCompletionData", "Complete", completionSegment.Offset, completionSegment.EndOffset, completionSegment.Length);
            try
            {
                var funcParamStart = Text.IndexOf("«", StringComparison.OrdinalIgnoreCase);
                string insertionText = funcParamStart > 0 ? Text.Substring(0, funcParamStart) : Text;

                if (_isCommentScript)
                {
                    // The "<from Results>" table-assertion helper is a synthetic completion: instead
                    // of inserting its label we ask the insight provider to build a "-->> | ... |"
                    // block from the current query results.
                    if (_isFromResults)
                    {
                        var block = _insightProvider?.GetTableAssertionFromResults();
                        if (string.IsNullOrEmpty(block)) return;

                        int fromEnd = completionSegment.EndOffset;
                        int fromStart = fromEnd;
                        while (fromStart > 0)
                        {
                            var prev = document.GetCharAt(fromStart - 1);
                            if (char.IsWhiteSpace(prev) || prev == '>') break;
                            fromStart--;
                        }
                        document.Replace(fromStart, fromEnd - fromStart, block);
                        return;
                    }

                    // A script-variable reference: replace everything back to (and including) the '$'
                    // that opened it with the full "$(name)" syntax. The generic word-boundary walk
                    // below would also swallow the preceding quote of a path argument (e.g. "$).
                    if (_isVariableRef)
                    {
                        int varEnd = completionSegment.EndOffset;
                        int varStart = varEnd;
                        bool foundDollar = false;
                        while (varStart > 0)
                        {
                            var prev = document.GetCharAt(varStart - 1);
                            if (char.IsWhiteSpace(prev) || prev == '>') break;
                            varStart--;
                            if (prev == '$') { foundDollar = true; break; }
                        }
                        if (foundDollar)
                        {
                            document.Replace(varStart, varEnd - varStart, $"$({insertionText})");
                            return;
                        }
                    }

                    // Comment-script command lines are not DAX, so the DAX-aware word-boundary logic
                    // (which treats "-->" as a comment) would incorrectly consume the marker and the
                    // separating space. Instead replace only the partial word immediately before the
                    // caret, bounded by whitespace or the "-->" marker.
                    int csEnd = completionSegment.EndOffset;
                    int csStart = csEnd;
                    while (csStart > 0)
                    {
                        var prev = document.GetCharAt(csStart - 1);
                        if (char.IsWhiteSpace(prev) || prev == '>') break;
                        csStart--;
                    }
                    document.Replace(csStart, csEnd - csStart, insertionText);
                    _insightProvider.ShowInsight(insertionText);
                    return;
                }

                // walk back to start of word
                var newSegment = GetPreceedingWordSegment(document, completionSegment);
                var replaceOffset = newSegment.Offset;
                var replaceLength = newSegment.Length;

                if (insertionRequestEventArgs is TextCompositionEventArgs args)
                {
                    // if the insertion char is the same as the last char in the 
                    // insertion text then trim it off
                    var insertionChar = args.Text;
                    if (insertionText.EndsWith(insertionChar, StringComparison.Ordinal)) insertionText = insertionText.TrimEnd(insertionChar[0]);
                }

                // When the caret is in the MIDDLE of an existing identifier the segment above only
                // reaches the caret, so the tail of the old word would be left behind - e.g. editing
                // "SELE|COLUMNS" to SELECTCOLUMNS would produce "SELECTCOLUMNSCOLUMNS". Extend the
                // replaced range to the end of the identifier so the whole word is replaced. This is
                // skipped when the completion itself opens a new call/reference (its text ends with
                // "(", "[" or a quote) because those are "wrapping" inserts placed at the caret that
                // must preserve the following text - e.g. inserting "FILTER(" before "VALUES(...)" to
                // get "FILTER(VALUES(...))".
                if (insertionText.Length > 0 && !EndsWithWrappingChar(insertionText)
                    && completionSegment.EndOffset < document.TextLength
                    && IsIdentifierChar(document.GetCharAt(completionSegment.EndOffset)))
                {
                    int wordEnd = completionSegment.EndOffset;
                    while (wordEnd < document.TextLength && IsIdentifierChar(document.GetCharAt(wordEnd))) wordEnd++;
                    var extendedLength = wordEnd - replaceOffset;
                    if (extendedLength > replaceLength) replaceLength = extendedLength;
                }

                document.Replace(replaceOffset, replaceLength, insertionText);
                _insightProvider.ShowInsight(insertionText);
            } catch (Exception ex)
            {
                Log.Fatal(ex, "{class} {method} Error inserting code completion data {message}", "DaxCompletionData", "CompleteInternal", ex.Message);
            }
        }

        private LinePosition GetPreceedingWordSegment(ICSharpCode.AvalonEdit.Document.IDocument document, ISegment completionSegment)
        {
            string line = "";

            int pos = completionSegment.EndOffset - 1;
            var loc = document.GetLocation(pos);
            Log.Debug("{class} {method} pos:{position}", "DaxCompletionData", "GetPreceedingWordSegment", pos);
            var docLine = document.GetLineByOffset(pos);
            //line = textArea.Document.GetText(docLine.Offset, loc.Column);
            line = document.GetText(docLine.Offset, docLine.Length);

            Log.Verbose("{class} {method} {message}", "DaxCompletionData", "GetPreceedingWordSegment", "line: " + line);
            var daxState = DaxLineParser.ParseLine(line, loc.Column, 0);
            //TODO - look ahead to see if we have a table/column/function end character that we should replace upto
            var segment = DaxLineParser.GetPreceedingWordSegment(docLine.Offset, loc.Column, line, daxState);

            // The line parser anchors the start of the current "word" on the character that ended the
            // previous token, so when the caret sits immediately after a separator (e.g. "EVALUATE |" or
            // "FILTER(|") the returned segment covers that separator. Replacing it would delete the
            // space/bracket and produce invalid syntax, so any leading non-identifier characters are
            // skipped. Quoted/bracketed references are excluded from this as their segment deliberately
            // starts on the opening ' or [ which the inserted text includes again.
            switch (daxState.LineState)
            {
                case LineState.String:
                case LineState.Table:
                case LineState.TableClosed:
                case LineState.Column:
                case LineState.ColumnClosed:
                case LineState.Measure:
                case LineState.MeasureClosed:
                case LineState.Dmv:
                    break;
                default:
                    while (segment.Length > 0)
                    {
                        var idx = segment.Offset - docLine.Offset;
                        if (idx < 0 || idx >= line.Length) break;
                        if (IsIdentifierChar(line[idx])) break;
                        segment.Offset++;
                        segment.Length--;
                    }
                    break;
            }

            return segment;

        }

        // A DAX identifier (function/keyword/DMV name) is made up of letters, digits, underscores and
        // '$' (used by DMV names like $SYSTEM). Used to find the end of an identifier the caret sits in.
        private static bool IsIdentifierChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$';
        }

        // A completion whose text opens a new call or reference ("FILTER(", "'Table"[..], "[Column")
        // is inserted at the caret to wrap the following text, so the current word must not be consumed.
        private static bool EndsWithWrappingChar(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            var last = text[text.Length - 1];
            return last == '(' || last == '[' || last == '\'' || last == '"';
        }

        public object Content
        {
            get { return _content; }
        }

        public object Description
        {
            get { return _description; }
        }

        public System.Windows.Media.ImageSource Image
        {
            get { return null; }
        }

        public string ImageResource
        {
            get { return _imageResource; }
        }

        public double Priority
        {
            get
            {
                return 0.0;//  _priority;
            }
        }

        public string Text
        {
            get { return _text; }
        }

        private ImageSource GetMetadataImage(ADOTabular.MetadataImages imageType)
        {
            switch (imageType)
            {
                case ADOTabular.MetadataImages.Column:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/Column.png" ));
                case ADOTabular.MetadataImages.Database:
                    break;
                case ADOTabular.MetadataImages.DmvTable:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/DmvTable.png"));
                case ADOTabular.MetadataImages.Folder:
                    break;
                case ADOTabular.MetadataImages.Function:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/Function.png" ));
                case ADOTabular.MetadataImages.HiddenColumn:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/HiddenColumn.png" ));
                case ADOTabular.MetadataImages.HiddenMeasure:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/HiddenMeasure.png" ));
                case ADOTabular.MetadataImages.HiddenTable:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/HiddenTable.png" ));
                case ADOTabular.MetadataImages.Hierarchy:
                case ADOTabular.MetadataImages.Kpi:
                    break;
                case ADOTabular.MetadataImages.Measure:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/Measure.png" ));
                case ADOTabular.MetadataImages.Model:
                    break;
                case ADOTabular.MetadataImages.Perspective:
                    break;
                case ADOTabular.MetadataImages.Table:
                    return new BitmapImage(new Uri("pack://application:,,,/DaxStudio.UI;component/images/Metadata/Table.png" ));
                default:
                    throw new NotImplementedException("Metadata image type not found");
            }
            return null;
        }

        private string GetImageResource(ADOTabular.MetadataImages imageType)
        {
            switch (imageType)
            {
                case ADOTabular.MetadataImages.Column:
                   return "columnDrawingImage";
                case ADOTabular.MetadataImages.Database:
                    break;
                case ADOTabular.MetadataImages.DmvTable:
                    return "table_dmvDrawingImage";
                case ADOTabular.MetadataImages.Folder:
                    break;
                case ADOTabular.MetadataImages.Function:
                    return "functionDrawingImage";
                case ADOTabular.MetadataImages.HiddenColumn:
                    return "columnDrawingImage";  // TODO - do we need a hidden version of this
                case ADOTabular.MetadataImages.HiddenMeasure:
                    return "measureDrawingImage";  // TODO - do we need a hidden version of this
                case ADOTabular.MetadataImages.HiddenTable:
                    return "tableDrawingImage";  // TODO - do we need a hidden version of this
                case ADOTabular.MetadataImages.Hierarchy:
                case ADOTabular.MetadataImages.Kpi:
                    break;
                case ADOTabular.MetadataImages.Measure:
                    return "measureDrawingImage";
                case ADOTabular.MetadataImages.Model:
                    break;
                case ADOTabular.MetadataImages.Perspective:
                    break;
                case ADOTabular.MetadataImages.Table:
                    return "tableDrawingImage";
                case ADOTabular.MetadataImages.Calendar:
                        return "datetimeDrawingImage";
                default:
                    throw new NotImplementedException("Metadata image type not found");
            }
            return null;
        }
    }

    public struct LinePosition
    {
#pragma warning disable CA1051 // Do not declare visible instance fields
        public int Offset;
        public int Length;
#pragma warning restore CA1051 // Do not declare visible instance fields
    }
}
