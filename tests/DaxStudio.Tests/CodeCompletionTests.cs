using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ICSharpCode.AvalonEdit.Document;
using Moq;
using DaxStudio.UI.Utils.Intellisense;
using System;
using System.Windows.Input;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.ViewModels;
using DAXEditorControl;

namespace DaxStudio.Tests
{
    [TestClass]
    public class CodeCompletionTests
    {
        

       

        [TestMethod]
        public void TestCodeCompletionMidStatement()
        {
            // Test inserting "FILTER(" between F and VALUES(
            var testLine = "CALCULATE( FVALUES([MyColumn])";
            var mockIp = new Mock<IInsightProvider> ();
            mockIp.Setup(ip => ip.ShowInsight("FILTER"));
            var compData = new DaxCompletionData(mockIp.Object, "FILTER(«Table", 1.0);

            var mockDocLine = new Mock<IDocumentLine>();
            var mockTextLocation = new TextLocation(0, 12);

            mockDocLine.SetupGet(dl => dl.Length).Returns(testLine.Length);

            var mockDoc = new Mock<IDocument>();
            mockDoc.SetupProperty(d => d.Text, testLine);
            mockDoc.Setup(d => d.GetLineByOffset(12)).Returns(mockDocLine.Object);
            mockDoc.Setup(d => d.GetLocation(12)).Returns(mockTextLocation);
            mockDoc.Setup(d => d.GetText(0, testLine.Length)).Returns(testLine);
            mockDoc.Setup(d => d.TextLength).Returns(testLine.Length);

            mockDoc.Setup(d => d.Replace(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback((int offset, int length, string src) => {
                    var start = mockDoc.Object.Text.Substring(0, offset);
                    var end = mockDoc.Object.Text.Substring(offset + length);
                    mockDoc.Object.Text = start + src + end;
                })
                .Verifiable();
            var mockSegment = new Mock<ISegment>();
            mockSegment.SetupGet(s => s.EndOffset).Returns(13);
            mockSegment.SetupGet(s => s.Length).Returns(0);

            IDocument doc = mockDoc.Object;
            ISegment seg = mockSegment.Object;
            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "I"));
            compData.CompleteInternal(doc, seg, e);

            Assert.AreEqual("CALCULATE( FILTER(VALUES([MyColumn])", doc.Text);
        }

        [TestMethod]
        public void TestCodeCompletionWithUnderscoresInName()
        {
            var testLine = "EVALUATE DIM_D";
            var mockIp = new Mock<IInsightProvider>();
            mockIp.Setup(ip => ip.ShowInsight("FILTER"));
            var compData = new DaxCompletionData(mockIp.Object, "Dim_Date", 1.0);

            var mockDocLine = new Mock<IDocumentLine>();
            var mockTextLocation = new TextLocation(0,testLine.Length);

            mockDocLine.SetupGet(dl => dl.Length).Returns(testLine.Length);

            var mockDoc = new Mock<IDocument>();
            mockDoc.SetupProperty(d => d.Text,testLine);
            //mockDoc.Object.Text = testLine;
            mockDoc.Setup(d => d.GetLineByOffset(testLine.Length-1)).Returns(mockDocLine.Object);
            mockDoc.Setup(d => d.GetLocation(testLine.Length-1)).Returns(mockTextLocation);
            mockDoc.Setup(d => d.GetText(0, testLine.Length)).Returns(testLine);
            mockDoc.Setup(d => d.TextLength).Returns(testLine.Length);

            mockDoc.Setup(d => d.Replace(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>()))
                .Callback((int offset, int length, string src) => {
                    var start = mockDoc.Object.Text.Substring(0, offset);
                    var end = mockDoc.Object.Text.Substring(offset + length);
                    mockDoc.Object.Text = start + src + end;
                })
                .Verifiable();
            var mockSegment = new Mock<ISegment>();
            mockSegment.SetupGet(s => s.EndOffset).Returns(testLine.Length);

            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "a"));
            IDocument doc = mockDoc.Object;
            ISegment seg = mockSegment.Object;
            compData.CompleteInternal(doc, seg, e);
            
            Assert.AreEqual("EVALUATE Dim_Date", doc.Text);
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
