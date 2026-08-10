using DaxStudio.Parsers.Dax;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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
            var item = items.First(i => i.Label == CommentScriptCompletionProvider.FromResultsLabel);
            Assert.AreEqual(CommentScriptCompletionProvider.FromResultsInsertText, item.InsertText);
        }

        [TestMethod]
        public void GetCompletions_AfterAssertTableModifier_OffersFromResults()
        {
            var unordered = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE UNORDERED ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(unordered, CommentScriptCompletionProvider.FromResultsLabel);

            var partial = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE PARTIAL ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(partial, CommentScriptCompletionProvider.FromResultsLabel);
        }

        [TestMethod]
        public void GetCompletions_EmptyMarker_IncludesBaseline()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "BASELINE");
        }

        [TestMethod]
        public void GetCompletions_AfterAssertTable_AlsoOffersBaseline()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "BASELINE");

            var unordered = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE UNORDERED ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(unordered, "BASELINE");
        }

        [TestMethod]
        public void GetCompletions_AfterComparisonOperator_OffersBaseline()
        {
            foreach (var op in new[] { "<=", "<", ">=", ">", "=" })
            {
                var labels = CommentScriptCompletionProvider.GetCompletions($"--> ASSERT DURATION {op} ").Select(i => i.Label).ToList();
                CollectionAssert.Contains(labels, "BASELINE", $"operator '{op}'");
            }

            var rowcount = CommentScriptCompletionProvider.GetCompletions("--> ASSERT ROWCOUNT = ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(rowcount, "BASELINE");
        }

        [TestMethod]
        public void GetCompletions_AfterComparisonOperator_AlsoOffersPrevious()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> ASSERT DURATION <= ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "PREVIOUS");

            var rowcount = CommentScriptCompletionProvider.GetCompletions("--> ASSERT ROWCOUNT = ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(rowcount, "PREVIOUS");
        }

        [TestMethod]
        public void GetCompletions_AfterAssertTable_AlsoOffersPrevious()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "PREVIOUS");

            var unordered = CommentScriptCompletionProvider.GetCompletions("--> ASSERT TABLE UNORDERED ").Select(i => i.Label).ToList();
            CollectionAssert.Contains(unordered, "PREVIOUS");
        }

        [TestMethod]
        public void GetCompletions_TypingPreviousAfterOperator_FiltersByPrefix()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> ASSERT DURATION <= PRE").Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "PREVIOUS");
            CollectionAssert.DoesNotContain(labels, "BASELINE");
        }

        [TestMethod]
        public void GetCompletions_TypingBaselineAfterOperator_FiltersByPrefix()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> ASSERT DURATION <= BAS").Select(i => i.Label).ToList();
            CollectionAssert.Contains(labels, "BASELINE");
        }

        [TestMethod]
        public void GetCompletions_AfterBaselineKeyword_OffersCapturedBaselineNames()
        {
            const string script = "--> BASELINE \"original\"\nEVALUATE { 1 }\n--> GO\n";

            var labels = CommentScriptCompletionProvider
                .GetCompletions("--> ASSERT DURATION <= BASELINE ", script)
                .Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "\"original\"");
        }

        [TestMethod]
        public void GetCompletions_AfterBaselineKeywordOnAssertTable_OffersCapturedBaselineNames()
        {
            const string script = "--> BASELINE \"original\"\nEVALUATE { 1 }\n--> GO\n";

            var labels = CommentScriptCompletionProvider
                .GetCompletions("--> ASSERT TABLE UNORDERED BASELINE ", script)
                .Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "\"original\"");
        }

        [TestMethod]
        public void GetCompletions_TypingBaselineName_FiltersByPrefix()
        {
            const string script = "--> BASELINE \"original\"\n--> GO\n--> BASELINE \"tuned\"\n--> GO\n";

            var labels = CommentScriptCompletionProvider
                .GetCompletions("--> ASSERT DURATION <= BASELINE \"or", script)
                .Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "\"original\"");
            CollectionAssert.DoesNotContain(labels, "\"tuned\"");
        }

        [TestMethod]
        public void GetDefinedBaselines_SkipsTheUnnamedForm()
        {
            const string script = "--> BASELINE\n--> GO\n--> BASELINE bare\n--> GO\n--> BASELINE \"quoted\"\n";

            var names = CommentScriptCompletionProvider.GetDefinedBaselines(script).ToList();

            CollectionAssert.AreEqual(new[] { "bare", "quoted" }, names);
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

        [TestMethod]
        public void GetCompletions_EmptyMarker_IncludesSetAndSaveAs()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> ").Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "SET");
            CollectionAssert.Contains(labels, "SAVEAS");
        }

        [TestMethod]
        public void GetCompletions_PartialSet_FiltersToSet()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> SE").Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "SET");
            Assert.IsFalse(labels.Contains("CONNECT"));
        }

        [TestMethod]
        public void GetDefinedVariables_ReturnsSetNamesInOrder()
        {
            var script = "--> SET OutDir = \"C:\\Reports\"\r\n--> SET Env = prod\r\nEVALUATE 'Sales'\r\n";

            var names = CommentScriptCompletionProvider.GetDefinedVariables(script);

            CollectionAssert.AreEqual(new[] { "OutDir", "Env" }, names.ToArray());
        }

        [TestMethod]
        public void GetDefinedVariables_IgnoresDuplicatesAndNonSetLines()
        {
            var script = "--> SET OutDir = \"a\"\r\n--> SAVEAS \"x.dax\"\r\n-- SET NotACommand = 1\r\n--> SET outdir = \"b\"\r\n";

            var names = CommentScriptCompletionProvider.GetDefinedVariables(script);

            CollectionAssert.AreEqual(new[] { "OutDir" }, names.ToArray());
        }

        [TestMethod]
        public void GetCompletions_AfterDollar_ReturnsDefinedVariablesAndBuiltIns()
        {
            var script = "--> SET OutDir = \"C:\\Reports\"\r\n--> SAVEAS \"$";

            var items = CommentScriptCompletionProvider.GetCompletions("--> SAVEAS \"$", script);
            var labels = items.Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "$(OutDir)");
            CollectionAssert.Contains(labels, "$(now:yyyy-MM-dd)");
            CollectionAssert.Contains(labels, "$(utcnow:yyyy-MM-dd)");
            CollectionAssert.Contains(labels, "$(env:NAME)");
            Assert.IsTrue(items.All(i => i.Kind == CompletionItemKind.Variable));
        }

        [TestMethod]
        public void GetCompletions_VariableItem_InsertsBareNameForFiltering()
        {
            var script = "--> SET OutDir = \"C:\\Reports\"\r\n";

            var item = CommentScriptCompletionProvider.GetCompletions("--> SAVEAS \"$", script)
                .Single(i => i.Label == "$(OutDir)");

            Assert.AreEqual("OutDir", item.InsertText);
        }

        [TestMethod]
        public void GetCompletions_PartialVariableReference_FiltersByPrefix()
        {
            var script = "--> SET OutDir = \"a\"\r\n--> SET Env = prod\r\n";

            var labels = CommentScriptCompletionProvider.GetCompletions("--> SAVEAS \"$(Out", script)
                .Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "$(OutDir)");
            Assert.IsFalse(labels.Contains("$(Env)"));
            Assert.IsFalse(labels.Contains("$(now:yyyy-MM-dd)"));
        }

        [TestMethod]
        public void GetCompletions_NoSetCommands_StillOffersBuiltInVariables()
        {
            var labels = CommentScriptCompletionProvider.GetCompletions("--> EXPORT METRICS \"$", null)
                .Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "$(now:yyyy-MM-dd)");
            Assert.AreEqual(3, labels.Count);
        }

        [TestMethod]
        public void GetCompletions_EscapedDollarParen_IsNotAVariableReference()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> SAVEAS \"$$", "--> SET OutDir = \"a\"\r\n");

            Assert.IsFalse(items.Any(i => i.Kind == CompletionItemKind.Variable));
        }

        [TestMethod]
        public void GetCompletions_ClosedVariableReference_ReturnsNoVariables()
        {
            var items = CommentScriptCompletionProvider.GetCompletions("--> SAVEAS \"$(OutDir)", "--> SET OutDir = \"a\"\r\n");

            Assert.IsFalse(items.Any(i => i.Kind == CompletionItemKind.Variable));
        }

        [TestMethod]
        public void TryGetVariableReferencePrefix_HandlesTheCommonCases()        {
            Assert.IsTrue(CommentScriptCompletionProvider.TryGetVariableReferencePrefix(" SAVEAS \"$", out var p1));
            Assert.AreEqual(string.Empty, p1);

            Assert.IsTrue(CommentScriptCompletionProvider.TryGetVariableReferencePrefix(" SAVEAS \"$(", out var p2));
            Assert.AreEqual(string.Empty, p2);

            Assert.IsTrue(CommentScriptCompletionProvider.TryGetVariableReferencePrefix(" SAVEAS \"$(Out", out var p3));
            Assert.AreEqual("Out", p3);

            Assert.IsFalse(CommentScriptCompletionProvider.TryGetVariableReferencePrefix(" SAVEAS \"file.dax\"", out _));
            Assert.IsFalse(CommentScriptCompletionProvider.TryGetVariableReferencePrefix(" SAVEAS \"$(Out Dir", out _));
        }

        [TestMethod]
        public void GetCompletions_OnSetLine_ExcludesTheVariableBeingDefined()
        {
            // A SET value is expanded eagerly so it cannot reference itself - "myFile" must not be offered
            // while defining "myFile", but the earlier "myPath" still is.
            var script = "--> SET myPath = \"C:\\temp\\\"\r\n--> SET myFile = \"$";

            var labels = CommentScriptCompletionProvider.GetCompletions("--> SET myFile = \"$", script)
                .Select(i => i.Label).ToList();

            CollectionAssert.DoesNotContain(labels, "$(myFile)");
            CollectionAssert.Contains(labels, "$(myPath)");
            CollectionAssert.Contains(labels, "$(now:yyyy-MM-dd)");
        }

        [TestMethod]
        public void GetCompletions_OnSetLine_ExclusionIsCaseInsensitive()
        {
            var script = "--> SET MyFile = \"a\"\r\n--> SET myfile = \"$";

            var labels = CommentScriptCompletionProvider.GetCompletions("--> SET myfile = \"$", script)
                .Select(i => i.Label).ToList();

            Assert.IsFalse(labels.Any(l => l.StartsWith("$(myfile", StringComparison.OrdinalIgnoreCase)));
        }

        [TestMethod]
        public void GetCompletions_OnNonSetLine_StillOffersEveryVariable()
        {
            var script = "--> SET myFile = \"a\"\r\n--> SAVEAS \"$";

            var labels = CommentScriptCompletionProvider.GetCompletions("--> SAVEAS \"$", script)
                .Select(i => i.Label).ToList();

            CollectionAssert.Contains(labels, "$(myFile)");
        }

        [TestMethod]
        public void GetVariableBeingDefined_ReturnsNameOnlyForSetLines()
        {
            Assert.AreEqual("myFile", CommentScriptCompletionProvider.GetVariableBeingDefined(" SET myFile = \"$"));
            Assert.IsNull(CommentScriptCompletionProvider.GetVariableBeingDefined(" SAVEAS \"$"));
            Assert.IsNull(CommentScriptCompletionProvider.GetVariableBeingDefined(" SET myFile"));
        }
    }
}
