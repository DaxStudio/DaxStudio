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
            var qry = @"Evaluate Filter(
// this is a test )
table1
,table1[col1] = 10 )
";
            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditor.BracketRenderer.DaxStudioBracketSearcher();
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
            var qry = @"Evaluate Filter(
-- this is a test )
table1
,table1[col1] = 10 )
";
            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditor.BracketRenderer.DaxStudioBracketSearcher();
            var res = srchr.SearchBracket(mockDoc, 17);
            Assert.IsNull(res);
            res = srchr.SearchBracket(mockDoc, 16);
            Assert.AreEqual(15, res.OpeningBracketOffset, "Test forward Matching Start Bracket");
            Assert.AreEqual(66, res.ClosingBracketOffset, "Test forward Matching End Bracket");
            res = srchr.SearchBracket(mockDoc, 37);
            Assert.IsNull(res, "Should not find bracket in comment");
            res = srchr.SearchBracket(mockDoc, 67);
            Assert.AreEqual(15, res.OpeningBracketOffset, "Test back Matching Start bracket");
            Assert.AreEqual(66, res.ClosingBracketOffset, "Test back Matching End bracket");
        }

        [TestMethod]
        public void TestSkippingStrings()
        {
            var qry = @"Evaluate Filter(
table1
,table1[col1] = "":)"" || ')' )";
            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditor.BracketRenderer.DaxStudioBracketSearcher();
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
            var srchr = new DAXEditor.BracketRenderer.DaxStudioBracketSearcher();
            
            var res = srchr.SearchBracket(mockDoc, 3);
            Assert.AreEqual(1, res.OpeningBracketOffset, "Test forward Matching Start Bracket");
            Assert.AreEqual(2, res.ClosingBracketOffset, "Test forward Matching End Bracket");
            res = srchr.SearchBracket(mockDoc, 1);
            Assert.AreEqual(0, res.ClosingBracketLength, "non-existant end Bracket");
            
        }

        [TestMethod]
        public void TestMultiLineQuery()
        {
            var qry = @"
EVALUATE
    CALCULATETABLE(
    'Product Subcategory',
    'Product Category'[Product Category Name] = @Category 
    ))
";
            var mockDoc = new DocumentMock(qry);
            var srchr = new DAXEditor.BracketRenderer.DaxStudioBracketSearcher();
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

        public event EventHandler TextChanged;

        public int TextLength
        {
            get { return _text.Length; }
        }
    }

}
