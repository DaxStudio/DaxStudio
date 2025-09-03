using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using ADOTabular;
using Caliburn.Micro;
using DAXEditorControl;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Serilog;

namespace DaxStudio.UI.Utils.Intellisense
{
    [Flags] public enum IntellisenseMetadataTypes
    {
        Columns   = 1,
        Functions = 2,
        Keywords  = 4,
        Measures  = 8,
        Tables    = 16,
        DMV       = 32,
        Calendars = 64,
        All       = Tables | Functions | Keywords | Calendars // columns and measures are only shown after a '[' char
    }

    public class DaxIntellisenseProvider:
        IIntellisenseProvider, 
        IInsightProvider, 
        IHandle<MetadataLoadedEvent>, 
        IHandle<DmvsLoadedEvent>,
        IHandle<FunctionsLoadedEvent>,
        IHandle<ConnectionPendingEvent>
    {
        private IEditor _editor;
        private DaxLineState _daxState;
        private bool _spacePressed;
        //private bool HasThrownException;
        private IEventAggregator _eventAggregator;
        private readonly IGlobalOptions _options;
        private const double BaseCompletionWindowWidth = 300;
        public DaxIntellisenseProvider (IDaxDocument activeDocument, IEventAggregator eventAggregator, IGlobalOptions options)
        {
            Document = activeDocument;
            _eventAggregator = eventAggregator;
            _options = options;
        }

        public DaxIntellisenseProvider(IDaxDocument activeDocument, IEventAggregator eventAggregator, IGlobalOptions options, ADOTabularModel model, ADOTabularDynamicManagementViewCollection dmvs, ADOTabularFunctionGroupCollection functions): this(activeDocument,eventAggregator, options)
        {
            Model = model;
            FunctionGroups = functions;
            DMVs = dmvs;
        }

        #region Properties
        public ADOTabularModel Model { get; private set; }
        public IEditor Editor { get => _editor; set => _editor = value;
        }
        #endregion

        #region Public IIntellisenseProvider Interface
        public void ProcessTextEntered(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow)
        {
            System.Diagnostics.Debug.WriteLine($"ProcessTextEntered: {e.Text}");

            //if (HasThrownException) return; // exit here if intellisense has previous thrown and exception

            try
            {

                _daxState = ParseLine();

                if (completionWindow != null
                    && !string.IsNullOrWhiteSpace(e.Text)
                    && completionWindow.StartOffset == completionWindow.EndOffset)
                {
                    Debug.WriteLine("Hack: Force dispose of completion window as StartOffset == EndOffset");
                    CloseCompletionWindow();
                    //completionWindow = null;
                }
                
                if (completionWindow != null)
                {
                    Debug.WriteLine($"completionWindow != null visible:{completionWindow.IsVisible} height: {completionWindow.ActualHeight}");
                    // close the completion window if it has no items
                    if (!completionWindow.CompletionList.ListBox.HasItems || !completionWindow.IsVisible)
                    {
                        Debug.WriteLine("Completion Window has no items - Closing");
                        CloseCompletionWindow();
                        //completionWindow = null;
                        return;
                    }
                    else
                    {

                        var document = ((TextArea)sender).Document;
                        var startOffset = completionWindow.StartOffset;
                        var endOffset = completionWindow.EndOffset;
                        if (startOffset > document.TextLength) startOffset = document.TextLength;
                        if (endOffset > document.TextLength) endOffset = document.TextLength;

                        // close the completion window if the current text is a 100% match for the current item
                        var txt = document.GetText(new TextSegment() { StartOffset = startOffset, EndOffset = endOffset });
                        Debug.WriteLine($"processing: {txt} start: {startOffset}({completionWindow.StartOffset}) end: {endOffset }({completionWindow.EndOffset})");
                        var selectedItem = completionWindow.CompletionList.SelectedItem;
                        if (string.Compare(selectedItem.Text, txt, true) == 0 || string.Compare(selectedItem.Content.ToString(), txt, true) == 0)
                        {
                            CloseCompletionWindow();
                            //completionWindow = null;
                        }

                        return;
                    }
                }

                if (char.IsLetterOrDigit(e.Text[0]) || "\'[".Contains(e.Text[0]))
                {

                    // exit if the completion window is already showing
                    if (completionWindow != null) return;

                    // exit if we are inside a string or comment
                    //_daxState = ParseLine();
                    var lineState = _daxState.LineState;
                    if (lineState == LineState.String || _editor.IsInComment()) return;

                    // don't show intellisense if we are in the measure name of a DEFINE block
                    if (DaxLineParser.IsLineMeasureDefinition(GetCurrentLine())) return;

                    Debug.WriteLine("Constructing new CompletionWindow");
                    completionWindow = CreateCompletionWindow(sender);

                    if (char.IsLetterOrDigit(e.Text[0]))
                    {
                        // if the window was opened by a letter or digit include it in the match segment
                        //completionWindow.StartOffset -= 1;
                        completionWindow.StartOffset = _daxState.StartOffset;
                        Debug.WriteLine("Setting Completion Offset: {0}", _daxState.StartOffset);
                    }

                    IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;

                    switch (e.Text)
                    {
                        case "[":

                            string tableName = GetPrecedingTableName();
                            if (string.IsNullOrWhiteSpace(tableName))
                            {
                                PopulateCompletionData(data, IntellisenseMetadataTypes.Measures);
                            }
                            else
                            {
                                PopulateCompletionData(data, IntellisenseMetadataTypes.Columns, _daxState);
                            }
                            break;
                        case "'":
                            PopulateCompletionData(data, IntellisenseMetadataTypes.Tables | IntellisenseMetadataTypes.Calendars);
                            break;

                        default:
                            switch (_daxState.LineState)
                            {
                                case LineState.Column:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Columns, _daxState);
                                    break;
                                case LineState.Table:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Tables | IntellisenseMetadataTypes.Calendars, _daxState);
                                    break;
                                case LineState.Measure:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Measures);
                                    break;
                                case LineState.Dmv:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.DMV);
                                    break;
                                case LineState.Digit: // only table names can start with digits
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Tables | IntellisenseMetadataTypes.Calendars, _daxState);
                                    break;
                                default:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.All);
                                    break;
                            }
                            break;
                    }
                    if (data.Count > 0)
                    {

                        var txt = _editor.DocumentGetText(new TextSegment() { StartOffset = _daxState.StartOffset, EndOffset = _daxState.EndOffset });

                        completionWindow.CompletionList.SelectItem(txt);
                        // only show the completion window if we have valid items to display
                        if (completionWindow.CompletionList.ListBox.HasItems)
                        {

                            Log.Verbose("InsightWindow == null : {IsNull}", _editor.InsightWindow == null);
                            if (_editor.InsightWindow != null && _editor.InsightWindow.IsVisible)
                            {
                                Log.Verbose("hiding insight window");
                                _editor.InsightWindow.Visibility = Visibility.Collapsed;
                            }

                            Log.Verbose("CW null: {CompletionWindowNull} CW.Vis: {CompletionWindowVisible} IW null: {insightWindowNull} IW.Vis: {InsightWindowVisible}", completionWindow == null, completionWindow.Visibility.ToString(), _editor.InsightWindow == null, completionWindow.Visibility.ToString());


                            completionWindow.Show();
                        }
                        else
                        {
                            Log.Verbose("{class} {method} {message}", "DaxIntellisenseProvider", "ProcessTextEntered", "Closing CompletionWindow as it has no matching items");
                            CloseCompletionWindow();
                            //completionWindow = null;
                        }
                    }
                    else
                    {
                        CloseCompletionWindow();
                        //completionWindow = null;
                    }
                }

                if (e.Text[0] == '(')
                {
                    //completionWindow?.Close();
                    CloseCompletionWindow();
                    var funcName = DaxLineParser.GetPreceedingWord(GetCurrentLine().TrimEnd('(').Trim()).ToLower();
                    Log.Verbose("Func: {Function}", funcName);
                    ShowInsight(funcName);
                }
                else
                {
                    if (_editor?.InsightWindow?.IsVisible??false) _editor.InsightWindow.Close();
                }
                
            }
            catch(Exception ex)
            {
                //HasThrownException = true;
                Log.Error("{class} {method} {exception} {stacktrace}", "DaxIntellisenseProvider", "ProcessTextEntered", ex.Message, ex.StackTrace);
                Document.OutputError($"Intellisense Disabled for this window - {ex.Message}");
            }
        }

        private CompletionWindow CreateCompletionWindow(object sender)
        {
            DaxStudioCompletionWindow completionWindow = new DaxStudioCompletionWindow(sender as TextArea);
            // set properties
            completionWindow.ResizeMode = ResizeMode.NoResize;
            completionWindow.Width = BaseCompletionWindowWidth * (_options.CodeCompletionWindowWidthIncrease / 100);
            completionWindow.CloseAutomatically = false;
            completionWindow.WindowStyle = WindowStyle.None;
            completionWindow.Background = new SolidColorBrush(Color.FromArgb(0,0,0,0));
            completionWindow.AllowsTransparency = true;
            //completionWindow.CompletionList.BorderThickness = new Thickness(1);
            DaxIntellisenseProvider.AssignResouceDictionary(completionWindow);
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

            //completionWindow.IsVisibleChanged += delegate
            //{
            //    System.Diagnostics.Debug.WriteLine("firing CompletionWindow IsVisibleChanged delegate");
            //    CloseCompletionWindow();
            //};
            completionWindow.Closed += completionWindow_Closed;
        }

        private void completionWindow_Closed(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("firing CompletionWindow Closed delegate");
            CloseCompletionWindow();
        }

        private void DetachCompletionWindowEvents(CompletionWindow completionWindow)
        {
            System.Diagnostics.Debug.WriteLine("Detaching Completion Window Events");
            completionWindow.PreviewKeyUp -= CompletionWindow_PreviewKeyUp;
            completionWindow.Closing -= completionWindow_Closing;
            completionWindow.PreviewKeyUp -= completionWindow_PreviewKeyUp;
            completionWindow.MouseEnter -= completionWindow_MouseEnter;
            completionWindow.MouseLeave -= completionWindow_MouseLeave;

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
            // close the completion window on the following keys
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

        public void ShowInsight(string funcName)
        {
            ShowInsight(funcName, -1);
        }
        
        public void ShowInsight(string funcName, int offset)
        {

            funcName = funcName.TrimEnd('(');

            ADOTabularFunction f = Document?.Connection?.FunctionGroups?.GetByName(funcName);
            
            if (f != null)
            {
                ShowFunctionInsightWindow(offset, f);
                return;
            }

            if (Document?.Connection?.Keywords.Contains(funcName, StringComparer.OrdinalIgnoreCase)??false)
            {
                ShowKeywordInsightWindow(offset, funcName);
            }
        }

        private void ShowFunctionInsightWindow(int offset, ADOTabularFunction f)
        {
            try
            {
                Log.Verbose("Showing InsightWindow for function {function}", f.Caption);
                //_editor.InsightWindow?.Close();
                _editor.InsightWindow = null;
                _editor.InsightWindow = new InsightWindow(_editor.TextArea);
                _editor.InsightWindow.Resources.MergedDictionaries.Add(InsightWindowCustomResources);
                if (offset > -1)
                {
                    _editor.InsightWindow.StartOffset = offset;
                }
                _editor.InsightWindow.Content = BuildInsightFunctionContent(f, 400);
                try
                {
                    _editor.InsightWindow.Show();

                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning("{class} {method} {message}", "DaxIntellisenseProvider", "ShowInsight", "Error calling InsightWindow.Show(): " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}", "DaxIntellisenseProvider", "ShowInsight", ex.Message);
            }
        }

        private void ShowKeywordInsightWindow(int offset, string keyword)
        {
            try
            {
                Log.Verbose($"Showing InsightWindow for keyword: {keyword}");
                //_editor.InsightWindow?.Close();
                _editor.InsightWindow = null;
                _editor.InsightWindow = new InsightWindow(_editor.TextArea);
                _editor.InsightWindow.Resources.MergedDictionaries.Add(InsightWindowCustomResources);
                if (offset > -1)
                {
                    _editor.InsightWindow.StartOffset = offset;
                }
                _editor.InsightWindow.Content = BuildInsightKeywordContent(keyword, 400);
                try
                {
                    _editor.InsightWindow.Show();

                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning("{class} {method} {message}", "DaxIntellisenseProvider", "ShowInsight", "Error calling InsightWindow.Show(): " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {message}", "DaxIntellisenseProvider", "ShowInsight", ex.Message);
            }
        }

        public string GetCurrentWord(TextViewPosition pos)
        {
            try
            {
                if (_editor.IsInComment()) return string.Empty;
                var docLine = _editor.DocumentGetLineByNumber(pos.Line);
                string line = _editor.DocumentGetText(docLine.Offset, docLine.Length);
                var lineState = DaxLineParser.ParseLine(line, pos.Column , 0);
                return line.Substring( lineState.StartOffset, lineState.EndOffset - lineState.StartOffset);
            }
            catch 
            {
                return string.Empty;
            }
        }

        private UIElement BuildInsightFunctionContent(ADOTabularFunction f, int maxWidth)
        {
            var grd = new Grid();
            grd.ColumnDefinitions.Add(new ColumnDefinition() { MaxWidth = maxWidth });
            var tb = new TextBlock {TextWrapping = TextWrapping.Wrap};
            var caption = new Run(f.DaxName);
            tb.Inlines.Add(new Bold(caption));
            tb.Inlines.Add("\n");
            tb.Inlines.Add(f.Description);

            var docLink = new Hyperlink();
            docLink.Inlines.Add($"https://dax.guide/{f.Caption}");
            docLink.NavigateUri = new Uri($"https://dax.guide/{f.Caption}/?aff=dax-studio");
            docLink.RequestNavigate += InsightHyperLinkNavigate;
            tb.Inlines.Add("\n");
            tb.Inlines.Add(docLink);
            Grid.SetColumn(tb, 0);
            grd.Children.Add(tb);
            return grd;
        }

        private UIElement BuildInsightKeywordContent(string keyword, int maxWidth)
        {
            var grd = new Grid();
            grd.ColumnDefinitions.Add(new ColumnDefinition() { MaxWidth = maxWidth });
            var tb = new TextBlock { TextWrapping = TextWrapping.Wrap };
            var caption = new Run(keyword);
            tb.Inlines.Add(new Bold(caption));
            tb.Inlines.Add(new Run(" «Keyword»"));
            //tb.Inlines.Add("\n");
            //tb.Inlines.Add(f.Description);

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

        void completionWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            var completionWindow = (CompletionWindow)sender;
            
            _spacePressed = e.Key == Key.Space;
            // close window if F5 or F6 are pressed
            var keyStr = e.Key.ToString();
            System.Diagnostics.Debug.WriteLine($"completionWindow_PreviewKeyUp: {keyStr}");
            if (keyStr == _options.HotkeyRunQuery
                || keyStr == _options.HotkeyRunQueryAlt
                || keyStr == _options.HotkeyFormatQueryStandard
                || keyStr == _options.HotkeyFormatQueryAlternate
                )
            { 
                CloseCompletionWindow();
                completionWindow = null;
            }

        }

        void completionWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // cancel closing if part way into a table or column name
            var lineState = ParseLine();
            if (_spacePressed && (lineState.LineState == LineState.Column || lineState.LineState == LineState.Table))
            {
                e.Cancel = true;
                return;
            }
            
            //var line = GetCurrentLine();
            //if (line.EndsWith("(")) {
            //    var funcName = DaxLineParser.GetPreceedingWord(line.TrimEnd('('));
            //    ShowInsight(funcName);
            //}
        }

        public void ProcessTextEntering(object sender, TextCompositionEventArgs e, ref CompletionWindow completionWindow)
        {
            if (e.Text.Length <= 0 || completionWindow == null) return;
            if (e.Text[0] == '(')
            {
                // Whenever a non-letter is typed while the completion window is open,
                // insert the currently selected element.
                completionWindow.CompletionList.RequestInsertion(e);
                    
            }
            // Do not set e.Handled=true.
            // We still want to insert the character that was typed.
        }
        #endregion

        public IDaxDocument Document { get; }
        public ADOTabularDynamicManagementViewCollection DMVs { get; private set; }
        public ADOTabularFunctionGroupCollection FunctionGroups { get; private set; }

        private DaxLineState ParseLine()
        {
            string line = GetCurrentLine();
            int pos = _editor.CaretOffset>0 ? _editor.CaretOffset - 1 : 0;
            var loc = _editor.DocumentGetLocation(pos);
            var docLine = _editor.DocumentGetLineByOffset(pos);
#if DEBUG
            Log.Debug("{class} {method} {line} col:{column} off:{offset}", "DaxIntellisenseProvider", "ParseLine", line,loc.Column, docLine.Offset);
#endif
            return DaxLineParser.ParseLine(line, loc.Column, docLine.Offset);
        }
        
        private void PopulateCompletionData(IList<ICompletionData> data, IntellisenseMetadataTypes metadataType)
        {
            PopulateCompletionData(data, metadataType,null);
        }
        private void PopulateCompletionData(IList<ICompletionData> data, IntellisenseMetadataTypes metadataType, DaxLineState state)
        {
            // exit early if the Metadata is not cached
            if (!MetadataIsCached) return;

            var tmpData = new List<ICompletionData>();
            Log.Verbose("{class} {method} Type: {metadataType}  Table: {table} Column: {column}", "DaxIntellisenseProvider", "PopulateCompletionData", metadataType.ToString(), state == null ? "-" : state.TableName, state == null ? "-": state.ColumnName);
            if (metadataType.HasFlag(IntellisenseMetadataTypes.Tables)
                || metadataType.HasFlag(IntellisenseMetadataTypes.Columns)
                || metadataType.HasFlag(IntellisenseMetadataTypes.Measures))
            {
                //var tabs = Document.Connection.Database.Models[Document.MetadataPane.SelectedModelName].Tables;
                var tabs = Model.Tables;
                foreach (var tab in tabs)
                {
                    // add tables
                    if (metadataType.HasFlag(IntellisenseMetadataTypes.Tables)) 
                            tmpData.Add( new DaxCompletionData(this, tab, _daxState));

                    // add columns or measures
                    if ((metadataType.HasFlag(IntellisenseMetadataTypes.Columns) 
                        && ( string.IsNullOrWhiteSpace(_daxState.TableName)) || (tab.Name.Equals(_daxState.TableName, StringComparison.CurrentCultureIgnoreCase)))
                        || metadataType.HasFlag(IntellisenseMetadataTypes.Measures))
                    {
                        foreach (var col in tab.Columns)
                        {
                            if ((col.ObjectType == ADOTabularObjectType.Column && metadataType.HasFlag(IntellisenseMetadataTypes.Columns))
                                || col.ObjectType == ADOTabularObjectType.Measure && metadataType.HasFlag(IntellisenseMetadataTypes.Measures))
                            {

                                tmpData.Add(new DaxCompletionData(this, col, _daxState));
                            }
                        }
                    }
                }
            }

            // add calendars
            if (metadataType.HasFlag(IntellisenseMetadataTypes.Calendars))
            {
                foreach (var cal in Model.Calendars)
                {
                    tmpData.Add(new DaxCompletionData(this, cal, _daxState));
                }
            }

            // add functions
            if (metadataType.HasFlag(IntellisenseMetadataTypes.Functions))
            {
                foreach (var funcGrp in Document.Connection.FunctionGroups)
                {
                    foreach (var func in funcGrp.Functions)
                    {
                        tmpData.Add( new DaxCompletionData(this,func));
                    }
                }
            }

            if (metadataType.HasFlag(IntellisenseMetadataTypes.DMV))
            {
                foreach (var dmv in Document.Connection.DynamicManagementViews)
                {
                    tmpData.Add(new DaxCompletionData(this, dmv));
                }
            }

            // add keywords
            if (metadataType.HasFlag(IntellisenseMetadataTypes.Keywords))
            {
                // Keywords
                tmpData.Add(new DaxCompletionData(this, "EVALUATE", 200.0));
                tmpData.Add(new DaxCompletionData(this, "MEASURE", 200.0));
                tmpData.Add(new DaxCompletionData(this, "MPARAMETER", 200.0));
                tmpData.Add(new DaxCompletionData(this, "COLUMN", 200.0));
                tmpData.Add(new DaxCompletionData(this, "TABLE", 200.0));
                tmpData.Add(new DaxCompletionData(this, "DEFINE", 200.0));
                tmpData.Add(new DaxCompletionData(this, "ORDER BY", 200.0));
                tmpData.Add(new DaxCompletionData(this, "ASC", 200.0));
                tmpData.Add(new DaxCompletionData(this, "DESC", 200.0));
                tmpData.Add(new DaxCompletionData(this, "SELECT", 200.0));
                tmpData.Add(new DaxCompletionData(this, "FROM", 200.0));
                tmpData.Add(new DaxCompletionData(this, "WHERE", 200.0));
                tmpData.Add(new DaxCompletionData(this, "VAR", 200.0));
                tmpData.Add(new DaxCompletionData(this, "RETURN", 200.0));
                tmpData.Add(new DaxCompletionData(this, "START", 200.0));
                tmpData.Add(new DaxCompletionData(this, "AT", 200.0));
                tmpData.Add(new DaxCompletionData(this, "REL", 200.0));
                tmpData.Add(new DaxCompletionData(this, "ABS", 200.0));
                tmpData.Add(new DaxCompletionData(this, "$SYSTEM", 200.0));

                // Data Types
                tmpData.Add(new DaxCompletionData(this, "BINARY", 300.0));
                tmpData.Add(new DaxCompletionData(this, "BOOLEAN", 300.0));
                tmpData.Add(new DaxCompletionData(this, "CURRENCY", 300.0));
                tmpData.Add(new DaxCompletionData(this, "DATETIME", 300.0));
                tmpData.Add(new DaxCompletionData(this, "DECIMAL", 300.0));
                tmpData.Add(new DaxCompletionData(this, "DOUBLE", 300.0));
                tmpData.Add(new DaxCompletionData(this, "INTEGER", 300.0));
                tmpData.Add(new DaxCompletionData(this, "STRING", 300.0));
                tmpData.Add(new DaxCompletionData(this, "VARIANT", 300.0));
            }
            foreach(var itm in tmpData.OrderBy(x => x.Content.ToString()))
            {
                data.Add(itm);
            }
            
        }
        private string GetPrecedingTableName()
        {
            string tableName = "";
            try
            {
                string line = GetCurrentLine();
                tableName = DaxLineParser.GetPreceedingTableName(line);
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {error}", nameof(DaxIntellisenseProvider), nameof(GetPrecedingTableName), ex.Message);
            }
            return tableName;
        }

        private string GetCurrentLine()
        {
            //Log.Debug("{class} {method} {message}", "DaxIntellisenseProvider", "GetCurrentLine", "start");
            int pos = _editor.CaretOffset > 0 ? _editor.CaretOffset - 1: 0;
            var loc = _editor.DocumentGetLocation(pos);
            var docLine = _editor.DocumentGetLineByOffset(pos);
            if (docLine.Length == 0) return "";
            string line = _editor.DocumentGetText(docLine.Offset, loc.Column);
            //Log.Debug("{class} {method} {message}", "DaxIntellisenseProvider", "GetCurrentLine", "end");
            return line;
        }


        // TODO - do we need a way of triggering intellisense manually (ctrl-space) ??
        public void ProcessKeyDown(object sender, KeyEventArgs e)
        {
            
            //if (HasThrownException) return;
            if (e.Key == Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control ))
            {
                //TODO show intellisense on ctrl-space
                Debug.WriteLine("show intellisense");
                e.Handled = true;  //swallow keystroke
            }
        }

        public Task HandleAsync(MetadataLoadedEvent message, CancellationToken cancellationToken)
        {
            if (message.Document == Document)
            {
                Model = message.Model;
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
            return Task.CompletedTask;
        }

        public Task HandleAsync(ConnectionPendingEvent message, CancellationToken cancellationToken)
        {
            if (message.Document == Document)
            {
                FunctionGroups = null;
                DMVs = null;
            }
            return Task.CompletedTask;
        }

        public bool MetadataIsCached => Model != null && FunctionGroups != null && DMVs != null;

        private object _completionWindowCloseLock = new object();
        public void CloseCompletionWindow()
        {
            System.Diagnostics.Debug.WriteLine("Calling CloseCompletionWindow");
            lock (_completionWindowCloseLock)
            {
                _editor.InsightWindow?.Close();
                _editor.DisposeCompletionWindow();
            }
        }
    }
}
