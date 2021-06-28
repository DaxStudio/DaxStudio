using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Xml;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using System.Text.RegularExpressions;
using DAXEditorControl.BracketRenderer;
using System.Windows.Media;
using DAXEditorControl.Renderers;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Controls;
using System.Text;
using ICSharpCode.AvalonEdit;
using System.ComponentModel;
using System.Windows.Documents;

namespace DAXEditorControl
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:DAXEditor"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:DAXEditor;assembly=DAXEditor"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:CustomControl1/>
    ///
    /// </summary>
    /// 
    public struct HighlightPosition : IEquatable<HighlightPosition>
    {
        
        public int Length { get; set; }
        public int Index { get; set; }


        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(HighlightPosition left, HighlightPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(HighlightPosition left, HighlightPosition right)
        {
            return !(left == right);
        }

        public bool Equals(HighlightPosition other)
        {
            return Index == other.Index && Length == other.Length;
        }

        public override bool Equals(object obj)
        {
            return obj is HighlightPosition position && Equals(position);
        }
    }
    public delegate List<HighlightPosition> HighlightDelegate(string text, int startOffset, int endOffset); 
    public partial class DAXEditor : ICSharpCode.AvalonEdit.TextEditor, IEditor, IDisposable
    {
        private readonly BracketRenderer.BracketHighlightRenderer _bracketRenderer;
        private WordHighlighTransformer _wordHighlighter;
        private readonly TextMarkerService _textMarkerService;
        private ToolTip _toolTip;
        private bool _syntaxErrorDisplayed;
        private IHighlighter _documentHighlighter;

        static DAXEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(DAXEditor), new FrameworkPropertyMetadata(typeof(DAXEditor)));
        }

        public DAXEditor() 
        {
            //DefaultStyleKeyProperty.OverrideMetadata(typeof(DAXEditor), new FrameworkPropertyMetadata(typeof(DAXEditor)));

            //SearchPanel.Install(this.TextArea);
            var brush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#C8FFA55F")); //orange // grey FFE6E6E6
            HighlightBackgroundBrush = brush;
            this.TextArea.SelectionChanged += TextEditor_TextArea_SelectionChanged;
            
            TextView textView = this.TextArea.TextView;

            // Add Bracket Highlighter
            _bracketRenderer = new BracketHighlightRenderer(textView);
            textView.BackgroundRenderers.Add(_bracketRenderer);
            //textView.LineTransformers.Add(_bracketRenderer);
            textView.Services.AddService(typeof(BracketHighlightRenderer), _bracketRenderer);

            // Add Syntax Error marker
            _textMarkerService = new TextMarkerService(this);
            textView.BackgroundRenderers.Add(_textMarkerService);
            textView.LineTransformers.Add(_textMarkerService);
            textView.Services.AddService(typeof(TextMarkerService), _textMarkerService);
            
            // add handlers for tooltip error display
            textView.MouseHover += TextEditorMouseHover;
            textView.MouseHoverStopped += TextEditorMouseHoverStopped;
            textView.VisualLinesChanged += VisualLinesChanged;

            //textView.PreviewKeyUp += TextArea_PreviewKeyUp;
            // add the stub Intellisense provider
            IntellisenseProvider = new IntellisenseProviderStub();

            this.DocumentChanged += DaxEditor_DocumentChanged;
            DataObject.AddPastingHandler(this, OnDataObjectPasting);

            RegiserKeyBindings();
        }

        private void RegiserKeyBindings()
        {
            
            //InputBindings.Add(new InputBinding( new HotKeyCommand(MoveLineUp) , new KeyGesture(Key.Up, ModifierKeys.Control | ModifierKeys.Shift)));
            //InputBindings.Add(new InputBinding(new HotKeyCommand(MoveLineDown), new KeyGesture(Key.Down, ModifierKeys.Control | ModifierKeys.Shift)));

            //InputBindings.Add(new InputBinding(new HotKeyCommand(MoveLineUp), new KeyGesture(Key.Up, ModifierKeys.Alt )));
            //InputBindings.Add(new InputBinding(new HotKeyCommand(MoveLineDown), new KeyGesture(Key.Down, ModifierKeys.Alt )));
        }

        public EventHandler<DataObjectPastingEventArgs> OnPasting { get; set; }

        // Raise Custom OnPasting event
        private void OnDataObjectPasting(object sender, DataObjectPastingEventArgs e)
        {
            OnPasting?.Invoke(sender, e);
        }

        internal void UpdateSyntaxRule(string colourName,  IEnumerable<string> wordList)
        {
            var kwordRule = this.SyntaxHighlighting.MainRuleSet.Rules.FirstOrDefault(r => r.Color.Name == colourName);
            var pattern = new StringBuilder();
            pattern.Append(@"\b(?>");

            // the syntaxhighlighting checks the first match so we need to make sure that the longer versions of
            // funtions come first. eg. CALCULATETABLE before CALCULATE, ALLSELECTED before ALL, etc
            // sorting them in descending order achieves this
            var sortedWordList = wordList.OrderByDescending(word => word);
            foreach (var word in sortedWordList)
            {
                pattern.Append(word.Replace(".", @"\."));
                pattern.Append('|');
            }
            pattern.Remove(pattern.Length - 1, 1);
            pattern.Append(@")\b");
            kwordRule.Regex = new Regex(pattern.ToString(), kwordRule.Regex.Options);
        }

        public void UpdateFunctionHighlighting(IEnumerable<string> functions)
        {
            UpdateSyntaxRule("Function", functions);
        }

        public void UpdateKeywordHighlighting(IEnumerable<string> keywords)
        {
            UpdateSyntaxRule("Kword", keywords);
        }

        public void ChangeColorBrightness(double factor)
        {
            foreach (var syntaxHighlight in SyntaxHighlighting.NamedHighlightingColors)
            {
                var foreground = syntaxHighlight.Foreground.GetColor(null);
                if (foreground == null) return;
                HSLColor hsl = new HSLColor((Color)foreground);
                hsl.Luminosity *= factor;
                syntaxHighlight.Foreground = new SimpleHighlightingBrush((Color)hsl);
            }

            //var funcCol = SyntaxHighlighting.NamedHighlightingColors.FirstOrDefault(c => c.Name == "Function");
            //var hex = "Blue";
            //System.Windows.Media.Color _color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex);
            //HSLColor hsl = new HSLColor(_color);
            //hsl.Luminosity = hsl.Luminosity * factor;
            //funcCol.Foreground = new SimpleHighlightingBrush((Color)hsl);
        }

        public void SetSyntaxHighlightColorTheme(string theme)
        {
            var prefix = theme + ".";
            foreach (var syntaxHighlight in SyntaxHighlighting.NamedHighlightingColors)
            {
                if (syntaxHighlight.Name.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    var suffix = syntaxHighlight.Name.Replace(prefix, "");
                    var baseColor = SyntaxHighlighting.NamedHighlightingColors.FirstOrDefault(color => color.Name == suffix);
                    if (baseColor != null)
                    {
                        baseColor.Foreground = syntaxHighlight.Foreground;
                        
                    }
                }
            }

            //var funcCol = SyntaxHighlighting.NamedHighlightingColors.FirstOrDefault(c => c.Name == "Function");
            //var hex = "Blue";
            //System.Windows.Media.Color _color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex);
            //HSLColor hsl = new HSLColor(_color);
            //hsl.Luminosity = hsl.Luminosity * factor;
            //funcCol.Foreground = new SimpleHighlightingBrush((Color)hsl);


        }

        private void DaxEditor_DocumentChanged(object sender, EventArgs e)
        {
            if (this.Document == null ) return;
            if (this.SyntaxHighlighting == null) return;
            _documentHighlighter = new DocumentHighlighter( this.Document, this.SyntaxHighlighting);
        }

        public bool IsInComment()
        {
            return IsInComment(this.TextArea.Caret.Offset);
        }

        public bool IsInComment(int offset)
        {
            var loc = this.Document.GetLocation(offset);
            return IsInComment(loc);
        }

        public bool IsInComment(TextLocation loc)
        {
            var pos = this.Document.GetOffset(loc);
            HighlightedLine result = _documentHighlighter.HighlightLine(loc.Line);
            bool isInComment = result.Sections.Any(
                s => s.Offset <= pos && s.Offset + s.Length >= pos
                     && s.Color.Name == "Comment");
            return isInComment;
        }

        void TextEditor_TextArea_SelectionChanged(object sender, EventArgs e)
        {
            this.TextArea.TextView.Redraw();
        }
        protected override void OnInitialized(EventArgs e)
        {
            
            base.OnInitialized(e);
            base.Loaded += OnLoaded;
            base.Unloaded += OnUnloaded;
            TextArea.TextEntering += TextEditor_TextArea_TextEntering;
            TextArea.TextEntered += TextEditor_TextArea_TextEntered;
            TextArea.PreviewKeyDown += TextArea_PreviewKeyDown;
            TextArea.ContextMenuOpening += TextArea_ContextMenuOpening;
            TextArea.Caret.PositionChanged += Caret_PositionChanged;
            this.TextChanged += TextArea_TextChanged;

            System.Reflection.Assembly myAssembly = System.Reflection.Assembly.GetAssembly(GetType());
            using (var s = myAssembly.GetManifestResourceStream("DAXEditor.Resources.DAX.xshd"))
            {

                using (var reader = new XmlTextReader(s) 
                { 
                    XmlResolver = null,
                    DtdProcessing = DtdProcessing.Prohibit 
                })
                {
                    SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
         
            // default settings - can be overridden in the settings dialog
            this.FontFamily = new FontFamily("Lucida Console");
            this.DefaultFontSize = 11.0;
            this.FontSize = DefaultFontSize;
            this.ShowLineNumbers = true;
            
        }

        private void TextArea_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var pos2 = Mouse.GetPosition(this);
            // get line at position
            var mousePos = new Point(e.CursorLeft, e.CursorTop);
            var pos = this.GetPositionFromPoint(pos2);
            if (pos == null) {
                ContextMenuWord = string.Empty;
            }
            else
            {
                var word = IntellisenseProvider.GetCurrentWord((TextViewPosition)pos);
                ContextMenuWord = word;
            }
            
            // get word
            // set contextMenuWord
        }

        void TextArea_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            IntellisenseProvider.ProcessKeyDown(sender, e);
        }

        private void TextArea_TextChanged(object sender, EventArgs e)
        {
            if (_syntaxErrorDisplayed)
            {
                ClearErrorMarkings();
            }
        }

        void Caret_PositionChanged(object sender, EventArgs e)
        {
            try
            {
                HighlightBrackets();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
            {
                // swallow all errors
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }

        public Brush HighlightBackgroundBrush { get; set; }

        private HighlightDelegate _highlightFunction;
        public HighlightDelegate HighlightFunction
               {
                   get { return _highlightFunction; }
                   set {
                       if (_highlightFunction != null)
                       { 
                           // remove the old function before adding the new one
                           this.TextArea.TextView.LineTransformers.Remove(_wordHighlighter); 
                       }
                       _highlightFunction = value;
                        _wordHighlighter = new WordHighlighTransformer(_highlightFunction, HighlightBackgroundBrush);
                        this.TextArea.TextView.LineTransformers.Add(_wordHighlighter);
                   }
               }

        private double _defaultFontSize = 11.0;
        public double DefaultFontSize {
            get { return _defaultFontSize; }
            set { _defaultFontSize = value;
                FontSize = _defaultFontSize;
            } 
        }

        public double FontScale
        {
            get => FontSize/DefaultFontSize * 100;
            set => FontSize = DefaultFontSize * value/100;
        }

        private readonly List<double> _fontScaleDefaultValues = new List<double>() {25.0, 50.0, 100.0, 200.0, 300.0, 400.0};
        public  List<double> FontScaleDefaultValues => _fontScaleDefaultValues;

        private void OnUnloaded(object sender, RoutedEventArgs routedEventArgs)
        {
            this.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            this.PreviewMouseWheel += OnPreviewMouseWheel;
        }

        private DaxStudioBracketSearcher FindBrackets
        { get; set; }

        

 public void HighlightBrackets()
 {
        //if (this.TextArea.Options.EnableHighlightBrackets == true)
      //{
        if (this.FindBrackets == null)
        {
          this.FindBrackets = new DaxStudioBracketSearcher();
        }
        var bracketSearchResult = FindBrackets.SearchBracket(this.Document, this.TextArea.Caret.Offset);
        this._bracketRenderer.SetHighlight(bracketSearchResult);
      //}
      //else
      //{
      //  this._bracketRenderer.SetHighlight(null);
      //}
}

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                double fontSize = this.FontSize + e.Delta / 25.0;

                if (fontSize < 6)
                    this.FontSize = 6;
                else
                {
                    if (fontSize > 200)
                        this.FontSize = 200;
                    else
                        this.FontSize = fontSize;
                }

                e.Handled = true;
            }
        }

        private IIntellisenseProvider IntellisenseProvider { get; set; }

        CompletionWindow _completionWindow;
        public InsightWindow InsightWindow { get; set; }

        TextArea IEditor.TextArea => TextArea;

        void TextEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            IntellisenseProvider.ProcessTextEntered(sender, e,ref _completionWindow);
        }

        const string COMMENT_DELIM_SLASH="//";
        const string COMMENT_DELIM_DASH = "--";
        //private bool IsLineCommented(DocumentLine line)
        //{
        //    var trimmed =  this.Document.GetText(line.Offset,line.Length).Trim();
        //    return trimmed.IndexOf(COMMENT_DELIM_DASH, StringComparison.InvariantCultureIgnoreCase).Equals(0) 
        //        || trimmed.IndexOf(COMMENT_DELIM_SLASH, StringComparison.InvariantCultureIgnoreCase).Equals(0);
        //}

        #region "Commenting/Uncommenting"
        private static readonly IFormatProvider invariantCulture = System.Globalization.CultureInfo.InvariantCulture;

        private readonly Regex rxUncommentSlashes = new Regex(string.Format(invariantCulture,"^(\\s*){0}",COMMENT_DELIM_SLASH), RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex rxUncommentDashes = new Regex(string.Format(invariantCulture,"^(\\s*){0}", COMMENT_DELIM_DASH), RegexOptions.Compiled | RegexOptions.Multiline);
        private readonly Regex rxComment = new Regex("^(.*)", RegexOptions.Compiled | RegexOptions.Multiline);
        //private Regex rxComment = new Regex("^(\\s*)", RegexOptions.Compiled | RegexOptions.Multiline);
        private void SelectFullLines()
        {
            int selStart = Document.GetLineByOffset(SelectionStart).Offset;
            int selLength = Document.GetLineByOffset(SelectionStart + SelectionLength).EndOffset - selStart;
            SelectionStart = selStart;
            SelectionLength = selLength;
        }

        public void CommentSelectedLines()
        {
            SelectFullLines();
            SelectedText = rxComment.Replace(SelectedText, string.Format(invariantCulture,"{0}$1",COMMENT_DELIM_SLASH));
        }

        public bool AllLinesCommented()
        {
            
            foreach (var line in SelectedText.Split('\n'))
            {
                if (!line.StartsWith(COMMENT_DELIM_DASH)) return false;

            }

            return true;
        }
        
        public void ToggleCommentSelectedLines()
        {
            SelectFullLines();
            if (AllLinesCommented())
            {
                CommentSelectedLines();
            }
                else
            {
                
                UncommentSelectedLines();
            }
        }

        public void UncommentSelectedLines()
        {
            SelectFullLines();
            if (SelectedText.TrimStart().StartsWith(COMMENT_DELIM_SLASH, StringComparison.InvariantCultureIgnoreCase))
            {  SelectedText = rxUncommentSlashes.Replace(SelectedText, "$1"); }
            if (SelectedText.TrimStart().StartsWith(COMMENT_DELIM_DASH, StringComparison.InvariantCultureIgnoreCase))
            { SelectedText = rxUncommentDashes.Replace(SelectedText, "$1"); }
        }

        #endregion

        void TextEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            IntellisenseProvider.ProcessTextEntering(sender, e, ref _completionWindow);
        }

        
      

        public TextLocation DocumentGetLocation(int offset)
        {
            return this.Document.GetLocation(offset);
        }

        public void DocumentReplace(int offset, int length, string newText)
        {
            this.Document.Replace(offset, length, newText);
        }

        public void DisplayErrorMarkings( int line, int column, int length, string message)
        {
            
            // remove any previous error markers
            ClearErrorMarkings();

            if (line >= 1 && line <= this.Document.LineCount)
            {
                int offset = this.Document.GetOffset(new TextLocation(line, column));
                if (offset == this.Document.TextLength && this.Document.TextLength >= 1) offset -= 1;

                if (this.Document.GetText(offset, 1) == "'") { }

                int endOffset = TextUtilities.GetNextCaretPosition(this.Document, offset, System.Windows.Documents.LogicalDirection.Forward, CaretPositioningMode.WordBorder);
                if (length <= 1) length = endOffset - offset;
                if (length <= 0) length = 1;
                
                _textMarkerService.Create(offset, length, message);
                _syntaxErrorDisplayed = true;
            }    

        }

        public void ClearErrorMarkings()
        {
            IServiceProvider sp = this;
            var markerService = (TextMarkerService)sp.GetService(typeof(TextMarkerService));
            markerService.Clear();
            if (_toolTip != null)
            {
                _toolTip.IsOpen = false;
            }
            _syntaxErrorDisplayed = false;
        }

        private void TextEditorMouseHover(object sender, MouseEventArgs e)
        {
            var pos = this.TextArea.TextView.GetPositionFloor(e.GetPosition(this.TextArea.TextView) + this.TextArea.TextView.ScrollOffset);
            bool inDocument = pos.HasValue;
            if (inDocument)
            {
                TextLocation logicalPosition = pos.Value.Location;
                int offset = this.Document.GetOffset(logicalPosition);

                var markersAtOffset = _textMarkerService.GetMarkersAtOffset(offset);
                TextMarkerService.TextMarker markerWithToolTip = markersAtOffset.FirstOrDefault(marker => marker.ToolTip != null);

                if (markerWithToolTip != null)
                {
                    if (_toolTip == null)
                    {
                        _toolTip = new ToolTip();
                        _toolTip.Closed += ToolTipClosed;
                        _toolTip.PlacementTarget = this;
                        _toolTip.Content = new TextBlock
                        {
                            Text = markerWithToolTip.ToolTip,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxHeight = 50,
                            MaxWidth = 600
                        };
                        _toolTip.IsOpen = true;
                        e.Handled = true;
                    }
                }
            }
        }

        void ToolTipClosed(object sender, RoutedEventArgs e)
        {
            _toolTip = null;
        }

        void TextEditorMouseHoverStopped(object sender, MouseEventArgs e)
        {
            if (_toolTip != null)
            {
                _toolTip.IsOpen = false;
                e.Handled = true;
            }
        }

        private void VisualLinesChanged(object sender, EventArgs e)
        {
            if (_toolTip != null)
            {
                _toolTip.IsOpen = false;
            }
        }

        private readonly object _disposeLock = new object();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any errors when closing the completion window should be swallowed")]
        public void DisposeCompletionWindow()
        {
            System.Diagnostics.Debug.WriteLine($"DaxEditor.DisposeCompletionWindow (IsMouseOverCompletionWindow: {_completionWindow?.IsMouseOver}");
            //if (IsMouseOverCompletionWindow) return;
            if (_completionWindow != null && _completionWindow.IsMouseOver) return;

            lock (_disposeLock)
            {
                // close function tooltip if it is open
               if (_toolTip != null)
                    _toolTip.IsOpen = false;

                // force completion window to close
                if (_completionWindow == null) return;
                //if (_completionWindow.IsVisible) 
                try
                {
                    _completionWindow?.Close();
                } 
                catch 
                { 
                    //swallow any errors while trying to close the completion window
                }
                _completionWindow = null;
                return;
            }
        }

        public void DisableIntellisense()
        {
            IntellisenseProvider = new IntellisenseProviderStub();
        }

        public void EnableIntellisense(IIntellisenseProvider provider)
        {
            this.IntellisenseProvider = provider;
        }

        public DocumentLine DocumentGetLineByOffset(int pos)
        {
            return Document.GetLineByOffset(pos);
        }

        public DocumentLine DocumentGetLineByNumber(int line)
        {
            return Document.GetLineByNumber(line);
        }

        public string DocumentGetText(int offset, int length)
        {
            return Document.GetText(offset, length);
        }

        public string DocumentGetText(TextSegment segment)
        {
            return Document.GetText(segment);
        }

        public void SetCaretPosition(int line, int column)
        {
            var offset = Document.GetOffset(line, column);
            CaretOffset = offset;
        }

        public string GetCurrentWord(TextViewPosition pos)
        {
            return IntellisenseProvider.GetCurrentWord(pos);
        }

        public int GetOffset(int line, int column)
        {
            return Document.GetOffset(line, column);
        }
        
        public void ShowInsightWindow(string functionName)
        {
            IntellisenseProvider?.ShowInsight(functionName);
        }

        public void ShowInsightWindow(string functionName, int offset)
        {
            IntellisenseProvider?.ShowInsight(functionName, offset);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _documentHighlighter.Dispose();
            }
        }

        public void SelectCurrentWord()
        {

            var offset = CaretOffset;

            if (offset >= Document.TextLength)
                offset--;

            int offsetStart = TextUtilities.GetNextCaretPosition(Document, offset, LogicalDirection.Backward, CaretPositioningMode.WordBorder);
            int offsetEnd = TextUtilities.GetNextCaretPosition(Document, offset, LogicalDirection.Forward, CaretPositioningMode.WordBorder);

            if (offsetEnd == -1 || offsetStart == -1)
                return;

            var currentChar = Document.GetText(offset, 1);

            if (string.IsNullOrWhiteSpace(currentChar))
                return;

            Select(offsetStart, offsetEnd - offsetStart);
            
        }

        public void MoveLineUp()
        {
            var currentLine = Document.GetLineByOffset(CaretOffset);
            if (currentLine.LineNumber == 1) return;
            var line = Document.GetText(currentLine.Offset, currentLine.TotalLength);
            if (currentLine.LineNumber == Document.LineCount)
            {
                // if this is the last line it does not have a trailing newline char
                // so we need to add one.
                line = line + '\n';
            }
            var prevLine = currentLine.PreviousLine;
            var prevOffset = prevLine?.Offset ?? 0;
            var sb = new StringBuilder(Document.Text);
            sb.Remove(currentLine.Offset, currentLine.TotalLength);
            sb.Insert(prevOffset, line);
            Document.Text = sb.ToString();

            // set the caret position to the line we just moved
            CaretOffset = prevOffset;

//            Select(currentLine.Offset, currentLine.TotalLength);
//            Cut();
//            CaretOffset = prevLine.Offset;
//            Paste();
//            CaretOffset = prevLine.Offset;
        }

        public void MoveLineDown()
        {
            var currentLine = Document.GetLineByOffset(CaretOffset);
            if (currentLine.LineNumber == Document.LineCount ) return;
            var nextLine = currentLine.NextLine;
            var line = Document.GetText(currentLine.Offset, currentLine.TotalLength);
            var currentOffset = currentLine.Offset;
            var nextLen = nextLine.TotalLength;
            var lastLineOffset = 0;
            if (nextLine.LineNumber == Document.LineCount) { 
                line = "\n" + line.TrimEnd();
                lastLineOffset = 1;
            }

            var sb = new StringBuilder(Document.Text);
            sb.Remove(currentOffset, currentLine.TotalLength);
            sb.Insert(currentOffset + nextLen, line);
            Document.Text = sb.ToString();

            // Set the caret position to the line we just moved
            CaretOffset = currentOffset + nextLen + lastLineOffset;

            //var currentLine = Document.GetLineByOffset(CaretOffset);
            //if (currentLine.LineNumber == Document.LineCount-1) return;
            //var nextLine = currentLine.NextLine;
            //Select(currentLine.Offset, currentLine.TotalLength);
            //Cut();
            //CaretOffset = currentLine.EndOffset;
            //Paste();

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }


}
