using System;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DAXEditorControl;
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
        
        [TestInitialize]
        public void Init() {
            ed = new MockEditor("This is some sample text\non 3 different lines\nwith more samples");
            mockEventAggregator = new MockEventAggregator();
        }

        

        [TestMethod]
        public void FindCaseInsensitive()
        {
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                Editor = ed,
                TextToFind = "SAMPLE",
                CaseSensitive = false
            };
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart);
            Assert.AreEqual(6, ed.SelectionLength);
        }

        [TestMethod]
        public void FindCaseSensitive()
        {
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                Editor = ed,
                TextToFind = "SAMPLE",
                CaseSensitive = true,
                UseRegex = false,
                UseWildcards = false
            };
            vm.FindText();
            Assert.AreEqual(0, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(0, ed.SelectionLength, "Selection Length");

            vm.TextToFind = "sample";
            
            vm.FindText();
            Assert.AreEqual(13, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(6, ed.SelectionLength, "Selection Length");
        }

        [TestMethod]
        public void FindWildcard()
        {

            var newEd = new MockEditor("This is some sample text\non 3 different lines\nwith more samples");
            var vm2 = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                Editor = newEd,
                UseWildcards = true,
                UseRegex = false,
                CaseSensitive = false,
                TextToFind = "sam* "
            };

            vm2.FindText();

            Assert.AreEqual(13, newEd.SelectionStart, "Selection Start");
            Assert.AreEqual(7, newEd.SelectionLength, "Selection Length");
        }

        [TestMethod]
        public void FindWildcardWithFullWords()
        {

            var newEd = new MockEditor("This is some sample text\non 3 different lines\nwith more samples");
            var vm2 = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                Editor = newEd,
                UseWildcards = true,
                UseRegex = false,
                CaseSensitive = false,
                UseWholeWord = true,
                TextToFind = "sam*"
            };

            vm2.FindText();

            Assert.AreEqual(13, newEd.SelectionStart, "Selection Start First");
            Assert.AreEqual(6, newEd.SelectionLength, "Selection Length First");

            vm2.FindNext();

            Assert.AreEqual(56, newEd.SelectionStart, "Selection Start Next");
            Assert.AreEqual(7, newEd.SelectionLength, "Selection Length Next");

        }

        [TestMethod]
        public void FindRegEx()
        {
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                Editor = ed,
                UseRegex = true,
                UseWildcards = false,
                CaseSensitive = false,
                TextToFind = "sam[^\\s]*"
            };
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
            // need to ceatea a new editor for replaces tests as they change the text
            var localEditor = new MockEditor("This is some sample text\non 3 different lines\nwith more samples");
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                Editor = localEditor,
                UseRegex = true,
                UseWildcards = false,
                CaseSensitive = false,
                TextToFind = "sam[^\\s]*",
                TextToReplace = "hello"
            };
            vm.FindText();
            vm.ReplaceText();

            Assert.AreEqual("This is some hello text\non 3 different lines\nwith more samples",
                localEditor.Text,
                "Replacement Text");

        }

        [TestMethod]
        public void ReplaceAllTest()
        {
            var localEditor = new MockEditor("This is some sample text\non 3 different lines\nwith more samples");
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                // need to ceatea a new editor for replaces tests as they change the text
                Editor = localEditor,
                UseRegex = true,
                UseWildcards = false,
                CaseSensitive = false,
                TextToFind = "sam[^\\s]*",
                TextToReplace = "hello"
            };
            //vm.Find();
            vm.ReplaceAllText();

            Assert.AreEqual("This is some hello text\non 3 different lines\nwith more hello",
                localEditor.Text,
                "Replacement Text");

        }

    }

    
}
