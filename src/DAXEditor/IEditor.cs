using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using System;

namespace DAXEditorControl
{
    public interface IEditor
    {
        string Text {get;}
        int SelectionStart { get; }
        int SelectionLength {get;}
        //ITextSource Document { get; }
        void BeginChange();
        void EndChange();
#pragma warning disable CA1716 // Identifiers should not match keywords
        void Select(int start, int length);
#pragma warning restore CA1716 // Identifiers should not match keywords
        void ScrollTo(int line, int col);
        TextLocation DocumentGetLocation(int offset);
        void DocumentReplace(int offset, int length, string newText);
        DocumentLine DocumentGetLineByOffset(int pos);
        DocumentLine DocumentGetLineByNumber(int line);
        string DocumentGetText(int offset, int length);
        string DocumentGetText(TextSegment segment);
        bool IsMouseOverCompletionWindow { get; set; }
        bool IsInComment();

        InsightWindow InsightWindow { get; set; }
        void DisposeCompletionWindow();
        ICSharpCode.AvalonEdit.Editing.TextArea TextArea { get;  }
        //TextDocument Document { get; }

        int CaretOffset { get; }
    }
}
