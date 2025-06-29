﻿using System;
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
                TextToFind = "SAMPLE", // setting TextToFind will trigger the FindText method
                CaseSensitive = false
            };
            Assert.AreEqual(13, ed.SelectionStart);
            Assert.AreEqual(6, ed.SelectionLength);
        }

        [TestMethod]
        public void FindCaseSensitive()
        {
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                Editor = ed,
                CaseSensitive = true,
                UseRegex = false,
                UseWildcards = false,
                TextToFind = "SAMPLE", // setting TextToFind will trigger the FindText method
            };
            Assert.AreEqual(0, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(0, ed.SelectionLength, "Selection Length");

            vm.TextToFind = "sample"; // setting TextToFind will trigger the FindText method

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
                TextToFind = "sam*" // setting TextToFind will trigger the FindText method
            };

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
                TextToFind = "sam[^\\s]*" // setting TextToFind will trigger the FindText method
            };
            vm.FindNext();
            Assert.AreEqual(13, ed.SelectionStart, "Selection Start");
            Assert.AreEqual(6, ed.SelectionLength, "Selection Length");

            vm.TextToFind = "\\s.?iff[^\\s]*";
            vm.FindNext();

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
                TextToFind = "sam[^\\s]*",  // setting TextToFind will trigger the FindText method
                TextToReplace = "hello"
            };
            vm.FindNext();
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

            vm.ReplaceAllText();

            Assert.AreEqual("This is some hello text\non 3 different lines\nwith more hello",
                localEditor.Text,
                "Replacement Text");

        }

        [TestMethod]
        public void ReplaceQuotesTest()
        {
            var localEditor = new MockEditor("Filter(values(table[column]), table[column] in {'1','2','3'} ");
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                // need to ceatea a new editor for replaces tests as they change the text
                Editor = localEditor,
                UseRegex = true,
                UseWildcards = false,
                CaseSensitive = false,
                TextToFind = "'",
                TextToReplace = "\""
            };

            vm.ReplaceAllText();

            Assert.AreEqual("Filter(values(table[column]), table[column] in {\"1\",\"2\",\"3\"} ",
                localEditor.Text,
                "Replacement Text");

        }

        [TestMethod,Ignore] // something is incosistent with this test and it fails sometimes and not others
        public void ReplaceSelectedQuotesTest()
        {
            var localEditor = new MockEditor("{'1','2','3'} ");
            localEditor.Select(0, 8);
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                // need to create a new editor for replaces tests as they change the text
                Editor = localEditor,
                UseRegex = false,
                UseWildcards = false,
                CaseSensitive = false,
                SelectionActive = true
            };
            vm.TextToFind = "'";  // setting TextToFind will trigger the FindText method
            vm.TextToReplace = "\"";

            vm.ReplaceText();
            Assert.AreEqual("{\"1','2','3'} ", localEditor.Text, "First Replacement");

            vm.ReplaceText();
            Assert.AreEqual("{\"1\",'2','3'} ", localEditor.Text, "Second Replacement");

            vm.ReplaceText();
            Assert.AreEqual("{\"1\",\"2','3'} ", localEditor.Text, "Third Replacement");

        }

        [TestMethod]
        public void ReplaceSelectedTextOfDifferentLengthsTest()
        {
            var localEditor = new MockEditor("aa bb aa bb aa bb");
            localEditor.Select(0, 11);
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                // need to ceatea a new editor for replaces tests as they change the text
                Editor = localEditor,
                UseRegex = true,
                UseWildcards = false,
                CaseSensitive = false,
                SelectionActive = true,
                TextToFind = "bb",
                TextToReplace = "cccc"
            };

            vm.ReplaceText();
            vm.ReplaceText();
            vm.ReplaceText();

            Assert.AreEqual("aa cccc aa cccc aa bb",
                localEditor.Text,
                "Replacement Text");

        }

        [TestMethod]
        public void ReplaceAllSelectedQuotesTest()
        {
            var localEditor = new MockEditor("{'1','2','3'} ");
            localEditor.Select(0, 8);
            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                // need to ceatea a new editor for replaces tests as they change the text
                Editor = localEditor,
                UseRegex = true,
                UseWildcards = false,
                CaseSensitive = false,
                SelectionActive = true,
                TextToFind = "'",
                TextToReplace = "\""
            };

            vm.ReplaceAllText();

            Assert.AreEqual("{\"1\",\"2\",'3'} ",
                localEditor.Text,
                "Replacement Text");

        }

        [TestMethod]
        public void ReplaceRegexCaptureTest()
        {
            var localEditor = new MockEditor("Hi abc123xyz there");

            var vm = new FindReplaceDialogViewModel(mockEventAggregator)
            {
                // need to ceatea a new editor for replaces tests as they change the text
                Editor = localEditor,
                UseRegex = true,
                UseWildcards = false,
                CaseSensitive = false,
                TextToFind = @"abc(\d+)xyz",
                TextToReplace = "Output $1"
            };

            vm.ReplaceAllText();

            Assert.AreEqual("Hi Output 123 there",
                localEditor.Text,
                "Replacement Text");

        }

    }

    
}
