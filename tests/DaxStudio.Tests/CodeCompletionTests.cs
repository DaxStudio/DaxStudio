using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.AvalonEdit.Document;
using DaxStudio.UI.Utils.Intellisense;
using System;
using System.Windows.Input;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.ViewModels;
using DAXEditorControl;
using NSubstitute;

namespace DaxStudio.Tests
{
    [STATestClass]
    public class CodeCompletionTests
    {

        [TestMethod]
        public void TestCodeCompletionMidStatement()
        {
            // Test inserting "FILTER(" between F and VALUES(
            var testLine = "CALCULATE( FVALUES([MyColumn])";
            var mockIp = Substitute.For<IInsightProvider>();
            mockIp.ShowInsight("FILTER");
            var compData = new DaxCompletionData(mockIp, "FILTER(«Table", 1.0);

            var mockDocLine = Substitute.For<IDocumentLine>();
            var mockTextLocation = new TextLocation(0, 12);

            mockDocLine.Length.Returns(testLine.Length);

            var mockDoc = Substitute.For<IDocument>();
            mockDoc.Text.Returns(testLine);
            mockDoc.GetLineByOffset(12).Returns(mockDocLine);
            mockDoc.GetLocation(12).Returns(mockTextLocation);
            mockDoc.GetText(0, testLine.Length).Returns(testLine);
            mockDoc.TextLength.Returns(testLine.Length);

            string documentText = testLine; // Keep track of the text state
            mockDoc.Text.Returns(documentText);

            mockDoc.When(x => x.Replace(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()))
                .Do(callInfo => {
                    int offset = callInfo.ArgAt<int>(0);
                    int length = callInfo.ArgAt<int>(1);
                    string src = callInfo.ArgAt<string>(2);
                    
                    var start = documentText.Substring(0, offset);
                    var end = documentText.Substring(offset + length);
                    documentText = start + src + end;
                    
                    // Update the mock to return the new text
                    mockDoc.Text.Returns(documentText);
                });
            var mockSegment = Substitute.For<ISegment>();
            mockSegment.EndOffset.Returns(13);
            mockSegment.Length.Returns(0);

            IDocument doc = mockDoc;
            ISegment seg = mockSegment;
            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "I"));
            compData.CompleteInternal(doc, seg, e);

