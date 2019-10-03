using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class DaxEditorBracketHighlightTests
    {
        [TestMethod]
        public void TestSkippingDoubleSlashComments()
        {
            var qry = @"Evaluate Filter(" + Environment.NewLine
                    + "// this is a test )" + Environment.NewLine
                    + "table1" + Environment.NewLine
                    + ",table1[col1] = 10 )" + Environment.NewLine;

            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditorControl.BracketRenderer.DaxStudioBracketSearcher();
            var res = srchr.SearchBracket(mockDoc, 17);
            Assert.IsNull(res, "Test should not match anything");
            // test matching end br4acket
            res = srchr.SearchBracket(mockDoc, 16);
            Assert.AreEqual(15, res.OpeningBracketOffset, "Test forward Matching Start Bracket");
            Assert.AreEqual(66, res.ClosingBracketOffset, "Test forward Matching End Bracket");
            // test
            res = srchr.SearchBracket(mockDoc, 67);
            Assert.AreEqual(15, res.OpeningBracketOffset,"Test back Matching Start bracket");
            Assert.AreEqual(66, res.ClosingBracketOffset, "Test back Matching End bracket");
        }

        [TestMethod]
        public void TestSkippingDoubleDashComments()
        {
            var qry = @"Evaluate Filter(" + Environment.NewLine
                    + "-- this is a test )\"" + Environment.NewLine
                    + "table1" + Environment.NewLine
                    + ",table1[col1] = 10 )" + Environment.NewLine;

            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditorControl.BracketRenderer.DaxStudioBracketSearcher();
            var res = srchr.SearchBracket(mockDoc, 17);
            Assert.IsNull(res);
            res = srchr.SearchBracket(mockDoc, 16);
            Assert.AreEqual(15, res.OpeningBracketOffset, "Test forward Matching Start Bracket");
            Assert.AreEqual(67, res.ClosingBracketOffset, "Test forward Matching End Bracket");
            res = srchr.SearchBracket(mockDoc, 37);
            Assert.IsNull(res, "Should not find bracket in comment");
            res = srchr.SearchBracket(mockDoc, 68);
            Assert.AreEqual(15, res.OpeningBracketOffset, "Test back Matching Start bracket");
            Assert.AreEqual(67, res.ClosingBracketOffset, "Test back Matching End bracket");
        }

        [TestMethod]
        public void TestSkippingStrings()
        {
            var qry = "Evaluate Filter(" + Environment.NewLine
                    + "table1" + Environment.NewLine
                    + ",table1[col1] = \":)\" || ')' )";
            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditorControl.BracketRenderer.DaxStudioBracketSearcher();
            var res = srchr.SearchBracket(mockDoc, 17);
            Assert.IsNull(res);
            res = srchr.SearchBracket(mockDoc, 16);
            Assert.AreEqual(15, res.OpeningBracketOffset, "Test forward Matching Start Bracket");
            Assert.AreEqual(54, res.ClosingBracketOffset, "Test forward Matching End Bracket");
            
            res = srchr.SearchBracket(mockDoc, 45);
            Assert.IsNull(res, "Should not find bracket in string");
            
            res = srchr.SearchBracket(mockDoc, 52);
            Assert.IsNull(res, "Should not find bracket in char");
            
            res = srchr.SearchBracket(mockDoc, 55);
            Assert.AreEqual(15, res.OpeningBracketOffset, "Test back Matching Start bracket");
            Assert.AreEqual(54, res.ClosingBracketOffset, "Test back Matching End bracket");
             
        }
        [TestMethod]
        public void TestUnbalancedBrackets()
        {
            var qry = @"(()";
            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditorControl.BracketRenderer.DaxStudioBracketSearcher();
            
            var res = srchr.SearchBracket(mockDoc, 3);
            Assert.AreEqual(1, res.OpeningBracketOffset, "Test forward Matching Start Bracket");
            Assert.AreEqual(2, res.ClosingBracketOffset, "Test forward Matching End Bracket");
            res = srchr.SearchBracket(mockDoc, 1);
            Assert.AreEqual(0, res.ClosingBracketLength, "non-existant end Bracket");
            
        }

        [TestMethod]
        public void TestMultiLineQuery()
        {
            var qry = Environment.NewLine
                    + "EVALUATE" + Environment.NewLine
                    + "    CALCULATETABLE(" + Environment.NewLine
                    + "    'Product Subcategory'," + Environment.NewLine
                    + "    'Product Category'[Product Category Name] = @Category " + Environment.NewLine
                    + "    ))" + Environment.NewLine;

            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditorControl.BracketRenderer.DaxStudioBracketSearcher();
            var res = srchr.SearchBracket(mockDoc, 1);
            Assert.IsNull(res, "no match found at start of string");
            res = srchr.SearchBracket(mockDoc, 31);
            Assert.AreEqual(125, res.ClosingBracketOffset, "Found End Bracket");
            res = srchr.SearchBracket(mockDoc, 126);
            Assert.AreEqual(30, res.OpeningBracketOffset, "Found End Bracket");
            res = srchr.SearchBracket(mockDoc, 127);
            Assert.AreEqual(126, res.OpeningBracketOffset, "Found End Bracket");
            Assert.AreEqual(0, res.ClosingBracketOffset, "No Start Bracket");
        }
    }



    public class DocumentMock: ICSharpCode.AvalonEdit.Document.ITextSource
    {
        private string _text;
        public DocumentMock(string text)
        {
            _text = text;
        }
        public System.IO.TextReader CreateReader()
        {
            throw new NotImplementedException();
        }

        public ICSharpCode.AvalonEdit.Document.ITextSource CreateSnapshot(int offset, int length)
        {
            throw new NotImplementedException();
        }

        public ICSharpCode.AvalonEdit.Document.ITextSource CreateSnapshot()
        {
            throw new NotImplementedException();
        }

        public char GetCharAt(int offset)
        {
            return _text[offset];
        }

        public string GetText(int offset, int length)
        {
            return _text.Substring(offset, length);
        }

        public int IndexOfAny(char[] anyOf, int startIndex, int count)
        {
            throw new NotImplementedException();
        }

        public string Text
        {
            get { return _text; }
        }
#pragma warning disable CS0067
        // required for implementing the interface, but not used for these tests
        public event EventHandler TextChanged;
#pragma warning restore CS0067
        public int TextLength
        {
            get { return _text.Length; }
        }

        public System.IO.TextReader CreateReader(int offset, int length)
        {
            throw new NotImplementedException();
        }

        public string GetText(ICSharpCode.AvalonEdit.Document.ISegment segment)
        {
            throw new NotImplementedException();
        }

        public int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
        {
            throw new NotImplementedException();
        }

        public int IndexOf(char c, int startIndex, int count)
        {
            throw new NotImplementedException();
        }

        public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
        {
            throw new NotImplementedException();
        }

        public int LastIndexOf(char c, int startIndex, int count)
        {
            throw new NotImplementedException();
        }

        public ICSharpCode.AvalonEdit.Document.ITextSourceVersion Version
        {
            get { throw new NotImplementedException(); }
        }

        public void WriteTextTo(System.IO.TextWriter writer, int offset, int length)
        {
            throw new NotImplementedException();
        }

        public void WriteTextTo(System.IO.TextWriter writer)
        {
            throw new NotImplementedException();
        }
    }

}
