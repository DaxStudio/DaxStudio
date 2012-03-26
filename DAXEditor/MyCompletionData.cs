using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
using System.Resources;
using DAXEditor.Properties;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAXEditor
{
    public enum CodeCompletionType
    {
        Function,
        Table,
        Column,
        Measure
    }

    /// Implements AvalonEdit ICompletionData interface to provide the entries in the
    /// completion drop down.
    public class MyCompletionData : ICompletionData
    {
        public CodeCompletionType CompletionType;
        public MyCompletionData(string text, CodeCompletionType completionType)
        {
            this.Text = text;
            this.CompletionType = completionType;
        }

        public System.Windows.Media.ImageSource Image
        {
            get {
                Uri uriSource;
                switch (this.CompletionType)
                {
                    case CodeCompletionType.Function:
                        uriSource = new Uri(@"/DAXEditor;component/Resources/DAXEditor.Function.png", UriKind.Relative);
                        return  new BitmapImage(uriSource);
                        
                    case CodeCompletionType.Table:
                        uriSource = new Uri(@"/DAXEditor;component/Resources/DAXEditor.Table.png", UriKind.Relative);
                        return  new BitmapImage(uriSource);
                        
                    case CodeCompletionType.Column:
                        uriSource = new Uri(@"/DAXEditor;component/Resources/DAXEditor.Column.png", UriKind.Relative);
                        return  new BitmapImage(uriSource);
                        
                    case CodeCompletionType.Measure:
                        uriSource = new Uri(@"/DAXEditor;component/Resources/DAXEditor.Measure.png", UriKind.Relative);
                        return  new BitmapImage(uriSource);
                        
                    default:
                        return null;
                }
                
            }
        }

        public string Text { get; private set; }

        // Use this property if you want to show a fancy UIElement in the list.
        public object Content
        {
            get { return this.Text; }
        }

        public object Description
        {
            get { return "Description for " + this.Text; }
        }

        public void Complete(TextArea textArea, ISegment completionSegment,
            EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, this.Text);
        }

        
        double ICompletionData.Priority
        {
            get { return 0.0; }
        }
    }
}
