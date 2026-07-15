using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.CommentScript;
using DaxStudio.Parsers.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DaxState = DaxStudio.Parsers.Metadata.DaxState;
using EditState = DaxStudio.Parsers.Metadata.EditState;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    /// <summary>
    /// Integration tests: CommentScript preprocessing → DAXParser parsing → intellisense.
    /// Verifies the full pipeline works end-to-end.
    /// </summary>
    [TestClass]
    public class IntegrationTests
    {
        private IModelMetadataProvider _metadata;

        [TestInitialize]
        public void Setup()
        {
            _metadata = Substitute.For<IModelMetadataProvider>();

            _metadata.GetTables().Returns(new List<TableMetadata>
            {
                new TableMetadata("Sales", "Sales transactions table"),
                new TableMetadata("Product", "Product catalog"),
                new TableMetadata("Date", "Calendar table"),
                new TableMetadata("Customer", "Customer dimension")
            });

            _metadata.GetColumns("Sales").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Sales", "Amount", "Decimal", "Sales amount"),
                new ColumnMetadata("Sales", "Quantity", "Int64", "Units sold"),
                new ColumnMetadata("Sales", "OrderDate", "DateTime", "Date of order"),
                new ColumnMetadata("Sales", "ProductKey", "Int64", "Product FK")
            });

            _metadata.GetColumns("Product").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Product", "Name", "String", "Product name"),
                new ColumnMetadata("Product", "Color", "String", "Product color"),
                new ColumnMetadata("Product", "Category", "String", "Product category"),
                new ColumnMetadata("Product", "ProductKey", "Int64", "Product PK")
            });

            _metadata.GetColumns("Date").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Date", "Date", "DateTime", "Calendar date"),
                new ColumnMetadata("Date", "Year", "Int64", "Calendar year"),
                new ColumnMetadata("Date", "Month", "String", "Month name")
            });

            _metadata.GetColumns("Customer").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Customer", "Name", "String", "Customer name"),
                new ColumnMetadata("Customer", "City", "String", "Customer city")
            });

            _metadata.GetMeasures().Returns(new List<MeasureMetadata>
            {
                new MeasureMetadata("Sales", "Total Sales", "SUM('Sales'[Amount])", "Sum of sales"),
                new MeasureMetadata("Sales", "Total Qty", "SUM('Sales'[Quantity])", "Sum of quantity")
            });

            _metadata.GetMeasures("Sales").Returns(new List<MeasureMetadata>
            {
                new MeasureMetadata("Sales", "Total Sales", "SUM('Sales'[Amount])"),
                new MeasureMetadata("Sales", "Total Qty", "SUM('Sales'[Quantity])")
            });

            _metadata.GetMeasures(Arg.Is<string>(s => s != "Sales")).Returns(new List<MeasureMetadata>());

            _metadata.GetBuiltInFunctions().Returns(new List<FunctionSignature>
            {
                new FunctionSignature("SUM", "Adds all numbers in a column", "Decimal",
                    new List<FunctionParameter> { new FunctionParameter("ColumnName", "Column", "A column reference") }),
                new FunctionSignature("CALCULATE", "Evaluates expression in modified filter context", "Any",
                    new List<FunctionParameter>
                    {
                        new FunctionParameter("Expression", "Any", "Expression to evaluate"),
                        new FunctionParameter("Filter1", "Boolean/Table", "Optional filter", true, true)
                    }),
                new FunctionSignature("FILTER", "Returns a filtered table", "Table",
                    new List<FunctionParameter>
                    {
                        new FunctionParameter("Table", "Table", "Table to filter"),
                        new FunctionParameter("FilterExpression", "Boolean", "Filter condition")
                    }),
                new FunctionSignature("ADDCOLUMNS", "Adds calculated columns to a table", "Table",
                    new List<FunctionParameter>
                    {
                        new FunctionParameter("Table", "Table", "Base table"),
                        new FunctionParameter("Name", "String", "Column name"),
                        new FunctionParameter("Expression", "Any", "Column expression")
                    }),
                new FunctionSignature("SUMMARIZECOLUMNS", "Groups by columns with optional filters", "Table",
                    new List<FunctionParameter>
                    {
                        new FunctionParameter("GroupBy_ColumnName", "Column", "Column to group by"),
                        new FunctionParameter("FilterTable", "Table", "Optional filter table", true),
                        new FunctionParameter("Name", "String", "Measure name", true, true),
                        new FunctionParameter("Expression", "Any", "Measure expression", true, true)
                    }),
                new FunctionSignature("TOTALWTD", "Evaluates with week-to-date filter", "Any",
                    new List<FunctionParameter>
                    {
                        new FunctionParameter("Expression", "Any", "Expression to evaluate"),
                        new FunctionParameter("Dates", "Column", "Date column"),
                        new FunctionParameter("Calendar", "String", "Calendar name", true)
                    })
            });

            _metadata.GetUserDefinedFunctions().Returns(new List<UdfMetadata>
            {
                new UdfMetadata("Sales.CalculateMargin", "Calculates profit margin",
                    new List<UdfParameter>
                    {
                        new UdfParameter("revenue", UdfTypeCategory.Scalar, UdfTypeSubtype.Decimal),
                        new UdfParameter("cost", UdfTypeCategory.Scalar, UdfTypeSubtype.Decimal)
                    })
            });

            _metadata.GetCalendars().Returns(new List<CalendarMetadata>
            {
                new CalendarMetadata("FiscalCalendar", "Date", new List<string> { "Year", "Quarter", "Month" })
            });
        }

        #region CommentScript → DAX Pipeline

        [TestMethod]
        public void Pipeline_CommentScriptWithQuery_ParsesDAXCorrectly()
        {
            // Full pipeline: CommentScript preprocessing → DAX parsing
            var input = "--> CONNECT SERVER localhost\\tab19\n" +
                "--> USE \"Adventure Works\"\n" +
                "EVALUATE\n" +
                "    SUMMARIZECOLUMNS(\n" +
                "        'Product'[Category],\n" +
                "        \"Total\", SUM('Sales'[Amount])\n" +
                "    )\n";

            // Step 1: Preprocess
            var daxQuery = PreprocessScript(input);

            // Step 2: Parse DAX with DAXParser
            var service = new DaxParserService(_metadata);
            var result = service.Parse(daxQuery);

            Assert.IsTrue(result.Success, $"DAX should parse successfully. Errors: {string.Join("; ", result.Errors)}");
            Assert.IsNotNull(result.Tree);
        }

        [TestMethod]
        public void Pipeline_CommentScriptWithParameters_ExtractsAndParses()
        {
            var input = "--> PARAMETER @Color = Red\n" +
                "EVALUATE\n" +
                "    FILTER('Product', 'Product'[Color] = @Color)\n";

            var daxQuery = PreprocessScript(input);

            var service = new DaxParserService(_metadata);
            var result = service.Parse(daxQuery);

            Assert.IsNotNull(result.Tree);
            // The DAX parser handles @Color as a PARAMETER token
        }

        [TestMethod]
        public void Pipeline_CommentScriptWithUDF_ParsesCorrectly()
        {
            var input = "--> CONNECT SERVER localhost\n" +
                "DEFINE\n" +
                "    FUNCTION MyCalc = (x: SCALAR DECIMAL VAL) => x * 1.1\n" +
                "EVALUATE\n" +
                "    ADDCOLUMNS('Sales', \"Adjusted\", MyCalc('Sales'[Amount]))\n";

            var daxQuery = PreprocessScript(input);

            var service = new DaxParserService(_metadata);
            var result = service.Parse(daxQuery);

            Assert.IsTrue(result.Success, $"UDF query should parse. Errors: {string.Join("; ", result.Errors)}");
        }

        #endregion

        #region Full Intellisense Scenarios

        [TestMethod]
        public void Intellisense_TypingTableRef_GetsTableCompletions()
        {
            var service = new DaxParserService(_metadata);
            var input = "EVALUATE 'Sal";
            var completions = service.GetCompletions(input, input.Length);

            Assert.Contains(c => c.Label.Contains("Sales"), completions,
                "Should suggest 'Sales' table");
        }

        [TestMethod]
        public void Intellisense_AfterTableRef_GetsColumnsAndMeasures()
        {
            var service = new DaxParserService(_metadata);

            // After typing 'Sales'[, the partial column token triggers completion
            var input = "EVALUATE ADDCOLUMNS('Sales', \"x\", 'Sales'[";
            var state = service.GetEditState(input, input.Length);

            // Should detect partial column context
            Assert.IsTrue(
                state.State == EditState.PartialColumn || state.State == EditState.FunctionArgument,
                $"Expected PartialColumn or FunctionArgument but got {state.State}");

            var completions = service.GetCompletions(state);
            Assert.IsNotEmpty(completions,
                $"Should return completions. State={state.State}, Table={state.CurrentTable}");
        }

        [TestMethod]
        public void Intellisense_InsideCalculate_GetsSignatureHelp()
        {
            var service = new DaxParserService(_metadata);
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]),";

            var sigHelp = service.GetSignatureHelp(input, input.Length);

            Assert.IsNotNull(sigHelp);
            Assert.AreEqual("CALCULATE", sigHelp.FunctionName.ToUpperInvariant());
            Assert.AreEqual(1, sigHelp.ActiveArgumentIndex, "Should be at 2nd argument (filter)");
            Assert.IsNotNull(sigHelp.Signature);
            Assert.HasCount(2, sigHelp.Signature.Parameters);
        }

        [TestMethod]
        public void Intellisense_NestedFunctions_GetsInnerSignature()
        {
            var service = new DaxParserService(_metadata);
            var input = "EVALUATE CALCULATE(FILTER('Sales',";

            var sigHelp = service.GetSignatureHelp(input, input.Length);

            Assert.IsNotNull(sigHelp);
            Assert.AreEqual("FILTER", sigHelp.FunctionName.ToUpperInvariant());
            Assert.AreEqual(1, sigHelp.ActiveArgumentIndex, "Should be at 2nd argument of FILTER");
        }

        [TestMethod]
        public void Intellisense_DefineBlock_SuggestsKeywords()
        {
            var service = new DaxParserService(_metadata);
            var input = "DEFINE ";

            var completions = service.GetCompletions(input, input.Length);

            var labels = completions.Select(c => c.Label).ToList();
            Assert.Contains("MEASURE", labels);
            Assert.Contains("VAR", labels);
            Assert.Contains("FUNCTION", labels);
        }

        [TestMethod]
        public void Intellisense_EvaluateContext_SuggestsTablesAndFunctions()
        {
            var service = new DaxParserService(_metadata);
            var input = "EVALUATE ";

            var completions = service.GetCompletions(input, input.Length);

            Assert.Contains(c => c.Kind == CompletionItemKind.Table, completions);
            Assert.Contains(c => c.Kind == CompletionItemKind.Function, completions);
        }

        [TestMethod]
        public void Intellisense_ComplexQuery_CompletionsWork()
        {
            var service = new DaxParserService(_metadata);

            // Multi-line query with DEFINE and variables
            var input = "DEFINE\n" +
                "    VAR SalesAmt = SUM('Sales'[Amount])\n" +
                "EVALUATE\n" +
                "    ADDCOLUMNS('Product', \"Sales\", ";

            var completions = service.GetCompletions(input, input.Length);

            // Should include functions, tables, measures and potentially variables
            Assert.IsNotEmpty(completions, "Should return completions in complex context");
        }

        [TestMethod]
        public void Intellisense_UdfSignatureHelp_ReturnsUdfParams()
        {
            var service = new DaxParserService(_metadata);
            var input = "EVALUATE { Sales.CalculateMargin(";

            var sigHelp = service.GetSignatureHelp(input, input.Length);

            Assert.IsNotNull(sigHelp);
            // The function name should be found
            Assert.Contains("CALCULATEMARGIN", sigHelp.FunctionName.ToUpperInvariant(),
                $"Expected CalculateMargin but got {sigHelp.FunctionName}");
        }

        [TestMethod]
        public void Intellisense_EmptyInput_SuggestsDefineOrEvaluate()
        {
            var service = new DaxParserService(_metadata);
            var input = "";

            var completions = service.GetCompletions(input, 0);

            Assert.Contains(c => c.Label == "DEFINE" || c.Label == "EVALUATE", completions,
                "Empty input should suggest DEFINE or EVALUATE");
        }

        [TestMethod]
        public void Intellisense_IncompleteMeasureDefinition_DoesNotThrow()
        {
            var service = new DaxParserService(_metadata);
            // Incomplete input like this puts the ANTLR parser into error recovery, which previously
            // produced an EvaluateBlock/DefineBlock context with a missing keyword token and threw a
            // NullReferenceException in DaxCursorStateWalker.
            var input = "DEFINE MEASURE [te";

            var completions = service.GetCompletions(input, input.Length);

            Assert.IsNotNull(completions);
        }

        [TestMethod]
        public void Intellisense_DefinedFunction_AppearsInCompletions()
        {
            var service = new DaxParserService(_metadata);
            var input = "DEFINE FUNCTION hello = (a) => \"hello \" & a\r\nEVALUATE { he";

            var completions = service.GetCompletions(input, input.Length);

            Assert.Contains(c => c.Label == "hello", completions,
                "A DEFINE FUNCTION name should be offered as a completion");
        }

        [TestMethod]
        public void Intellisense_GetDefinedFunctions_ReturnsNameAndParameters()
        {
            var service = new DaxParserService(_metadata);
            var input = "DEFINE FUNCTION hello = (a, b) => \"hello \" & a & b\r\nEVALUATE { hello(1, 2) }";

            var functions = service.GetDefinedFunctions(input);

            var fn = functions.FirstOrDefault(f => f.Name == "hello");
            Assert.IsNotNull(fn, "Should find the DEFINE FUNCTION named 'hello'");
            CollectionAssert.AreEqual(new[] { "a", "b" }, fn.Parameters.Select(p => p.Name).ToArray());
        }

        [TestMethod]
        public void Intellisense_Parse_CollectsErrors()
        {
            var service = new DaxParserService(_metadata);

            // Invalid syntax
            var result = service.Parse("EVALUATE CALCULATE(,)");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Tree, "Should still produce a parse tree");
        }

        [TestMethod]
        public void Intellisense_Tokenize_ReturnsAllTokenTypes()
        {
            var service = new DaxParserService(_metadata);
            var tokens = service.Tokenize("EVALUATE CALCULATE(SUM('Sales'[Amount]), 'Product'[Color] = \"Red\")");

            Assert.IsGreaterThan(5, tokens.Count, "Should tokenize into multiple tokens");
            Assert.Contains(t => t.Type == DAXLexer.EVALUATE, tokens);
            Assert.Contains(t => t.Type == DAXLexer.CALCULATE, tokens);
            Assert.Contains(t => t.Type == DAXLexer.SUM, tokens);
            Assert.Contains(t => t.Type == DAXLexer.TABLE_REF, tokens);
            Assert.Contains(t => t.Type == DAXLexer.COLUMN_OR_MEASURE, tokens);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Runs the CommentScript preprocessor and returns the extracted DAX query text.
        /// </summary>
        private string PreprocessScript(string input)
        {
            ICharStream chars = new DAXCharStream(input);
            var lexer = new PreProcessorLexer(chars);
            ITokenStream stream = new BufferedTokenStream(lexer);
            var parser = new PreProcessorParser(stream);

            var tree = parser.document();

            var arrayParameters = new Dictionary<string, List<string>>();
            var batch = new List<ScriptBatch>();
            var listener = new PreProcessorListener(arrayParameters, batch);
            var walker = new ParseTreeWalker();
            walker.Walk(listener, tree);

            // The first batch's output is the accumulated DAX text
            return batch.Count > 0 ? batch[0].Output.ToString() : "";
        }

        #endregion
    }
}
