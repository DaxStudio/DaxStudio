using DAXEditor;
using ICSharpCode.AvalonEdit.Document;

namespace DaxStudio.Tests
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

        public ICSharpCode.AvalonEdit.Document.TextLocation DocumentGetLocation(int offset)
        {
            var lines = _text.Substring(offset).Split('\n');
            TextLocation loc = new TextLocation(lines.Length, lines[(lines.Length - 1)].Length);
            return loc;
        }

        public void DocumentReplace(int offset, int length, string newText)
        {
            _text = _text.Substring(0, offset) + newText + _text.Substring(offset + length);
        }
    }
}
