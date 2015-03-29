using System;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DAXEditor;
using ICSharpCode.AvalonEdit.Document;

namespace DaxStudio.Tests
{
    [TestClass]
    public class FindReplaceTests
    {
        private MockEditor ed;
        private FindReplaceDialogViewModel vm;
        [TestInitialize]
        public void Init() {
            ed = new MockEditor("This is some sample text\non 3 different lines\nwith more samples");
            
        }

        [TestMethod]
        public void FindCaseInsensitive()
        {
            vm = new FindReplaceDialogViewModel(ed);
            vm.TextToFind = "SAMPLE";
            vm.CaseSensitive = false;
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart);
            Assert.AreEqual(6, ed.SelectionLength);
        }

        [TestMethod]
        public void FindCaseSensitive()
        {
            vm = new FindReplaceDialogViewModel(ed);
            vm.TextToFind = "SAMPLE";
            vm.CaseSensitive = true;
            vm.FindText();
            Assert.AreEqual(0, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(0, ed.SelectionLength, "Selection Length");

            vm.TextToFind = "sample";
            vm.CaseSensitive = true;
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(6, ed.SelectionLength, "Selection Length");
        }

        [TestMethod]
        public void FindWildcard()
        {
            vm = new FindReplaceDialogViewModel(ed);
            vm.UseWildcards = true;
            vm.TextToFind = "sam*";
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(11, ed.SelectionLength, "Selection Length");
        }

        [TestMethod]
        public void FindRegEx()
        {
            vm = new FindReplaceDialogViewModel(ed);
            vm.UseRegex = true;
            vm.TextToFind = "sam[^\\s]*";
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(6, ed.SelectionLength, "Selection Length");

            vm.TextToFind = "\\s.?iff[^\\s]*";
            vm.FindText();
            Assert.AreEqual(29, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(10, ed.SelectionLength, "Selection Length");
            Assert.AreEqual(" different", ed.Selection);
        }

        [TestMethod]
        public void ReplaceTest()
        {
            vm = new FindReplaceDialogViewModel(ed);
            vm.UseRegex = true;
            vm.TextToFind = "sam[^\\s]*";
            vm.TextToReplace = "hello";
            vm.FindText();
            vm.ReplaceText();

            Assert.AreEqual("This is some hello text\non 3 different lines\nwith more samples",
                ed.Text,
                "Replacement Text");

        }

        [TestMethod]
        public void ReplaceAllTest()
        {
            vm = new FindReplaceDialogViewModel(ed);
            vm.UseRegex = true;
            vm.TextToFind = "sam[^\\s]*";
            vm.TextToReplace = "hello";
            //vm.Find();
            vm.ReplaceAllText();

            Assert.AreEqual("This is some hello text\non 3 different lines\nwith more hello",
                ed.Text,
                "Replacement Text");

        }

    }

    public class MockEditor: IEditor
    {
        string _text = "";
        public MockEditor(string sampleText)
        {
            _text = sampleText;
        }

        public string Text { get { return _text; }
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
            TextLocation loc = new TextLocation( lines.Length , lines[(lines.Length - 1)].Length);
            return loc;
        }

        public void DocumentReplace(int offset, int length, string newText)
        {
            _text = _text.Substring(0, offset) + newText + _text.Substring(offset + length);
        }
    }
}
