using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DAXEditor {
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
    }
}
