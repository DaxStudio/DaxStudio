using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using DaxState = DaxStudio.Parsers.Metadata.DaxState;
using EditState = DaxStudio.Parsers.Metadata.EditState;

namespace DaxStudio.Parsers.Tests
{
    /// <summary>
    /// Unit tests for <see cref="DaxCompletionProvider"/> covering keyword completions, table-name
    /// quoting and the "only columns for a qualified table reference" behaviour.
    /// </summary>
    [TestClass]
    public class DaxCompletionProviderQuotingTests
    {
        private IModelMetadataProvider _metadata;
        private DaxCompletionProvider _provider;

        [TestInitialize]
        public void Setup()
        {
            _metadata = Substitute.For<IModelMetadataProvider>();

            _metadata.GetTables().Returns(new List<TableMetadata>
            {
                new TableMetadata("Sales", "Sales table"),                              // no quotes needed
                new TableMetadata("Product Category", "Has a space", false, null),      // needs quotes (space)
                new TableMetadata("1Fact", "Starts with a digit", false, null),         // needs quotes (digit start)
                new TableMetadata("Date", "Reserved word", false, "'Date'")             // pre-quoted DaxName
            });

            _metadata.GetColumns("Sales").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Sales", "Amount", "Decimal", "Sales amount"),
                new ColumnMetadata("Sales", "Quantity", "Int64", "Units sold")
            });
            _metadata.GetColumns(Arg.Is<string>(s => s != "Sales")).Returns(new List<ColumnMetadata>());

            _metadata.GetMeasures().Returns(new List<MeasureMetadata>
            {
                new MeasureMetadata("Sales", "Total Sales", "SUM('Sales'[Amount])", "Sum of sales")
            });
            _metadata.GetMeasures(Arg.Any<string>()).Returns(new List<MeasureMetadata>
            {
                new MeasureMetadata("Sales", "Total Sales", "SUM('Sales'[Amount])", "Sum of sales")
            });

            _metadata.GetBuiltInFunctions().Returns(new List<FunctionSignature>
            {
                new FunctionSignature("SUM", "Sum", "Decimal", new List<FunctionParameter>())
            });
            _metadata.GetUserDefinedFunctions().Returns(new List<UdfMetadata>());

            _provider = new DaxCompletionProvider(_metadata);
        }

        [TestMethod]
        public void ExpressionContext_IncludesQueryKeywords()
        {
            var state = new DaxState(EditState.ExpressionStart);
            var labels = _provider.GetCompletions(state).Select(c => c.Label).ToList();

            Assert.Contains("DEFINE", labels);
            Assert.Contains("EVALUATE", labels);
            Assert.Contains("ORDER BY", labels);
        }

        [TestMethod]
        public void SimpleTableName_IsNotQuoted()
        {
            var state = new DaxState(EditState.ExpressionStart);
            var labels = _provider.GetCompletions(state)
                .Where(c => c.Kind == CompletionItemKind.Table)
                .Select(c => c.Label)
                .ToList();

            Assert.Contains("Sales", labels, "A simple table name should not be quoted");
            CollectionAssert.DoesNotContain(labels, "'Sales'");
        }

        [TestMethod]
        public void TableNameWithSpace_IsQuoted()
        {
            var state = new DaxState(EditState.ExpressionStart);
            var labels = _provider.GetCompletions(state)
                .Where(c => c.Kind == CompletionItemKind.Table)
                .Select(c => c.Label)
                .ToList();

            Assert.Contains("'Product Category'", labels, "A table name with a space should be quoted");
        }

        [TestMethod]
        public void TableNameStartingWithDigit_IsQuoted()
        {
            var state = new DaxState(EditState.ExpressionStart);
            var labels = _provider.GetCompletions(state)
                .Where(c => c.Kind == CompletionItemKind.Table)
                .Select(c => c.Label)
                .ToList();

            Assert.Contains("'1Fact'", labels, "A table name starting with a digit should be quoted");
        }

        [TestMethod]
        public void TableName_UsesProvidedDaxName()
        {
            var state = new DaxState(EditState.ExpressionStart);
            var labels = _provider.GetCompletions(state)
                .Where(c => c.Kind == CompletionItemKind.Table)
                .Select(c => c.Label)
                .ToList();

            Assert.Contains("'Date'", labels, "When a DaxName is supplied it should be used verbatim");
        }

        [TestMethod]
        public void QualifiedTableReference_ReturnsOnlyThatTablesColumns()
        {
            // 'Sales'[  -> only Sales columns, no measures from other contexts
            var state = new DaxState(EditState.CompleteTable, currentTable: "'Sales'");
            var completions = _provider.GetCompletions(state);

            Assert.IsTrue(completions.All(c => c.Kind == CompletionItemKind.Column),
                "A qualified table reference should only return columns");
            Assert.Contains(c => c.Label == "[Amount]", completions);
            Assert.IsFalse(completions.Any(c => c.Kind == CompletionItemKind.Measure),
                "A qualified table reference should not return measures");
        }

        [TestMethod]
        public void QualifiedPartialColumn_ReturnsOnlyThatTablesColumns()
        {
            // 'Sales'[Am  -> only Sales columns filtered, no measures
            var state = new DaxState(EditState.PartialColumn, currentTable: "'Sales'", partialText: "[Am");
            var completions = _provider.GetCompletions(state);

            Assert.IsTrue(completions.Count > 0);
            Assert.IsTrue(completions.All(c => c.Kind == CompletionItemKind.Column),
                "A qualified partial column reference should only return columns");
            Assert.IsFalse(completions.Any(c => c.Kind == CompletionItemKind.Measure));
        }

        [TestMethod]
        public void UnqualifiedColumnReference_ReturnsMeasures()
        {
            // [  (no preceding table) -> measures
            var state = new DaxState(EditState.PartialColumn, partialText: "[");
            var completions = _provider.GetCompletions(state);

            Assert.Contains(c => c.Kind == CompletionItemKind.Measure && c.Label == "[Total Sales]", completions);
        }
    }
}
