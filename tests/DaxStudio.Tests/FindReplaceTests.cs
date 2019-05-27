using System;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DAXEditor;
using ICSharpCode.AvalonEdit.Document;
using DaxStudio.Tests.Mocks;
using Caliburn.Micro;

namespace DaxStudio.Tests
{
    [TestClass]
    public class FindReplaceTests
    {
        private MockEditor ed;
        private IEventAggregator mockEventAggregator;
        private FindReplaceDialogViewModel vm;
        [TestInitialize]
        public void Init() {
            ed = new MockEditor("This is some sample text\non 3 different lines\nwith more samples");
            mockEventAggregator = new MockEventAggregator();
        }

        [TestMethod]
        public void FindCaseInsensitive()
        {
            vm = new FindReplaceDialogViewModel(mockEventAggregator);
            vm.Editor = ed;
            vm.TextToFind = "SAMPLE";
            vm.CaseSensitive = false;
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart);
            Assert.AreEqual(6, ed.SelectionLength);
        }

        [TestMethod]
        public void FindCaseSensitive()
        {
            vm = new FindReplaceDialogViewModel(mockEventAggregator);
            vm.Editor = ed;
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
            // TODO - this test fails intermittently, need to see if we can figure out why...

            vm = new FindReplaceDialogViewModel(mockEventAggregator);
            vm.Editor = ed;
            vm.UseWildcards = true;
            vm.TextToFind = "sam*";
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(11, ed.SelectionLength, "Selection Length");
        }

        [TestMethod]
        public void FindRegEx()
        {
            vm = new FindReplaceDialogViewModel(mockEventAggregator);
            vm.Editor = ed;
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
            vm = new FindReplaceDialogViewModel(mockEventAggregator);
            vm.Editor = ed;
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
            vm = new FindReplaceDialogViewModel(mockEventAggregator);
            vm.Editor = ed;
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

    
}
