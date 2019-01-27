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
using DAXEditor;

namespace DaxStudio.Tests
{
    [TestClass]
    public class CodeCompletionTests
    {
        

        //[TestMethod]
        //public void TestInsertingFunction()
        //{
        //    var editor = new Mock<IEditor>();
        //    editor.SetupGet(x => x.Text).Returns("CALCULATE VALUES([MyColumn])");
        //    var doc = new Mock<IDaxDocument>();
        //    //doc.SetupGet(x => x.Text).Returns("CALCULATE VALUES([MyColumn])");

        //    var _mockEventAggregator = new Mock<IEventAggregator>();
        //    var _mockGlobalOptions = new Mock<IGlobalOptions>();
        //    var mockHost = new Mock<IDaxStudioHost>();
        //    var wm = new Mock<IWindowManager>().Object;
        //    //var optionsVm = new OptionsViewModel(_mockEventAggregator.Object);
        //    var ribbonVm = new RibbonViewModel(mockHost.Object, _mockEventAggregator.Object, wm,  _mockGlobalOptions.Object);
        //    //var timingVm = new ServerTimingDetailsViewModel();
        //    //var docVm = new DocumentViewModel(wm, _mockEventAggregator.Object, mockHost.Object, ribbonVm, timingVm, _mockGlobalOptions.Object);

        //    var provider = new DaxIntellisenseProvider(doc.Object, editor.Object, _mockEventAggregator.Object);
        //    //var composition = new TextComposition();
        //    //System.Windows.Input.TextCompositionEventArgs e = new TextCompositionEventArgs(new InputDevice(), composition);
        //    //provider.ProcessTextEntered(this, e, completionWindow);
        //}

        [TestMethod,Ignore]
        public void TestCodeCompletion()
        {
            var testLine = "CALCULATE VALUES([MyColumn])";
            var mockIp = new Mock<IInsightProvider> ();
            mockIp.Setup(ip => ip.ShowInsight("FILTER"));
            var compData = new DaxCompletionData(mockIp.Object, "FILTER", 1.0);

            var mockDocLine = new Mock<IDocumentLine>();
            
            mockDocLine.SetupGet(dl => dl.Offset).Returns(0);
            mockDocLine.SetupGet(dl => dl.Length).Returns( testLine.Length);

            var mockDoc = new Mock<IDocument>();
            mockDoc.SetupProperty(d => d.Text , testLine);
            mockDoc.Setup(d => d.GetLineByOffset(10)).Returns(mockDocLine.Object);
            mockDoc.Setup(d => d.GetText(0, testLine.Length));
            mockDoc.Setup(d => d.GetText(0, 28)).Returns(testLine.Substring(10));
            mockDoc.Setup(d => d.Replace( It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ITextSource>()))
                .Callback((int offset, int length,ITextSource src) => {
                    var start = src.Text.Substring(0, offset);
                    var end = src.Text.Substring(offset + length);
                    mockDoc.Object.Text = start + src.Text + end;
                })
                .Verifiable();
            var mockSegment = new Mock<ISegment>();
            mockSegment.SetupGet(s => s.EndOffset).Returns(11);

            var e = new TextCompositionEventArgs( Keyboard.PrimaryDevice, new TextComposition(null,null, "F") );
            compData.CompleteInternal(mockDoc.Object, mockSegment.Object, e);
            IDocument doc = mockDoc.Object;
            Assert.AreEqual("CALCULATE FILTER(VALUES([MyColumn])", doc.Text);
        }

        [TestMethod,Ignore]
        public void TestCodeCompletionWithUnderscoresInName()
        {
            var testLine = "EVALUATE DIM_D";
            var mockIp = new Mock<IInsightProvider>();
            mockIp.Setup(ip => ip.ShowInsight("FILTER"));
            var compData = new DaxCompletionData(mockIp.Object, "Dim_Date", 1.0);

            var mockDocLine = new Mock<IDocumentLine>();

            mockDocLine.SetupGet(dl => dl.Offset).Returns(0);
            mockDocLine.SetupGet(dl => dl.Length).Returns(testLine.Length);

            var mockDoc = new Mock<IDocument>();
            mockDoc.SetupProperty(d => d.Text, testLine);
            mockDoc.Setup(d => d.GetLineByOffset(10)).Returns(mockDocLine.Object);
            mockDoc.Setup(d => d.GetText(0, testLine.Length));
            mockDoc.Setup(d => d.GetText(0, 28)).Returns(testLine.Substring(10));
            // todo setup getlocation
            mockDoc.Setup(d => d.Replace(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<ITextSource>()))
                .Callback((int offset, int length, ITextSource src) => {
                    var start = src.Text.Substring(0, offset);
                    var end = src.Text.Substring(offset + length);
                    mockDoc.Object.Text = start + src.Text + end;
                })
                .Verifiable();
            var mockSegment = new Mock<ISegment>();
            mockSegment.SetupGet(s => s.EndOffset).Returns(11);

            var e = new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(null, null, "a"));
            compData.CompleteInternal(mockDoc.Object, mockSegment.Object, e);
            IDocument doc = mockDoc.Object;
            Assert.AreEqual("EVALUTE Dim_Date", doc.Text);
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
    }
}
