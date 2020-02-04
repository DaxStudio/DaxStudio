using ADOTabular;
using Caliburn.Micro;
using DAXEditorControl;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils.Intellisense;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;

namespace DaxStudio.UI.Utils
{
    [Flags] public enum IntellisenseMetadataTypes
    {
        Columns   = 1,
        Functions = 2,
        Keywords  = 4,
        Measures  = 8,
        Tables    = 16,
        DMV       = 32,
        ALL       = Tables | Functions | Keywords  // columns and measures are only shown after a '[' char
    }

    public class DaxIntellisenseProvider:
        IIntellisenseProvider, 
        IInsightProvider, 
        IHandle<MetadataLoadedEvent>, 
        IHandle<DmvsLoadedEvent>,
        IHandle<FunctionsLoadedEvent>,
        IHandle<SelectedModelChangedEvent>,
        IHandle<ConnectionPendingEvent>
    {
        private IEditor _editor;
        private DaxLineState _daxState;
        private bool SpacePressed;
        private bool HasThrownException;
        private IEventAggregator _eventAggregator;
        private IGlobalOptions _options;

        public DaxIntellisenseProvider (IDaxDocument activeDocument, IEditor editor, IEventAggregator eventAggregator, IGlobalOptions options)
        {
            Document = activeDocument;
            _editor = editor;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            _options = options;
        }

        #region Properties
        public ADOTabularModel Model { get; private set; }

        #endregion

        #region Public IIntellisenseProvider Interface
        public void ProcessTextEntered(object sender, System.Windows.Input.TextCompositionEventArgs e, ref ICSharpCode.AvalonEdit.CodeCompletion.CompletionWindow completionWindow)
        {
            if (HasThrownException) return; // exit here if intellisense has previous thrown and exception

            try
            {
                if (completionWindow != null)
                {
                    // close the completion window if it has no items
                    if (!completionWindow.CompletionList.ListBox.HasItems)
                    {
                        completionWindow.Close();
                        return;
                    }
                    // close the completion window if the current text is a 100% match for the current item
                    var txt = ((TextArea)sender).Document.GetText(new TextSegment() { StartOffset = completionWindow.StartOffset, EndOffset = completionWindow.EndOffset });
                    var selectedItem = completionWindow.CompletionList.SelectedItem;
                    if (string.Compare(selectedItem.Text, txt, true) == 0 || string.Compare(selectedItem.Content.ToString(), txt, true) == 0) completionWindow.Close();

                    return;
                }

                if (char.IsLetterOrDigit(e.Text[0]) || "\'[".Contains(e.Text[0]))
                {

                    // exit if the completion window is already showing
                    if (completionWindow != null) return;

                    // exit if we are inside a string or comment
                    _daxState = ParseLine();
                    var lineState = _daxState.LineState;
                    if (lineState == LineState.String || _editor.IsInComment()) return;

                    // don't show intellisense if we are in the measure name of a DEFINE block
                    if (DaxLineParser.IsLineMeasureDefinition(GetCurrentLine())) return;

                    // TODO add insights window for Function parameters
                    //InsightWindow insightWindow = new InsightWindow(sender as ICSharpCode.AvalonEdit.Editing.TextArea);
                    
                    completionWindow = new CompletionWindow(sender as ICSharpCode.AvalonEdit.Editing.TextArea);
                    completionWindow.ResizeMode = ResizeMode.NoResize;
                    completionWindow.Width = completionWindow.Width * (_options.CodeCompletionWindowWidthIncrease / 100);
                    completionWindow.PreviewKeyUp += CompletionWindow_PreviewKeyUp;
                    completionWindow.CloseAutomatically = false;
                    
                    completionWindow.WindowStyle = WindowStyle.None;
                    completionWindow.CompletionList.BorderThickness = new System.Windows.Thickness(1);

                    if (char.IsLetterOrDigit(e.Text[0]))
                    {
                        // if the window was opened by a letter or digit include it in the match segment
                        //completionWindow.StartOffset -= 1;
                        completionWindow.StartOffset = _daxState.StartOffset;
                        System.Diagnostics.Debug.WriteLine("Setting Completion Offset: {0}", _daxState.StartOffset);
                    }

                    IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;

                    switch (e.Text)
                    {
                        case "[":

                            string tableName = GetPreceedingTableName();
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
                            PopulateCompletionData(data, IntellisenseMetadataTypes.Tables);
                            break;

                        default:
                            switch (_daxState.LineState)
                            {
                                case LineState.Column:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Columns, _daxState);
                                    break;
                                case LineState.Table:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Tables, _daxState );
                                    break;
                                case LineState.Measure:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Measures);
                                    break;
                                case LineState.Dmv:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.DMV);
                                    break;
                                default:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.ALL);
                                    break;
                            }
                            break;
                    }
                    if (data.Count > 0)
                    {
                        //var line = GetCurrentLine();
                        //System.Diagnostics.Debug.Assert(line.Length >= _daxState.EndOffset);
                        var txt = _editor.DocumentGetText(new TextSegment() { StartOffset = _daxState.StartOffset, EndOffset = _daxState.EndOffset });
                        //var txt = line.Substring(_daxState.StartOffset,_daxState.EndOffset - _daxState.StartOffset);

                        completionWindow.CompletionList.SelectItem(txt);
                        // only show the completion window if we have valid items to display
                        if (completionWindow.CompletionList.ListBox.HasItems)
                        {
                            
                            Log.Verbose("InsightWindow == null : {IsNull}", _editor.InsightWindow == null);
                            if (_editor.InsightWindow != null && _editor.InsightWindow.IsVisible)
                            {
                                Log.Verbose("hiding insight window");
                                _editor.InsightWindow.Visibility = Visibility.Collapsed;
                                //_editor.InsightWindow = null;
                                
                            }

                            Log.Verbose("CW null: {CompletionWindowNull} CW.Vis: {CompletionWindowVisible} IW null: {insightWindowNull} IW.Vis: {InsightWindowVisible}", completionWindow == null, completionWindow.Visibility.ToString() , _editor.InsightWindow == null, completionWindow.Visibility.ToString());
                            
                            completionWindow.Show();
                            completionWindow.Closing += completionWindow_Closing;
                            completionWindow.PreviewKeyUp += completionWindow_PreviewKeyUp;
                            completionWindow.Closed += delegate
                            {
                                _editor.DisposeCompletionWindow();
                            };
                        }
                        else
                        {
                            Log.Debug("{class} {method} {message}", "DaxIntellisenseProvider" , "ProcessTextEntered", "Closing CompletionWindow as it has no matching items");
                            
                            completionWindow.Close();
                            _editor.DisposeCompletionWindow();
                            completionWindow = null;
                        }
                    }
                    else
                    {
                        _editor.DisposeCompletionWindow();
                        completionWindow = null;
                    }
                }

                if (e.Text[0] == '(')
                {
                    completionWindow?.Close();
                    var funcName = DaxLineParser.GetPreceedingWord(GetCurrentLine().TrimEnd('(').Trim()).ToLower();
                    Log.Verbose("Func: {Function}", funcName);
                    ShowInsight(funcName);
                }
            }
            catch(Exception ex)
            {
                HasThrownException = true;
                Log.Error("{class} {method} {exception} {stacktrace}", "DaxIntellisenseProvider", "ProcessTextEntered", ex.Message, ex.StackTrace);
                Document.OutputError(string.Format("Intellisense Disabled for this window - {0}", ex.Message));
            }
        }


        private void CompletionWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            // close the completion window on the following keys
            switch (e.Key)
            {
                case Key.Left:
                case Key.Right:
                case Key.OemCloseBrackets:
                    _editor.DisposeCompletionWindow();
                    break;
            }
            
        }

        public void ShowInsight(string funcName)
        {
            //if (!funcName.EndsWith("(")) return;
            funcName = funcName.TrimEnd('(');

            ADOTabularFunction f = Document?.Connection?.FunctionGroups?.GetByName(funcName);
            
            if (f != null)
            {
                try
                {
                    Log.Verbose("Showing InsightWindow for {function}", f.Caption);
                    //var insight = _editor.InsightWindow;
                    //if (_editor.InsightWindow == null )
                    //{
                    _editor.InsightWindow = null;
                    _editor.InsightWindow = new InsightWindow(_editor.TextArea);
                    //insight.ExpectInsertionBeforeStart = true;
                    //}
                    //_editor.InsightWindow.MaxWidth = 200;
                    
                    _editor.InsightWindow.Content = BuildInsightContent(f,400);
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
        }

        private UIElement BuildInsightContent(ADOTabularFunction f, int maxWidth)
        {
            var grd = new Grid();
            grd.ColumnDefinitions.Add(new ColumnDefinition() { MaxWidth = maxWidth });
            var tb = new TextBlock();
            tb.TextWrapping = TextWrapping.Wrap;
            var caption = new Run(f.DaxName);
            //caption.Foreground = Brushes.Blue;
            tb.Inlines.Add(new Bold(caption));
            tb.Inlines.Add("\n");
            tb.Inlines.Add(f.Description);
            //tb.Inlines.Add("\n");
            //tb.Inlines.Add(new Italic(new Run(f.DaxName)));
            var docLink = new Hyperlink();
            docLink.Inlines.Add($"https://dax.guide/{f.Caption}");
            docLink.NavigateUri = new Uri($"https://dax.guide/{f.Caption}");
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

        void completionWindow_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var completionWindow = (CompletionWindow)sender;
            var segmentLength = completionWindow.EndOffset - completionWindow.StartOffset;
            SpacePressed = e.Key == Key.Space;
            // close window if F5 or F6 are pressed
            if (e.Key == Key.F5
                || e.Key == Key.F6) 
            { 
                completionWindow.Close(); 
                return;
            }
            // insert the current item when the right arrow is pressed
            //if (e.Key == Key.Right)
            //{
            //    completionWindow.CompletionList.RequestInsertion(null);
            //}
        }

        void completionWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // cancel closing if part way into a table or column name
            var lineState = ParseLine();
            if (SpacePressed && (lineState.LineState == LineState.Column || lineState.LineState == LineState.Table)) e.Cancel = true;
            var line = GetCurrentLine();
            if (line.EndsWith("(")) {
                var funcName = DaxLineParser.GetPreceedingWord(line.TrimEnd('('));
                ShowInsight(funcName);
            }
        }

        public void ProcessTextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e, ref CompletionWindow completionWindow)
        {
            if (e.Text.Length > 0 && completionWindow != null)
            {
                if (e.Text[0] == '(')
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    completionWindow.CompletionList.RequestInsertion(e);
                    
                }
            }
            // Do not set e.Handled=true.
            // We still want to insert the character that was typed.
        }
        #endregion

        public IDaxDocument Document { get; private set; }
        public ADOTabularDynamicManagementViewCollection Dmvs { get; private set; }
        public ADOTabularFunctionGroupCollection FunctionGroups { get; private set; }

        private DaxLineState ParseLine()
        {
            //Log.Debug("{class} {method} {message}", "DaxIntellisenseProvider", "ParseLine", "start");
            string line = GetCurrentLine();
            int pos = _editor.CaretOffset>0 ? _editor.CaretOffset - 1 : 0;
            var loc = _editor.DocumentGetLocation(pos);
            var docLine = _editor.DocumentGetLineByOffset(pos);
            Log.Debug("{class} {method} {line} col:{column} off:{offset}", "DaxIntellisenseProvider", "ParseLine", line,loc.Column, docLine.Offset);
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
            Log.Debug("{class} {method} Type: {metadataType}  Table: {table} Column: {column}", "DaxIntellisenseProvider", "PopulateCompletionData", metadataType.ToString(), state == null ? "-" : state.TableName, state == null ? "-": state.ColumnName);
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
                tmpData.Add(new DaxCompletionData(this, "EVALUATE", 200.0));
                tmpData.Add(new DaxCompletionData(this, "MEASURE", 200.0));
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
                tmpData.Add(new DaxCompletionData(this, "$SYSTEM", 200.0));
            }
            foreach(var itm in tmpData.OrderBy(x => x.Content.ToString()))
            {
                data.Add(itm);
            }
            
        }
        private string GetPreceedingTableName()
        {
            string tableName = "";
            try
            {
                string line = GetCurrentLine();
                tableName = DaxLineParser.GetPreceedingTableName(line);
            }
            catch (Exception ex)
            {
                Log.Error("{class} {method} {error}", "DaxIntellisenseProvider", "GetPreceedingTableName", ex.Message);
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
            
            if (HasThrownException) return;
            if (e.Key == Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control ))
            {
                //TODO show intellisense on ctrl-space
                System.Diagnostics.Debug.WriteLine("show intellisense");
                e.Handled = true;  //swallow keystroke
            }
        }

        public void Handle(MetadataLoadedEvent message)
        {
            if (message.Document == Document)
            {
                Model = message.Model;
            }
        }

        public void Handle(SelectedModelChangedEvent message)
        {
            if (Document == message.Document)
            {
                Model = null;
            }
        }

        public void Handle(DmvsLoadedEvent message)
        {
            if (Document == message.Document)
            {
                Dmvs = message.DmvCollection;
            }
        }

        public void Handle(FunctionsLoadedEvent message)
        {
            if (Document == message.Document)
            {
                FunctionGroups = message.FunctionGroups;
            }
        }

        public void Handle(ConnectionPendingEvent message)
        {
            if (message.Document == Document)
            {
                FunctionGroups = null;
                Dmvs = null;
            }
        }

        public bool MetadataIsCached { get { return Model != null && FunctionGroups != null && Dmvs != null; } }

        public void CloseCompletionWindow()
        {
            if (_editor.InsightWindow != null)
            {
                _editor.InsightWindow?.Close();
                _editor.DisposeCompletionWindow();
            }
        }
    }
}
