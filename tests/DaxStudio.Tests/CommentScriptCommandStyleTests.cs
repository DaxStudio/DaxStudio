using System.Reflection;
using System.Xml;
using DAXEditorControl.Renderers;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class CommentScriptCommandStyleTests
    {
        [DataTestMethod]
        [DataRow("--> TEST \"Demo\"", CommentScriptCommandCategory.Scenario)]
        [DataRow("  --> baseline", CommentScriptCommandCategory.Scenario)]
        [DataRow("--> CONNECT localhost", CommentScriptCommandCategory.Connection)]
        [DataRow("--> USE \"Adventure Works\"", CommentScriptCommandCategory.Connection)]
        [DataRow("--> SET FORMAT CSV", CommentScriptCommandCategory.Connection)]
        [DataRow("--> OPEN file.dax", CommentScriptCommandCategory.Connection)]
        [DataRow("--> PARAMETER @Color = Red", CommentScriptCommandCategory.Connection)]
        [DataRow("--> CLEARCACHE", CommentScriptCommandCategory.Diagnostics)]
        [DataRow("--> TRACE QUERYPLAN", CommentScriptCommandCategory.Diagnostics)]
        [DataRow("--> GO DELAY 1s", CommentScriptCommandCategory.BatchSeparator)]
        [DataRow("--> RESULTS GRID", CommentScriptCommandCategory.Results)]
        [DataRow("--> SHOW TIMING", CommentScriptCommandCategory.Results)]
        [DataRow("--> SAVEAS results.csv", CommentScriptCommandCategory.Results)]
        [DataRow("--> OUTPUT CSV", CommentScriptCommandCategory.Results)]
        [DataRow("--> METRICS", CommentScriptCommandCategory.Results)]
        [DataRow("--> EXPORT METRICS output.json", CommentScriptCommandCategory.Results)]
        [DataRow("--> ASSERT DURATION <= PREVIOUS", CommentScriptCommandCategory.Validation)]
        public void TryClassify_MapsRecognizedCommands(string line, CommentScriptCommandCategory expected)
        {
            Assert.IsTrue(CommentScriptCommandStyle.TryClassify(line, out var style));
            Assert.AreEqual(expected, style.Category);
        }

        [DataTestMethod]
        [DataRow("-- regular comment")]
        [DataRow("--> UNKNOWN value")]
        [DataRow("-->> | expected | row |")]
        [DataRow("DEFINE")]
        [DataRow("")]
        public void TryClassify_IgnoresNonCommandLines(string line)
        {
            Assert.IsFalse(CommentScriptCommandStyle.TryClassify(line, out _));
        }

        [TestMethod]
        public void Go_UsesStandardIndicatorWidthAndBatchSeparatorTreatment()
        {
            Assert.IsTrue(CommentScriptCommandStyle.TryClassify("--> GO", out var go));
            Assert.IsTrue(CommentScriptCommandStyle.TryClassify("--> ASSERT TABLE PREVIOUS", out var assertion));

            Assert.IsTrue(go.IsBatchSeparator);
            Assert.AreEqual(assertion.IndicatorWidth, go.IndicatorWidth);
            Assert.AreEqual(4.0, go.IndicatorWidth);
            Assert.AreEqual("CsBatchSeparatorCommand", go.CommandColorName);
            Assert.AreEqual("CsBatchSeparatorRowBackground", go.RowBackgroundColorName);
        }

        [TestMethod]
        public void DaxHighlighting_ContainsEveryCommandCategoryColor()
        {
            var definition = LoadDaxHighlighting();

            foreach (CommentScriptCommandCategory category in
                System.Enum.GetValues(typeof(CommentScriptCommandCategory)))
            {
                Assert.IsNotNull(definition.GetNamedColor($"Cs{category}Command"));
                Assert.IsNotNull(definition.GetNamedColor($"Cs{category}RowBackground"));
                Assert.IsNotNull(definition.GetNamedColor($"Light.Cs{category}Command"));
                Assert.IsNotNull(definition.GetNamedColor($"Dark.Cs{category}Command"));
                Assert.IsNotNull(definition.GetNamedColor($"HighContrast.Cs{category}Command"));
            }
        }

        [TestMethod]
        public void DaxHighlighting_ColorsOnlyLeadingCommandToken()
        {
            const string line = "--> ASSERT DURATION <= BASELINE";
            var document = new TextDocument(line);
            using var highlighter = new DocumentHighlighter(document, LoadDaxHighlighting());
            var highlightedLine = highlighter.HighlightLine(1);

            Assert.AreEqual("CsValidationCommand", GetColorName(highlightedLine, line.IndexOf("ASSERT")));
            Assert.AreEqual("CsText", GetColorName(highlightedLine, line.IndexOf("BASELINE")));
        }

        private static string GetColorName(HighlightedLine line, int offset)
        {
            HighlightedSection match = null;
            foreach (var section in line.Sections)
            {
                if (section.Offset <= offset
                    && offset < section.Offset + section.Length
                    && (match == null || section.Length < match.Length))
                {
                    match = section;
                }
            }

            return match?.Color.Name;
        }

        private static IHighlightingDefinition LoadDaxHighlighting()
        {
            var assembly = typeof(DAXEditorControl.DAXEditor).Assembly;
            using var stream = assembly.GetManifestResourceStream("DAXEditor.Resources.DAX.xshd");
            Assert.IsNotNull(stream);
            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
    }
}
