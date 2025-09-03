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
            _imageResource = GetImageResource(calendar.MetadataImage);
            _priority = 100.0;
            _insightProvider = insightProvider;
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
                // walk back to start of word
                var newSegment = GetPreceedingWordSegment(document, completionSegment);
                var replaceOffset = newSegment.Offset;
                var replaceLength = newSegment.Length;
                var funcParamStart = Text.IndexOf("«", StringComparison.OrdinalIgnoreCase);
                string insertionText = funcParamStart > 0 ? Text.Substring(0, funcParamStart) : Text;

                if (insertionRequestEventArgs is TextCompositionEventArgs args)
                {
                    // if the insertion char is the same as the last char in the 
                    // insertion text then trim it off
                    var insertionChar = args.Text;
                    if (insertionText.EndsWith(insertionChar, StringComparison.Ordinal)) insertionText = insertionText.TrimEnd(insertionChar[0]);
                }
                if (completionSegment.EndOffset <= document.TextLength - 1)
                {
                    var lastCompletionChar = insertionText[insertionText.Length - 1];
                    var lastDocumentChar = document.GetCharAt(completionSegment.EndOffset);
                    Log.Debug("{class} {method} {lastCompletionChar} vs {lastDocumentChar} off: {offset} len:{length}", "DaxCompletionData", "Complete", lastCompletionChar, lastDocumentChar, newSegment.Offset, newSegment.Length);
                    if (lastCompletionChar == lastDocumentChar) replaceLength++;
                }
                document.Replace(newSegment.Offset, newSegment.Length, insertionText);
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
            return DaxLineParser.GetPreceedingWordSegment(docLine.Offset, loc.Column, line, daxState);

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