            Assert.AreEqual("CALCULATE( FILTER(VALUES([MyColumn])", doc.Text);
        }

        [TestMethod]
        public void TestMidWordFunctionRename_ReplacesWholeWord()
        {
            // The user edits an existing function name: the caret is in the middle of "SELECOLUMNS"
            // (after "SELE") and they pick "SELECTCOLUMNS". The whole identifier must be replaced so the
            // result is "SELECTCOLUMNS(...)" and NOT "SELECTCOLUMNSCOLUMNS(...)".
            var doc = new TextDocument("EVALUATE SELECOLUMNS([MyColumn])");
            var mockIp = Substitute.For<IInsightProvider>();
            var compData = new DaxCompletionData(mockIp, "SELECTCOLUMNS", 1.0);

            int caret = "EVALUATE SELE".Length; // caret in the middle of the identifier
            var seg = new TextSegment { StartOffset = caret, Length = 0 };
            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "\t"));

            compData.CompleteInternal(doc, seg, e);

            Assert.AreEqual("EVALUATE SELECTCOLUMNS([MyColumn])", doc.Text);
        }

        [TestMethod]
        public void TestMidWordFunctionRename_CaretAtEndOfWord()
        {
            // When the caret is at the end of the word being replaced there is no trailing identifier
            // text, so only that word is replaced.
            var doc = new TextDocument("EVALUATE SUM([Sales])");
            var mockIp = Substitute.For<IInsightProvider>();
            var compData = new DaxCompletionData(mockIp, "SUMX", 1.0);

            int caret = "EVALUATE SUM".Length; // caret at the end of "SUM", before "("
            var seg = new TextSegment { StartOffset = caret, Length = 0 };
            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "\t"));

            compData.CompleteInternal(doc, seg, e);

            Assert.AreEqual("EVALUATE SUMX([Sales])", doc.Text);
        }

        [TestMethod]
        public void TestMidWordWrappingInsert_PreservesTrailingText()
        {
            // A "wrapping" completion whose text ends with "(" is inserted at the caret and must NOT
            // consume the following identifier: inserting "FILTER(" before "VALUES" yields
            // "FILTER(VALUES(...))".
            var doc = new TextDocument("CALCULATE( FVALUES([MyColumn])");
            var mockIp = Substitute.For<IInsightProvider>();
            var compData = new DaxCompletionData(mockIp, "FILTER(«Table", 1.0);

            int caret = "CALCULATE( F".Length; // caret right after the "F"
            var seg = new TextSegment { StartOffset = caret, Length = 0 };
            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "\t"));

            compData.CompleteInternal(doc, seg, e);

            Assert.AreEqual("CALCULATE( FILTER(VALUES([MyColumn])", doc.Text);
        }

        [TestMethod]
        public void TestCodeCompletionWithUnderscoresInName()
        {
            var testLine = "EVALUATE DIM_D";
            var mockIp = Substitute.For<IInsightProvider>();
            mockIp.ShowInsight("FILTER");
            var compData = new DaxCompletionData(mockIp, "Dim_Date", 1.0);

            var mockDocLine = Substitute.For<IDocumentLine>();
            var mockTextLocation = new TextLocation(0,testLine.Length);

            mockDocLine.Length.Returns(testLine.Length);

            var mockDoc = Substitute.For<IDocument>();
            mockDoc.Text.Returns(testLine);
            //mockDoc.Object.Text = testLine;
            mockDoc.GetLineByOffset(testLine.Length-1).Returns(mockDocLine);
            mockDoc.GetLocation(testLine.Length-1).Returns(mockTextLocation);
            mockDoc.GetText(0, testLine.Length).Returns(testLine);
            mockDoc.TextLength.Returns(testLine.Length);

            mockDoc.When(x => x.Replace(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()))
                .Do(callInfo =>
                {
                    int offset = callInfo.ArgAt<int>(0);
                    int length = callInfo.ArgAt<int>(1);
                    string src = callInfo.ArgAt<string>(2);

                    var start = mockDoc.Text.Substring(0, offset);
                    var end = mockDoc.Text.Substring(offset + length);
                    mockDoc.Text = start + src + end;
                });
            var mockSegment = Substitute.For<ISegment>();
            mockSegment.EndOffset.Returns(testLine.Length);

            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "a"));
            IDocument doc = mockDoc;
            ISegment seg = mockSegment;
            compData.CompleteInternal(doc, seg, e);
            
            Assert.AreEqual("EVALUATE Dim_Date", doc.Text);
        }


        [TestMethod]
        public void TestCodeCompletionWithNumbersInName()
        {
            var testLine = "EVALUATE Dim1";
            var mockIp = Substitute.For<IInsightProvider>();
            mockIp.ShowInsight("FILTER");
            var compData = new DaxCompletionData(mockIp, "Dim1Date", 1.0);

            var mockDocLine = Substitute.For<IDocumentLine>();
            var mockTextLocation = new TextLocation(0, testLine.Length);

            mockDocLine.Length.Returns(testLine.Length);

            var mockDoc = Substitute.For<IDocument>();
            mockDoc.Text.Returns(testLine);
            //mockDoc.Object.Text = testLine;
            mockDoc.GetLineByOffset(testLine.Length - 1).Returns(mockDocLine);
            mockDoc.GetLocation(testLine.Length - 1).Returns(mockTextLocation);
            mockDoc.GetText(0, testLine.Length).Returns(testLine);
            mockDoc.TextLength.Returns(testLine.Length);

            mockDoc.When(x => x.Replace(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>()))
                .Do(callInfo =>
                {
                    int offset = callInfo.ArgAt<int>(0);
                    int length = callInfo.ArgAt<int>(1);
                    string src = callInfo.ArgAt<string>(2);

                    var start = mockDoc.Text.Substring(0, offset);
                    var end = mockDoc.Text.Substring(offset + length);
                    mockDoc.Text = start + src + end;
                });
            var mockSegment = Substitute.For<ISegment>();
            mockSegment.EndOffset.Returns(testLine.Length);

            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "a"));
            IDocument doc = mockDoc;
            ISegment seg = mockSegment;
            compData.CompleteInternal(doc, seg, e);

            Assert.AreEqual("EVALUATE Dim1Date", doc.Text);
        }

        [TestMethod]
        public void TestFunctionCompletion() {
            var line = "CALCULATE FILVALUES([MyColumn])";
            var col = 13;
            var daxState = DaxLineParser.ParseLine(line, col, 0);

            var pos = DaxLineParser.GetPreceedingWordSegment( 0, col, line, daxState);
            System.Diagnostics.Debug.WriteLine(pos);
            Assert.AreEqual(3, pos.Length);
        }

        //[TestMethod]
        //public void TestOpenColumnCompletion()
        //{
        //    var line = "OR ( PATHCONTAINS ( @Store, 'Store'[Store K), @Store = \"All\" )";
        //    var col = 43;
        //    var daxState = DaxLineParser.ParseLine(line, col, 0);

        //    var pos = DaxLineParser.GetPreceedingWordSegment(0, col, line, daxState);
        //    System.Diagnostics.Debug.WriteLine(pos);
        //    var colName = line.Substring(pos.Offset, pos.Length);

        //    Assert.AreEqual(35, pos.Offset);
        //    Assert.AreEqual("[Store K", colName);
        //    //Assert.AreEqual(6, pos.Length);

        //}

        [TestMethod]
        public void TestOpenColumnCompletion()
        {
            var line = "'Store'[Store K), @Store = \"All\" )";
            var col = 15;
            var daxState = DaxLineParser.ParseLine(line, col, 0);

            var pos = DaxLineParser.GetPreceedingWordSegment(0, col, line, daxState);
            System.Diagnostics.Debug.WriteLine(pos);
            var colName = line.Substring(pos.Offset, pos.Length);

            Assert.AreEqual(7, pos.Offset);
            Assert.AreEqual("[Store K", colName);
            //Assert.AreEqual(6, pos.Length);

        }


        [TestMethod]
        public void TestClosedColumnCompletion()
        {
            var line = "'Store'[Store K]), @Store = \"All\" )";
            var col = 15;
            var daxState = DaxLineParser.ParseLine(line, col, 0);

            var pos = DaxLineParser.GetPreceedingWordSegment(0, col, line, daxState);
            System.Diagnostics.Debug.WriteLine(pos);
            var colName = line.Substring(pos.Offset, pos.Length);
            Assert.AreEqual(7, pos.Offset);
            Assert.AreEqual("[Store K]", colName);
            Assert.AreEqual(LineState.ColumnClosed, daxState.LineState);
        }


        [TestMethod]
        public void TestDmvCompletion()
        {
            var line = "SELECT * FROM $SYS";
            var col = line.Length;
            var daxState = DaxLineParser.ParseLine(line, col, 0);

            var pos = DaxLineParser.GetPreceedingWordSegment(0, col, line, daxState);
            System.Diagnostics.Debug.WriteLine(pos);
            Assert.AreEqual(14, pos.Offset);
            Assert.AreEqual(4, pos.Length);

        }

    }
}
