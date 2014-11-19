using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class DAXEditor : ICSharpCode.AvalonEdit.TextEditor
    {
        private BracketRenderer.BracketHighlightRenderer _bracketRenderer;

        protected override void OnInitialized(EventArgs e)
        {
            
            base.OnInitialized(e);
            base.Loaded += OnLoaded;
            base.Unloaded += OnUnloaded;
            TextArea.TextEntering += textEditor_TextArea_TextEntering;
            TextArea.TextEntered += textEditor_TextArea_TextEntered;
            //SetValue(TextBoxControllerProperty, new TextBoxController());
            TextArea.Caret.PositionChanged += Caret_PositionChanged;

            //TextArea.Caret.PositionChanged += HighlightBrackets;
            //TextArea.DragOver += textEditor
            System.Reflection.Assembly myAssembly = System.Reflection.Assembly.GetAssembly(GetType());
            using (var s = myAssembly.GetManifestResourceStream("DAXEditor.Resources.DAX.xshd"))
            {
                using (var reader = new XmlTextReader(s))
                {
                    SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                }
            }
            //ToolTip tt = new ToolTip();
            //tt.Content = "hello";

            _bracketRenderer = new BracketRenderer.BracketHighlightRenderer(this.TextArea.TextView );
            //this.textEditor1.ToolTip = tt;

            //TODO - hardcoded for v1 - should be moved to a settings dialog
            this.FontFamily = new System.Windows.Media.FontFamily("Lucida Console");
            this.DefaultFontSize = 11.0;
            this.FontSize = DefaultFontSize;
            this.ShowLineNumbers = true;            
        }

               void Caret_PositionChanged(object sender, EventArgs e)
        {
            try{
                HighlightBrackets();
            }
            catch {}
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


        CompletionWindow completionWindow;

        void textEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            /*  TODO - temporarily commented out - we need a DAX parser before we can 
             *         properly implement intellisense like functionality
             *         
            if (e.Text == "(" || e.Text == " ")
            {
                
                // Open code completion after the user has pressed dot:
                completionWindow = new CompletionWindow(this.TextArea);

                // TODO - load completion data from metadata
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                data.Add(new MyCompletionData("Calculate", CodeCompletionType.Function));
                data.Add(new MyCompletionData("CalculateTable", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Earlier", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Earliest", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Filter", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Max", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Min", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Related", CodeCompletionType.Function));
                data.Add(new MyCompletionData("RelatedTable", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Values", CodeCompletionType.Function));
                data.Add(new MyCompletionData("Summarize", CodeCompletionType.Function));

                completionWindow.Show();
                completionWindow.Closed += delegate
                {
                    completionWindow = null;
                };
            }
             * */
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
        private Regex rxComment = new Regex("^(\\s*)", RegexOptions.Compiled | RegexOptions.Multiline);

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
            if (e.Text.Length > 0 && completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    completionWindow.CompletionList.RequestInsertion(e);
                }
            }
            // Do not set e.Handled=true.
            // We still want to insert the character that was typed.
        }

        /*
        public void HighlightBrackets(object sender, EventArgs e)
        {
            
			// * Special case: ITextEditor.Language guarantees that it never returns null.
			// * In this case however it can be null, since this code may be called while the document is loaded.
			// * ITextEditor.Language gets set in CodeEditorAdapter.FileNameChanged, which is called after
			// * loading of the document has finished.
			// * 
            if (this.Adapter.Language != null)
            {
                if (CodeEditorOptions.Instance.HighlightBrackets || CodeEditorOptions.Instance.ShowHiddenDefinitions)
                {
                    var bracketSearchResult = this.Adapter.Language.BracketSearcher.SearchBracket(this.Adapter.Document, this.TextArea.Caret.Offset);
                    if (CodeEditorOptions.Instance.HighlightBrackets)
                    {
                        this.bracketRenderer.SetHighlight(bracketSearchResult);
                    }
                    else
                    {
                        this.bracketRenderer.SetHighlight(null);
                    }
                    if (CodeEditorOptions.Instance.ShowHiddenDefinitions)
                    {
                        this.hiddenDefinitionRenderer.BracketSearchResult = bracketSearchResult;
                        this.hiddenDefinitionRenderer.Show();
                    }
                    else
                    {
                        this.hiddenDefinitionRenderer.ClosePopup();
                    }
                }
                else
                {
                    this.bracketRenderer.SetHighlight(null);
                    this.hiddenDefinitionRenderer.ClosePopup();
                }
            }
        }
*/
    }
}
