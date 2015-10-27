using ADOTabular;
using DAXEditor;
using DaxStudio.UI.ViewModels;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

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

    public class DaxIntellisenseProvider:IIntellisenseProvider
    {
        private DAXEditor.DAXEditor _editor;
        private DaxLineState _daxState;
        private bool SpacePressed;
        private bool HasThrownException;

        public DaxIntellisenseProvider (DocumentViewModel activeDocument, DAXEditor.DAXEditor editor)
        {
            Document = activeDocument;
            _editor = editor;
            
        }

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
                    completionWindow.CloseAutomatically = false;
                    
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
                                PopulateCompletionData(data, IntellisenseMetadataTypes.Columns, tableName);
                            }
                            break;
                        case "'":
                            PopulateCompletionData(data, IntellisenseMetadataTypes.Tables);
                            break;
                        default:
                            switch (_daxState.LineState)
                            {
                                case LineState.Column:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Columns, _daxState.TableName);
                                    break;
                                case LineState.Table:
                                    PopulateCompletionData(data, IntellisenseMetadataTypes.Tables);
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
                        var txt = _editor.Document.GetText(new TextSegment() { StartOffset = _daxState.StartOffset, EndOffset = _daxState.EndOffset });
                        //var txt = line.Substring(_daxState.StartOffset,_daxState.EndOffset - _daxState.StartOffset);

                        completionWindow.CompletionList.SelectItem(txt);
                        // only show the completion window if we have valid items to display
                        if (completionWindow.CompletionList.ListBox.HasItems)
                        {
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
                            _editor.DisposeCompletionWindow();
                            completionWindow = null;
                        }
                    }
                    else
                    {
                        completionWindow = null;
                    }
                }
            }
            catch(Exception ex)
            {
                HasThrownException = true;
                Log.Error("{class} {method} {exception} {stacktrace}", "DaxIntellisenseProvider", "ProcessTextEntered", ex.Message, ex.StackTrace);
                Document.OutputError(string.Format("Intellisense Disabled for this window - {0}", ex.Message));
            }
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
        }

        public void ProcessTextEntering(object sender, System.Windows.Input.TextCompositionEventArgs e, ref ICSharpCode.AvalonEdit.CodeCompletion.CompletionWindow completionWindow)
        {
            
        }
        #endregion

        public DocumentViewModel Document { get; private set; }

        private DaxLineState ParseLine()
        {
            string line = GetCurrentLine();
            int pos = _editor.CaretOffset - 1;
            var loc = _editor.Document.GetLocation(pos);
            var docLine = _editor.Document.GetLineByOffset(pos);
            return DaxLineParser.ParseLine(line, loc.Column, docLine.Offset);
        }
        
        private void PopulateCompletionData(IList<ICompletionData> data, IntellisenseMetadataTypes metadataType)
        {
            PopulateCompletionData(data, metadataType,"");
        }
        private void PopulateCompletionData(IList<ICompletionData> data, IntellisenseMetadataTypes metadataType, string tableName)
        {
            var tmpData = new List<ICompletionData>();
            
            if (metadataType.HasFlag(IntellisenseMetadataTypes.Tables)
                || metadataType.HasFlag(IntellisenseMetadataTypes.Columns)
                || metadataType.HasFlag(IntellisenseMetadataTypes.Measures))
            {
                foreach (var tab in Document.Connection.Database.Models[Document.MetadataPane.SelectedModelName].Tables)
                {
                    // add tables
                    if (metadataType.HasFlag(IntellisenseMetadataTypes.Tables)) 
                            tmpData.Add( new DaxCompletionData(tab));

                    // add columns or measures
                    if ((metadataType.HasFlag(IntellisenseMetadataTypes.Columns) 
                        && ( string.IsNullOrWhiteSpace(tableName)) || (tab.Name.Equals(tableName, StringComparison.CurrentCultureIgnoreCase)))
                        || metadataType.HasFlag(IntellisenseMetadataTypes.Measures))
                    {
                        foreach (var col in tab.Columns)
                        {
                            if ((col.ColumnType == ADOTabularColumnType.Column && metadataType.HasFlag(IntellisenseMetadataTypes.Columns))
                                || col.ColumnType == ADOTabularColumnType.Measure && metadataType.HasFlag(IntellisenseMetadataTypes.Measures))
                            {

                                tmpData.Add(new DaxCompletionData(col));
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
                        tmpData.Add( new DaxCompletionData(func));
                    }
                }
            }

            if (metadataType.HasFlag(IntellisenseMetadataTypes.DMV))
            {
                foreach (var dmv in Document.Connection.DynamicManagementViews)
                {
                    tmpData.Add(new DaxCompletionData(dmv));
                }
            }

            // add keywords
            if (metadataType.HasFlag(IntellisenseMetadataTypes.Keywords))
            {
                tmpData.Add(new DaxCompletionData("EVALUATE", 200.0));
                tmpData.Add(new DaxCompletionData("MEASURE", 200.0));
                tmpData.Add(new DaxCompletionData("DEFINE", 200.0));
                tmpData.Add(new DaxCompletionData("ORDER BY", 200.0));
                tmpData.Add(new DaxCompletionData("ASC", 200.0));
                tmpData.Add(new DaxCompletionData("DESC", 200.0));
                tmpData.Add(new DaxCompletionData("SELECT", 200.0));
                tmpData.Add(new DaxCompletionData("FROM", 200.0));
                tmpData.Add(new DaxCompletionData("WHERE", 200.0));
                tmpData.Add(new DaxCompletionData("$SYSTEM", 200.0));
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
            int pos = _editor.CaretOffset - 1;
            var loc = _editor.Document.GetLocation(pos);
            var docLine = _editor.Document.GetLineByOffset(pos);
            string line = _editor.Document.GetText(docLine.Offset, loc.Column);
            return line;
        }


        // TODO - do we need a way of triggering intellisense manually (ctrl-space) ??
        public void ProcessKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (HasThrownException) return;
            if (e.Key == System.Windows.Input.Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control ))
            {
                //TODO show intellisense on ctrl-space
                System.Diagnostics.Debug.WriteLine("show intellisense");
                e.Handled = true;  //swallow keystroke
            }
        }
    }
}
