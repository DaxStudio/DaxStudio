using System;
using DAXEditorControl;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace DaxStudio.Tests.Mocks
{
    public class MockEditor : IEditor
    {
        string _text = "";
        public MockEditor(string sampleText)
        {
            _text = sampleText;
        }

        public string Text
        {
            get { return _text; }
        }

        public int SelectionStart { get; private set; }

        public int SelectionLength { get; private set; }

        public string Selection { get { return _text.Substring(SelectionStart, SelectionLength); } }

        public void BeginChange()
        {
            System.Diagnostics.Debug.WriteLine("Editor.BeginChange Triggered");
        }

        public void EndChange()
        {
            System.Diagnostics.Debug.WriteLine("Editor.EndChange Triggered");
        }

        public void Select(int start, int length)
        {
            SelectionStart = start;
            SelectionLength = length;
        }

        public void ScrollTo(int line, int col)
        {
            _line = line;
            _col = col;
        }
        private int _line;
        private int _col;
        public int Line { get { return _line; } }
        public int Column { get { return _col; } }

        public InsightWindow InsightWindow {get; set;}

        public TextArea TextArea
        {
            get
            {
                return new TextArea();
            }
            
        }

        public TextDocument Document { get;set; }

        public int CaretOffset { get;set; }
        public bool IsMouseOverCompletionWindow { get => false; set { } }

        public ICSharpCode.AvalonEdit.Document.TextLocation DocumentGetLocation(int offset)
        {
            var lines = _text.Substring(offset).Split('\n');
            TextLocation loc = new TextLocation(lines.Length, lines[(lines.Length - 1)].Length);
            return loc;
        }

        public string DocumentGetText(int offset, int length)
        {
            return _text.Substring(offset, length);
        }

        public DocumentLine DocumentGetLineByOffset(int pos)
        {
            throw new NotImplementedException();
        }

        public string DocumentGetText(TextSegment segment)
        {
            throw new NotImplementedException();
        }

        public void DocumentReplace(int offset, int length, string newText)
        {
            _text = _text.Substring(0, offset) + newText + _text.Substring(offset + length);
        }

        public void SetIsInComment(bool value)
        {
            _isInComment = value;
        }

        private bool _isInComment = false;
        public bool IsInComment()
        {
            return _isInComment;
        }

        public void DisposeCompletionWindow()
        {
            // do nothing
        }

        public DocumentLine DocumentGetLineByNumber(int line)
        {
            throw new NotImplementedException();
        }
    }
}
