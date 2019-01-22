using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using DAXEditor.BracketRenderer;
using ICSharpCode.AvalonEdit.Search;
using System.Windows.Media;
using DAXEditor.Renderers;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Controls;
using System.Reflection;
using System.IO;
using System.Text;

namespace DAXEditor
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
    public struct HighlightPosition { public int Index; public int Length; 
        
    }
    public delegate List<HighlightPosition> HighlightDelegate(string text, int startOffset, int endOffset); 
    public partial class DAXEditor : ICSharpCode.AvalonEdit.TextEditor , IEditor
    {
        private readonly BracketRenderer.BracketHighlightRenderer _bracketRenderer;
        private WordHighlighTransformer _wordHighlighter;
        private readonly TextMarkerService textMarkerService;
        private ToolTip toolTip;
        private bool syntaxErrorDisplayed;
        private IHighlighter documentHighlighter;

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
            this.TextArea.SelectionChanged += textEditor_TextArea_SelectionChanged;
            TextView textView = this.TextArea.TextView;

            // Add Bracket Highlighter
            _bracketRenderer = new BracketHighlightRenderer(textView);
            textView.BackgroundRenderers.Add(_bracketRenderer);
            //textView.LineTransformers.Add(_bracketRenderer);
            textView.Services.AddService(typeof(BracketHighlightRenderer), _bracketRenderer);

            // Add Syntax Error marker
            textMarkerService = new TextMarkerService(this);
            textView.BackgroundRenderers.Add(textMarkerService);
            textView.LineTransformers.Add(textMarkerService);
            textView.Services.AddService(typeof(TextMarkerService), textMarkerService);

            // add handlers for tooltip error display
            textView.MouseHover += TextEditorMouseHover;
            textView.MouseHoverStopped += TextEditorMouseHoverStopped;
            textView.VisualLinesChanged += VisualLinesChanged;

            //textView.PreviewKeyUp += TextArea_PreviewKeyUp;
            // add the stub Intellisense provider
            IntellisenseProvider = new IntellisenseProviderStub();

            this.DocumentChanged += DaxEditor_DocumentChanged;
            DataObject.AddPastingHandler(this, OnDataObjectPasting);
            
        }

        public EventHandler<DataObjectPastingEventArgs> OnPasting;

        // Raise Custom OnPasting event
        private void OnDataObjectPasting(object sender, DataObjectPastingEventArgs e)
        {
            OnPasting?.Invoke(sender, e);
        }

        internal void UpdateSyntaxRule(string colourName,  IEnumerable<string> wordList)
        {
            var kwordRule = this.SyntaxHighlighting.MainRuleSet.Rules.Where(r => r.Color.Name == colourName).FirstOrDefault();
            var pattern = new StringBuilder();
            pattern.Append(@"\b(?>");

            // the syntaxhighlighting checks the first match so we need to make sure that the longer versions of
            // funtions come first. eg. CALCULATETABLE before CALCULATE, ALLSELECTED before ALL, etc
            // sorting them in descending order achieves this
            var sortedWordList = wordList.OrderByDescending(word => word);
            foreach (var word in sortedWordList)
            {
                pattern.Append(word.Replace(".", @"\."));
                pattern.Append("|");
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
                HSLColor hsl = new HSLColor((System.Windows.Media.Color)foreground);
                hsl.Luminosity = hsl.Luminosity * factor;
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
                if (syntaxHighlight.Name.StartsWith(prefix))
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
            documentHighlighter = new DocumentHighlighter( this.Document, this.SyntaxHighlighting);
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
            HighlightedLine result = documentHighlighter.HighlightLine(loc.Line);
            bool isInComment = result.Sections.Any(
                s => s.Offset <= pos && s.Offset + s.Length >= pos
                     && s.Color.Name == "Comment");
            return isInComment;
        }

        void textEditor_TextArea_SelectionChanged(object sender, EventArgs e)
        {
            this.TextArea.TextView.Redraw();
        }
        protected override void OnInitialized(EventArgs e)
        {
            
            base.OnInitialized(e);
            base.Loaded += OnLoaded;
            base.Unloaded += OnUnloaded;
            TextArea.TextEntering += textEditor_TextArea_TextEntering;
            TextArea.TextEntered += textEditor_TextArea_TextEntered;
            TextArea.PreviewKeyDown += TextArea_PreviewKeyDown;

            TextArea.Caret.PositionChanged += Caret_PositionChanged;
            this.TextChanged += TextArea_TextChanged;

            System.Reflection.Assembly myAssembly = System.Reflection.Assembly.GetAssembly(GetType());
            using (var s = myAssembly.GetManifestResourceStream("DAXEditor.Resources.DAX.xshd"))
            {
                using (var reader = new XmlTextReader(s))
                {
                    SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
         
            // default settings - can be overridden in the settings dialog
            this.FontFamily = new System.Windows.Media.FontFamily("Lucida Console");
            this.DefaultFontSize = 11.0;
            this.FontSize = DefaultFontSize;
            this.ShowLineNumbers = true;
            
        }

        void TextArea_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            IntellisenseProvider.ProcessKeyDown(sender, e);
        }

        private void TextArea_TextChanged(object sender, EventArgs e)
        {
            if (syntaxErrorDisplayed)
            {
                ClearErrorMarkings();
            }
        }

        void Caret_PositionChanged(object sender, EventArgs e)
        {
            try{
                HighlightBrackets();
            }
            catch {}
        }

        public Brush HighlightBackgroundBrush { get; set; }

        private HighlightDelegate _hightlightFunction;
        public HighlightDelegate HighlightFunction
               {
                   get { return _hightlightFunction; }
                   set {
                       if (_hightlightFunction != null)
                       { 
                           // remove the old function before adding the new one
                           this.TextArea.TextView.LineTransformers.Remove(_wordHighlighter); 
                       }
                       _hightlightFunction = value;
                        _wordHighlighter = new WordHighlighTransformer(_hightlightFunction, HighlightBackgroundBrush);
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
            get { return FontSize/DefaultFontSize * 100; }
            set { FontSize = DefaultFontSize * value/100; }
        }

        private readonly List<double> _fontScaleDefaultValues = new List<double>() {25.0, 50.0, 100.0, 200.0, 300.0, 400.0};
        public  List<double> FontScaleDefaultValues
        {
            get { return _fontScaleDefaultValues; }
        }

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

        CompletionWindow completionWindow;
        public InsightWindow InsightWindow { get; set; }

        TextArea IEditor.TextArea
        {
            get
            {
                return TextArea;
            }


        }

        void textEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            IntellisenseProvider.ProcessTextEntered(sender, e,ref completionWindow);
        }

        const string COMMENT_DELIM_SLASH="//";
        const string COMMENT_DELIM_DASH = "--";
        private bool IsLineCommented(DocumentLine line)
        {
            var trimmed =  this.Document.GetText(line.Offset,line.Length).Trim();
            return trimmed.IndexOf(COMMENT_DELIM_DASH).Equals(0) || trimmed.IndexOf(COMMENT_DELIM_SLASH).Equals(0);
        }

        #region "Commenting/Uncommenting"

        private Regex rxUncommentSlashes = new Regex(string.Format("^(\\s*){0}",COMMENT_DELIM_SLASH), RegexOptions.Compiled | RegexOptions.Multiline);
        private Regex rxUncommentDashes = new Regex(string.Format("^(\\s*){0}", COMMENT_DELIM_DASH), RegexOptions.Compiled | RegexOptions.Multiline);
        private Regex rxComment = new Regex("^(.*)", RegexOptions.Compiled | RegexOptions.Multiline);
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
            SelectedText = rxComment.Replace(SelectedText, string.Format("{0}$1",COMMENT_DELIM_SLASH));
        }

        public void UncommentSelectedLines()
        {
            SelectFullLines();
            if (SelectedText.TrimStart().StartsWith(COMMENT_DELIM_SLASH))
            {  SelectedText = rxUncommentSlashes.Replace(SelectedText, "$1"); }
            if (SelectedText.TrimStart().StartsWith(COMMENT_DELIM_DASH))
            { SelectedText = rxUncommentDashes.Replace(SelectedText, "$1"); }
        }

        #endregion

        void textEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            IntellisenseProvider.ProcessTextEntering(sender, e, ref completionWindow);
        }

        
      

        TextLocation IEditor.DocumentGetLocation(int offset)
        {
            return this.Document.GetLocation(offset);
        }

        void IEditor.DocumentReplace(int offset, int length, string newText)
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
                
                textMarkerService.Create(offset, length, message);
                syntaxErrorDisplayed = true;
            }    

        }

        public void ClearErrorMarkings()
        {
            IServiceProvider sp = this;
            var markerService = (TextMarkerService)sp.GetService(typeof(TextMarkerService));
            markerService.Clear();
            if (toolTip != null)
            {
                toolTip.IsOpen = false;
            }
            syntaxErrorDisplayed = false;
        }

        private void TextEditorMouseHover(object sender, MouseEventArgs e)
        {
            var pos = this.TextArea.TextView.GetPositionFloor(e.GetPosition(this.TextArea.TextView) + this.TextArea.TextView.ScrollOffset);
            bool inDocument = pos.HasValue;
            if (inDocument)
            {
                TextLocation logicalPosition = pos.Value.Location;
                int offset = this.Document.GetOffset(logicalPosition);

                var markersAtOffset = textMarkerService.GetMarkersAtOffset(offset);
                TextMarkerService.TextMarker markerWithToolTip = markersAtOffset.FirstOrDefault(marker => marker.ToolTip != null);

                if (markerWithToolTip != null)
                {
                    if (toolTip == null)
                    {
                        toolTip = new ToolTip();
                        toolTip.Closed += ToolTipClosed;
                        toolTip.PlacementTarget = this;
                        toolTip.Content = new TextBlock
                        {
                            Text = markerWithToolTip.ToolTip,
                            TextWrapping = TextWrapping.Wrap,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxHeight = 50,
                            MaxWidth = 600
                        };
                        toolTip.IsOpen = true;
                        e.Handled = true;
                    }
                }
            }
        }

        void ToolTipClosed(object sender, RoutedEventArgs e)
        {
            toolTip = null;
        }

        void TextEditorMouseHoverStopped(object sender, MouseEventArgs e)
        {
            if (toolTip != null)
            {
                toolTip.IsOpen = false;
                e.Handled = true;
            }
        }

        private void VisualLinesChanged(object sender, EventArgs e)
        {
            if (toolTip != null)
            {
                toolTip.IsOpen = false;
            }
        }
        public void DisposeCompletionWindow()
        {
            if (toolTip != null)
                toolTip.IsOpen = false;
            completionWindow?.Close();
            completionWindow = null;
            System.Diagnostics.Debug.WriteLine(">>> DisposeCompletionWindow");
        }

        public void DisableIntellisense()
        {
            this.IntellisenseProvider = new IntellisenseProviderStub();
        }

        public void EnableIntellisense(IIntellisenseProvider provider)
        {
            this.IntellisenseProvider = provider;
        }

        public DocumentLine DocumentGetLineByOffset(int pos)
        {
            return Document.GetLineByOffset(pos);
        }

        public string DocumentGetText(int offset, int length)
        {
            return Document.GetText(offset, length);
        }

        public string DocumentGetText(TextSegment segment)
        {
            return Document.GetText(segment);
        }
    }
}
