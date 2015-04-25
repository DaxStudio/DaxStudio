using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DaxStudio.UI.Utils
{
    class DaxCompletionData:ICompletionData
    {
        private readonly string _text;
        private readonly object _content;
        private readonly string _description;
        private readonly ImageSource _image;
        private double _priority = 120.0;
        public DaxCompletionData(string text, string content, string description, ImageSource image )
        {
            _text = text;
            _content = content;
            _description = description;
            _image = image;
        }

        public DaxCompletionData(string text, double priority)
        {
            _text = text;
            _content = text;
            _description = text;
            _image = null;
            _priority = priority;
        }

        public DaxCompletionData(ADOTabular.ADOTabularColumn column)
        {
            _text = column.DaxName;
            _content = column.Caption;
            _description = string.IsNullOrEmpty(column.Description)?null:column.Description;
            _image = GetMetadataImage(column.MetadataImage);
            _priority = 50.0;
        }

        public DaxCompletionData(ADOTabular.ADOTabularFunction function)
        {
            _text = function.DaxName;
            _content = function.Caption;
            _description = string.IsNullOrEmpty(function.Description)?function.Caption:function.Description;
            _image = GetMetadataImage(function.MetadataImage);
        }

        public DaxCompletionData(ADOTabular.ADOTabularTable table)
        {
            _text = table.DaxName;
            _content = table.Caption;
            _description = string.IsNullOrEmpty(table.Description)?null:table.Description;
            _image = GetMetadataImage(table.MetadataImage);
            _priority = 100.0;
        }

        public void Complete(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // walk back to start of word
            var newSegment = GetPreceedingWordSegment(textArea, completionSegment);
            var funcParamStart = Text.IndexOf("«");
            string insertionText = funcParamStart > 0?Text.Substring(0, funcParamStart):Text;
            
            if (insertionRequestEventArgs is TextCompositionEventArgs)
            {
                // if the insertion char is the same as the last char in the 
                // insertion text then trim it off
                var insertionChar = ((TextCompositionEventArgs)insertionRequestEventArgs).Text;
                if (insertionText.EndsWith(insertionChar)) insertionText = insertionText.TrimEnd(insertionChar[0]);
            }

            textArea.Document.Replace(newSegment, insertionText);
        }

        private ISegment GetPreceedingWordSegment(ICSharpCode.AvalonEdit.Editing.TextArea textArea, ICSharpCode.AvalonEdit.Document.ISegment completionSegment)
        {
            string line = "";
            
            int pos = completionSegment.EndOffset - 1;
            var loc = textArea.Document.GetLocation(pos);
            var docLine = textArea.Document.GetLineByOffset(pos);
            line = textArea.Document.GetText(docLine.Offset, loc.Column);
            
            return DaxLineParser.GetPreceedingWordSegment(textArea.Document,completionSegment.EndOffset, line);
            
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
            get { return _image; }
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
                case ADOTabular.MetadataImages.DmvTable:
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
    }
}
