using System;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Document;
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
            Text = text;
            CompletionType = completionType;
        }

        public System.Windows.Media.ImageSource Image
        {
            get {
                Uri uriSource;
                switch (CompletionType)
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
            get { return Text; }
        }

        public object Description
        {
            get { return "Description for " + Text; }
        }

        public void Complete(TextArea textArea, ISegment completionSegment,
            EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }

        
        double ICompletionData.Priority
        {
            get { return 0.0; }
        }
    }
}
