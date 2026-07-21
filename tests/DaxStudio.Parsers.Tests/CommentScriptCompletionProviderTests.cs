using DaxStudio.Parsers.Dax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class CommentScriptCompletionProviderTests
    {
        [TestMethod]
        public void IsCommentScriptLine_DetectsMarker()
        {
            Assert.IsTrue(CommentScriptCompletionProvider.IsCommentScriptLine("--> CONNECT"));
            Assert.IsTrue(CommentScriptCompletionProvider.IsCommentScriptLine("   --> "));
            Assert.IsFalse(CommentScriptCompletionProvider.IsCommentScriptLine("EVALUATE"));
            Assert.IsFalse(CommentScriptCompletionProvider.IsCommentScriptLine("-- a comment"));
        }

        [TestMethod]
        public void GetCompletions_EmptyMarker_ReturnsTopLevelCommands()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> ");
            var labels = items.Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "CONNECT");
            CollectionAssert.Contains(labels, "TRACE");
            CollectionAssert.Contains(labels, "ASSERT");
            Assert.IsTrue(items.All(i => i.Kind == CompletionItemKind.Keyword));
        }

        [TestMethod]
        public void GetCompletions_PartialCommand_FiltersByPrefix()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> CON");
            var labels = items.Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "CONNECT");
            Assert.IsFalse(labels.Contains("TRACE"));
        }

        [TestMethod]
        public void GetCompletions_AfterConnect_ReturnsConnectSubCommands()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> CONNECT ");
            var labels = items.Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "SERVER");
            CollectionAssert.Contains(labels, "DESKTOP");
            CollectionAssert.Contains(labels, "SSDT");
        }

        [TestMethod]
        public void GetCompletions_AfterTraceSubCommand_ReturnsOnOff()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> TRACE SERVERTIMINGS ");
            var labels = items.Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "ON");
            CollectionAssert.Contains(labels, "OFF");
        }

        [TestMethod]
        public void GetCompletions_AfterAssertTable_OffersFromResults()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE ");
            var item = items.Single();
            Assert.AreEqual(CommentScriptCompletionProvider.FromResultsLabel, item.Label);
            Assert.AreEqual(CommentScriptCompletionProvider.FromResultsInsertText, item.InsertText);
        }

        [TestMethod]
        public void GetCompletions_AfterAssertTableModifier_OffersFromResults()
        {
            var unordered = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE UNORDERED ");
            Assert.AreEqual(CommentScriptCompletionProvider.FromResultsLabel, unordered.Single().Label);

            var partial = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE PARTIAL ");
            Assert.AreEqual(CommentScriptCompletionProvider.FromResultsLabel, partial.Single().Label);
        }

        [TestMethod]
        public void GetCompletions_TypingTable_StillOffersTableSubCommand()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE");
            var labels = items.Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "TABLE");
        }

        [TestMethod]
        public void GetCompletions_NonCommentScriptLine_ReturnsEmpty()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("EVALUATE 'Sales'");
            Assert.AreEqual(0, items.Count);
        }
    }
}
