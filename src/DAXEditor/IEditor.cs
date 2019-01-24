using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;

namespace DAXEditor
{
    public interface IEditor
    {
        string Text {get;}
        int SelectionStart { get; }
        int SelectionLength {get;}
        //ITextSource Document { get; }
        void BeginChange();
        void EndChange();
        void Select(int start, int length);
        void ScrollTo(int line, int col);
        TextLocation DocumentGetLocation(int offset);
        void DocumentReplace(int offset, int length, string newText);
        DocumentLine DocumentGetLineByOffset(int pos);
        string DocumentGetText(int offset, int length);
        string DocumentGetText(TextSegment segment);

        bool IsInComment();

        InsightWindow InsightWindow { get; set; }
        void DisposeCompletionWindow();
        ICSharpCode.AvalonEdit.Editing.TextArea TextArea { get;  }
        //TextDocument Document { get; }

        int CaretOffset { get; }
    }
}
