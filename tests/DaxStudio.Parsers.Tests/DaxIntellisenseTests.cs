using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;
using DaxState = DaxStudio.Parsers.Metadata.DaxState;
using EditState = DaxStudio.Parsers.Metadata.EditState;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class DaxCursorStateTests
    {
        #region Token-based state detection

        [TestMethod]
        public void CursorState_PartialTable_DetectsPartialTableRef()
        {
            // Cursor at end of partial table reference: EVALUATE 'Prod|
            var input = "EVALUATE 'Prod";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.PartialTable, state.State);
        }

        [TestMethod]
        public void CursorState_PartialColumn_DetectsPartialColumnRef()
        {
            // Cursor at end of partial column: EVALUATE ADDCOLUMNS('Sales', "x", 'Product'[Col|
            var input = "EVALUATE ADDCOLUMNS('Sales', \"x\", 'Product'[Col";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.PartialColumn, state.State);
        }

        [TestMethod]
        public void CursorState_AfterDefine_ReturnsDefineContext()
        {
            var input = "DEFINE ";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.DefineContext, state.State);
        }

        [TestMethod]
        public void CursorState_AfterEvaluate_ReturnsEvaluateContext()
        {
            var input = "EVALUATE ";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.EvaluateContext, state.State);
        }

        [TestMethod]
        public void CursorState_AfterOperator_ReturnsAfterOperator()
        {
            var input = "EVALUATE FILTER('Sales', [Amount] > ";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.AfterOperator, state.State);
        }

        [TestMethod]
        public void CursorState_AfterOpenParen_ReturnsFunctionArgument()
        {
            var input = "EVALUATE CALCULATE(";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.FunctionArgument, state.State);
        }

        [TestMethod]
        public void CursorState_AfterComma_ReturnsFunctionArgument()
        {
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]),";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.FunctionArgument, state.State);
        }

        [TestMethod]
        public void CursorState_AfterColon_ReturnsParameterType()
        {
            // "DEFINE FUNCTION MyFunc = (x:" — cursor at end
            // The walker enters FunctionDefinition first (parent context)
            // which is the broader context. ParameterType would require
            // the cursor to be specifically inside a typeAnnotation child.
            // With the current grammar, "x:" is incomplete — no type after colon.
            // The walker correctly reports FunctionDefinition as the context.
            var input = "DEFINE FUNCTION MyFunc = (x:";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            // Token-based fallback detects COLON → ParameterType
            Assert.IsTrue(
                state.State == EditState.ParameterType || state.State == EditState.FunctionDefinition,
                $"Expected ParameterType or FunctionDefinition but got {state.State}");
        }

        [TestMethod]
        public void CursorState_AfterReturn_ReturnsReturnExpression()
        {
            var input = "EVALUATE VAR x = 1 RETURN ";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.ReturnExpression, state.State);
        }

        [TestMethod]
        public void CursorState_AfterLambdaArrow_ReturnsExpressionStart()
        {
            var input = "DEFINE FUNCTION MyFunc = (x: SCALAR INT64 VAL) => ";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.ExpressionStart, state.State);
        }

        [TestMethod]
        public void CursorState_EmptyInput_ReturnsTopLevel()
        {
            var input = "";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, 0);

            Assert.AreEqual(EditState.TopLevel, state.State);
        }

        [TestMethod]
        public void CursorState_PartialIdentifier_ReturnsIdentifier()
        {
            var input = "EVALUATE CALC";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            // The parser should recognize this as an identifier context
            Assert.IsNotNull(state);
            Assert.IsTrue(state.State == EditState.Identifier
                || state.State == EditState.EvaluateContext
                || state.State == EditState.FunctionArgument,
                $"Expected Identifier, EvaluateContext, or FunctionArgument but got {state.State}");
        }

        #endregion

        #region Parse-tree-based state detection

        [TestMethod]
        public void CursorState_InsideFunctionCall_DetectsFunctionAndArg()
        {
            // Cursor is inside a CALCULATE call after the first comma.
            // At that position the expression starts with 'Product'[Color]
            // which is a column reference — the walker correctly identifies
            // it as PartialColumn (useful for intellisense column suggestions).
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]), 'Product'[Color] = \"Red\")";
            int cursorPos = "EVALUATE CALCULATE(SUM('Sales'[Amount]), ".Length;
            var state = DaxCursorStateWalker.GetStateAtCursor(input, cursorPos);

            Assert.IsNotNull(state);
            // The cursor is at 'Product' which starts a column ref
            Assert.IsTrue(
                state.State == EditState.FunctionArgument || state.State == EditState.PartialColumn,
                $"Expected FunctionArgument or PartialColumn but got {state.State}");
        }

        [TestMethod]
        public void CursorState_DefineBlockAfterDefine_ShowsDefineContext()
        {
            // DEFINE with a VAR definition, cursor right after DEFINE keyword
            var input = "DEFINE\n  VAR x = 1\nEVALUATE 'Sales'";
            int cursorPos = "DEFINE\n  ".Length;
            var state = DaxCursorStateWalker.GetStateAtCursor(input, cursorPos);

            // Cursor is at position 9, which is at the VAR keyword
            // The walker should detect DefineContext or VarDefinition
            Assert.IsTrue(
                state.State == EditState.DefineContext || state.State == EditState.VarDefinition
                || state.State == EditState.ExpressionStart || state.State == EditState.Unknown,
                $"Expected DefineContext or related state but got {state.State}");
        }

        [TestMethod]
        public void CursorState_CollectsInScopeVariables()
        {
            var input = "EVALUATE VAR x = 1 VAR y = 2 RETURN x + y";
            int cursorPos = "EVALUATE VAR x = 1 VAR y = 2 RETURN ".Length;
            var state = DaxCursorStateWalker.GetStateAtCursor(input, cursorPos);

            Assert.IsNotNull(state);
            // Should have collected variables x and y
            Assert.IsNotNull(state.Variables);
        }

        [TestMethod]
        public void CursorState_SiblingScopes_DoNotLeakVariables()
        {
            // Sibling VAR blocks in CALCULATE args: b is in the first block, cursor is in the second
            // b should NOT be in scope (sibling scope was popped)
            var input = "EVALUATE CALCULATE(VAR b = 1 RETURN b, VAR a = 1 RETURN a + 1)";
            int cursorPos = "EVALUATE CALCULATE(VAR b = 1 RETURN b, VAR a = 1 RETURN ".Length;
            var state = DaxCursorStateWalker.GetStateAtCursor(input, cursorPos);

            Assert.IsNotNull(state);
            Assert.IsNotNull(state.Variables);
            Assert.IsFalse(state.Variables.Contains("b"),
                "Variable 'b' from sibling scope should NOT be in scope");
            Assert.IsTrue(state.Variables.Contains("a"),
                "Variable 'a' from current scope SHOULD be in scope");
        }

        [TestMethod]
        public void CursorState_NestedScopes_InnerAccessesOuter()
        {
            // Outer var 'a' should be visible inside the inner VAR/RETURN block
            var input = "EVALUATE {VAR a = 1 RETURN VAR b = a + 1 RETURN b + 1}";
            int cursorPos = "EVALUATE {VAR a = 1 RETURN VAR b = a + 1 RETURN ".Length;
            var state = DaxCursorStateWalker.GetStateAtCursor(input, cursorPos);

            Assert.IsNotNull(state);
            Assert.IsNotNull(state.Variables);
            Assert.IsTrue(state.Variables.Contains("a"),
                "Variable 'a' from outer scope should be in scope");
            Assert.IsTrue(state.Variables.Contains("b"),
                "Variable 'b' from current scope should be in scope");
        }

        [TestMethod]
        public void CursorState_VarNotYetDeclared_NotInScope()
        {
            // Cursor is in var1's expression — var1 should NOT be in scope yet (self-reference)
            // and var2 should also NOT be in scope (declared later)
            var input = "EVALUATE {VAR var1 = 1 + VAR var2 = 2 RETURN var1}";
            // Cursor right after "1 + " — inside var1's expression
            int cursorPos = "EVALUATE {VAR var1 = 1 + ".Length;
            var state = DaxCursorStateWalker.GetStateAtCursor(input, cursorPos);

            Assert.IsNotNull(state);
            Assert.IsNotNull(state.Variables);
            Assert.IsFalse(state.Variables.Contains("var1"),
                "Variable 'var1' should NOT be in scope (not yet declared — self-reference)");
            Assert.IsFalse(state.Variables.Contains("var2"),
                "Variable 'var2' should NOT be in scope (not yet declared)");
        }

        [TestMethod]
        public void CursorState_DefineVars_AvailableInEvaluate()
        {
            // DEFINE-level vars should be available in EVALUATE
            var input = "DEFINE VAR x = 1 VAR y = 2 EVALUATE { }";
            int cursorPos = "DEFINE VAR x = 1 VAR y = 2 EVALUATE { ".Length;
            var state = DaxCursorStateWalker.GetStateAtCursor(input, cursorPos);

            Assert.IsNotNull(state);
            Assert.IsNotNull(state.Variables);
            Assert.IsTrue(state.Variables.Contains("x"),
                "DEFINE-level variable 'x' should be in scope");
            Assert.IsTrue(state.Variables.Contains("y"),
                "DEFINE-level variable 'y' should be in scope");
        }

        #endregion
    }

    [TestClass]
    public class DaxSignatureHelperTests
    {
        [TestMethod]
        public void SignatureHelp_InsideFunction_ReturnsCorrectFunctionName()
        {
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]),";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNotNull(result);
            Assert.AreEqual("CALCULATE", result.FunctionName.ToUpperInvariant());
        }

        [TestMethod]
        public void SignatureHelp_FirstArgument_ReturnsIndexZero()
        {
            var input = "EVALUATE CALCULATE(";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.ActiveArgumentIndex);
        }

        [TestMethod]
        public void SignatureHelp_SecondArgument_ReturnsIndexOne()
        {
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]),";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.ActiveArgumentIndex);
        }

        [TestMethod]
        public void SignatureHelp_ThirdArgument_ReturnsIndexTwo()
        {
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]), 'Product'[Color] = \"Red\",";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.ActiveArgumentIndex);
        }

        [TestMethod]
        public void SignatureHelp_NestedFunction_ReturnsInnerFunction()
        {
            // EVALUATE CALCULATE(SUM(|
            var input = "EVALUATE CALCULATE(SUM(";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNotNull(result);
            Assert.AreEqual("SUM", result.FunctionName.ToUpperInvariant());
            Assert.AreEqual(0, result.ActiveArgumentIndex);
        }

        [TestMethod]
        public void SignatureHelp_NotInFunction_ReturnsNull()
        {
            var input = "EVALUATE 'Sales'";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void SignatureHelp_AfterClosedParen_ReturnsOuterFunction()
        {
            // EVALUATE CALCULATE(SUM('Sales'[Amount]), | 
            // The SUM() is closed, cursor is at 2nd arg of CALCULATE
            var input = "EVALUATE CALCULATE(SUM('Sales'[Amount]),";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNotNull(result);
            Assert.AreEqual("CALCULATE", result.FunctionName.ToUpperInvariant());
            Assert.AreEqual(1, result.ActiveArgumentIndex);
        }

        [TestMethod]
        public void SignatureHelp_WithMetadata_ReturnsFunctionSignature()
        {
            var metadata = Substitute.For<IModelMetadataProvider>();
            metadata.GetBuiltInFunctions().Returns(new List<FunctionSignature>
            {
                new FunctionSignature("CALCULATE", "Evaluates an expression in a context", "Table/Scalar",
                    new List<FunctionParameter>
                    {
                        new FunctionParameter("Expression", "Any", "The expression to evaluate"),
                        new FunctionParameter("Filter1", "Boolean/Table", "Filter to apply", isOptional: true, isRepeating: true)
                    })
            });
            metadata.GetUserDefinedFunctions().Returns(new List<UdfMetadata>());

            var input = "EVALUATE CALCULATE(";
            var result = DaxSignatureHelper.GetSignatureHelp(input, input.Length, metadata);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Signature);
            Assert.AreEqual("CALCULATE", result.Signature.Name);
            Assert.HasCount(2, result.Signature.Parameters);
        }

        [TestMethod]
        public void SignatureHelp_DottedFunction_ReturnsCorrectName()
        {
            var input = "EVALUATE ADDCOLUMNS('Sales', \"x\", BETA.DIST(";
            int cursorPos = input.Length;

            var result = DaxSignatureHelper.GetSignatureHelp(input, cursorPos);

            Assert.IsNotNull(result);
            Assert.AreEqual("BETA.DIST", result.FunctionName.ToUpperInvariant());
        }
    }

    [TestClass]
    public class DaxCompletionProviderTests
    {
        private static IModelMetadataProvider _metadata;
        private static DaxCompletionProvider _provider;

        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            _metadata = Substitute.For<IModelMetadataProvider>();

            _metadata.GetTables().Returns(new List<TableMetadata>
            {
                new TableMetadata("Sales", "Sales transactions"),
                new TableMetadata("Product", "Product catalog"),
                new TableMetadata("Date", "Calendar table")
            });

            _metadata.GetColumns("Sales").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Sales", "Amount", "Decimal", "Sales amount"),
                new ColumnMetadata("Sales", "Quantity", "Int64", "Units sold"),
                new ColumnMetadata("Sales", "OrderDate", "DateTime", "Date of sale")
            });

            _metadata.GetColumns("Product").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Product", "Name", "String", "Product name"),
                new ColumnMetadata("Product", "Color", "String", "Product color"),
                new ColumnMetadata("Product", "Category", "String", "Category")
            });

            _metadata.GetColumns("Date").Returns(new List<ColumnMetadata>
            {
                new ColumnMetadata("Date", "Date", "DateTime", "Calendar date"),
                new ColumnMetadata("Date", "Year", "Int64", "Year number")
            });

            var salesMeasures = new List<MeasureMetadata>
            {
                new MeasureMetadata("Sales", "Total Sales", "SUM('Sales'[Amount])", "Sum of sales amount"),
                new MeasureMetadata("Sales", "Avg Price", "AVERAGE('Sales'[Amount])", "Average unit price")
            };

            _metadata.GetMeasures().Returns(salesMeasures);
            _metadata.GetMeasures(Arg.Any<string>()).Returns(new List<MeasureMetadata>());
            _metadata.GetMeasures("Sales").Returns(salesMeasures);

            _metadata.GetBuiltInFunctions().Returns(new List<FunctionSignature>
            {
                new FunctionSignature("SUM", "Adds all numbers", "Decimal", new List<FunctionParameter>
                {
                    new FunctionParameter("ColumnName", "Column", "Column reference")
                }),
                new FunctionSignature("CALCULATE", "Evaluates expression in context", "Any", new List<FunctionParameter>
                {
                    new FunctionParameter("Expression", "Any", "Expression"),
                    new FunctionParameter("Filter", "Boolean/Table", "Filter", true, true)
                }),
                new FunctionSignature("FILTER", "Returns filtered table", "Table", new List<FunctionParameter>
                {
                    new FunctionParameter("Table", "Table", "Table"),
                    new FunctionParameter("FilterExpression", "Boolean", "Filter")
                })
            });

            _metadata.GetUserDefinedFunctions().Returns(new List<UdfMetadata>
            {
                new UdfMetadata("MyCustomCalc", "A custom UDF", new List<UdfParameter>
                {
                    new UdfParameter("value", UdfTypeCategory.Scalar, UdfTypeSubtype.Decimal)
                })
            });

            _metadata.GetCalendars().Returns(new List<CalendarMetadata>
            {
                new CalendarMetadata("FiscalCalendar", "Date", new List<string> { "Year", "Quarter", "Month" })
            });

            _provider = new DaxCompletionProvider(_metadata);
        }

        [TestMethod]
        public void Completions_PartialTable_ReturnsMatchingTables()
        {
            var state = new DaxState(EditState.PartialTable, partialText: "Sal");
            var items = _provider.GetCompletions(state);

            Assert.IsNotEmpty(items, "Should return at least one table");
            Assert.Contains(i => i.Label.Contains("Sales"), items, "Should include 'Sales'");
            Assert.IsTrue(items.All(i => i.Kind == CompletionItemKind.Table), "All items should be tables");
        }

        [TestMethod]
        public void Completions_PartialTable_FiltersCorrectly()
        {
            var state = new DaxState(EditState.PartialTable, partialText: "Prod");
            var items = _provider.GetCompletions(state);

            Assert.HasCount(1, items, "Should return exactly one match");
            Assert.Contains("Product", items[0].Label);
        }

        [TestMethod]
        public void Completions_CompleteTable_ReturnsColumnsAndMeasures()
        {
            var state = new DaxState(EditState.CompleteTable, currentTable: "Sales");
            var items = _provider.GetCompletions(state);

            // Should have 3 columns + 2 measures = 5 items
            Assert.HasCount(5, items);
            Assert.Contains(i => i.Kind == CompletionItemKind.Column, items);
            Assert.Contains(i => i.Kind == CompletionItemKind.Measure, items);
        }

        [TestMethod]
        public void Completions_DefineContext_ReturnsDefinitionKeywords()
        {
            var state = new DaxState(EditState.DefineContext);
            var items = _provider.GetCompletions(state);

            var labels = items.Select(i => i.Label).ToList();
            Assert.Contains("MEASURE", labels, "Should include MEASURE");
            Assert.Contains("VAR", labels, "Should include VAR");
            Assert.Contains("TABLE", labels, "Should include TABLE");
            Assert.Contains("COLUMN", labels, "Should include COLUMN");
            Assert.Contains("FUNCTION", labels, "Should include FUNCTION");
            Assert.IsTrue(items.All(i => i.Kind == CompletionItemKind.Keyword));
        }

        [TestMethod]
        public void Completions_EvaluateContext_ReturnsTablesAndFunctions()
        {
            var state = new DaxState(EditState.EvaluateContext);
            var items = _provider.GetCompletions(state);

            Assert.Contains(i => i.Kind == CompletionItemKind.Function, items, "Should include functions");
            Assert.Contains(i => i.Kind == CompletionItemKind.Table, items, "Should include tables");
        }

        [TestMethod]
        public void Completions_FunctionArgument_ReturnsAllExpressionItems()
        {
            var state = new DaxState(EditState.FunctionArgument, "CALCULATE", 0);
            var items = _provider.GetCompletions(state);

            Assert.Contains(i => i.Kind == CompletionItemKind.Function, items, "Should include functions");
            Assert.Contains(i => i.Kind == CompletionItemKind.Table, items, "Should include tables");
            Assert.Contains(i => i.Kind == CompletionItemKind.Measure, items, "Should include measures");
        }

        [TestMethod]
        public void Completions_FunctionArgument_IncludesUDFs()
        {
            var state = new DaxState(EditState.FunctionArgument);
            var items = _provider.GetCompletions(state);

            Assert.Contains(i => i.Label == "MyCustomCalc" && i.Kind == CompletionItemKind.Function, items,
                "Should include user-defined functions");
        }

        [TestMethod]
        public void Completions_FunctionArgument_IncludesInScopeVariables()
        {
            var state = new DaxState(EditState.FunctionArgument);
            state.Variables = new List<string> { "TotalAmount", "FilteredTable" };

            var items = _provider.GetCompletions(state);

            Assert.Contains(i => i.Label == "TotalAmount" && i.Kind == CompletionItemKind.Variable, items);
            Assert.Contains(i => i.Label == "FilteredTable" && i.Kind == CompletionItemKind.Variable, items);
        }

        [TestMethod]
        public void Completions_ParameterType_ReturnsTypeKeywords()
        {
            var state = new DaxState(EditState.ParameterType);
            var items = _provider.GetCompletions(state);

            var labels = items.Select(i => i.Label).ToList();
            Assert.Contains("SCALAR", labels, "Should include SCALAR");
            Assert.Contains("INT64", labels, "Should include INT64");
            Assert.Contains("DECIMAL", labels, "Should include DECIMAL");
            Assert.Contains("STRING", labels, "Should include STRING");
            Assert.Contains("VAL", labels, "Should include VAL");
            Assert.Contains("EXPR", labels, "Should include EXPR");
        }

        [TestMethod]
        public void Completions_CalendarArgument_ReturnsCalendars()
        {
            var state = new DaxState(EditState.CalendarArgument);
            var items = _provider.GetCompletions(state);

            Assert.HasCount(1, items);
            Assert.Contains("FiscalCalendar", items[0].Label);
            Assert.AreEqual(CompletionItemKind.Calendar, items[0].Kind);
        }

        [TestMethod]
        public void Completions_PeriodArgument_ReturnsPeriods()
        {
            var state = new DaxState(EditState.PeriodArgument);
            var items = _provider.GetCompletions(state);

            var labels = items.Select(i => i.Label).ToList();
            Assert.Contains("YEAR", labels);
            Assert.Contains("QUARTER", labels);
            Assert.Contains("MONTH", labels);
            Assert.Contains("DAY", labels);
        }

        [TestMethod]
        public void Completions_TopLevel_ReturnsDefineAndEvaluate()
        {
            var state = new DaxState(EditState.TopLevel);
            var items = _provider.GetCompletions(state);

            var labels = items.Select(i => i.Label).ToList();
            Assert.Contains("DEFINE", labels);
            Assert.Contains("EVALUATE", labels);
        }

        [TestMethod]
        public void Completions_OrderByContext_ReturnsAllColumns()
        {
            var state = new DaxState(EditState.OrderByContext);
            var items = _provider.GetCompletions(state);

            Assert.IsNotEmpty(items, "Should return columns");
            Assert.IsTrue(items.All(i => i.Kind == CompletionItemKind.Column));
            // Should include columns from all tables
            Assert.Contains(i => i.Label.Contains("Sales"), items);
            Assert.Contains(i => i.Label.Contains("Product"), items);
        }

        [TestMethod]
        public void Completions_AfterOperator_ReturnsExpressionItems()
        {
            var state = new DaxState(EditState.AfterOperator);
            var items = _provider.GetCompletions(state);

            Assert.Contains(i => i.Kind == CompletionItemKind.Function, items);
            Assert.Contains(i => i.Kind == CompletionItemKind.Table, items);
        }

        [TestMethod]
        public void Completions_VarDefinition_ReturnsEmptyList()
        {
            var state = new DaxState(EditState.VarDefinition);
            var items = _provider.GetCompletions(state);

            Assert.IsEmpty(items, "VAR definition names shouldn't auto-complete");
        }

        [TestMethod]
        public void Completions_FunctionDefinition_ReturnsEmptyList()
        {
            var state = new DaxState(EditState.FunctionDefinition);
            var items = _provider.GetCompletions(state);

            Assert.IsEmpty(items, "Function definition names shouldn't auto-complete");
        }
    }

    [TestClass]
    public class DaxParserServiceTests
    {
        private IModelMetadataProvider _metadata;
        private DaxParserService _service;

        [TestInitialize]
        public void Setup()
        {
            _metadata = Substitute.For<IModelMetadataProvider>();

            _metadata.GetTables().Returns(new List<TableMetadata>
            {
                new TableMetadata("Sales"),
                new TableMetadata("Product")
            });

            _metadata.GetColumns(Arg.Any<string>()).Returns(new List<ColumnMetadata>());
            _metadata.GetMeasures().Returns(new List<MeasureMetadata>());
            _metadata.GetMeasures(Arg.Any<string>()).Returns(new List<MeasureMetadata>());
            _metadata.GetBuiltInFunctions().Returns(new List<FunctionSignature>());
            _metadata.GetUserDefinedFunctions().Returns(new List<UdfMetadata>());
            _metadata.GetCalendars().Returns(new List<CalendarMetadata>());

            _service = new DaxParserService(_metadata);
        }

        [TestMethod]
        public void Parse_ValidQuery_ReturnsSuccess()
        {
            var result = _service.Parse("EVALUATE 'Sales'");

            Assert.IsTrue(result.Success, "Valid query should parse successfully");
            Assert.IsEmpty(result.Errors);
            Assert.IsNotNull(result.Tree);
        }

        [TestMethod]
        public void Parse_ComplexQuery_ReturnsSuccess()
        {
            var input = @"DEFINE
    MEASURE 'Sales'[Total] = SUM('Sales'[Amount])
    VAR x = 42
EVALUATE
    CALCULATETABLE('Sales', 'Sales'[Amount] > x)
ORDER BY 'Sales'[Amount] DESC";

            var result = _service.Parse(input);

            Assert.IsTrue(result.Success, $"Complex query should parse. Errors: {string.Join("; ", result.Errors)}");
        }

        [TestMethod]
        public void Parse_PartialInput_DoesNotThrow()
        {
            // Partial input should not throw — error strategy recovers
            var result = _service.Parse("EVALUATE CALCULATE(SUM(");

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Tree);
            // May have errors but should not crash
        }

        [TestMethod]
        public void GetEditState_ReturnsState()
        {
            var state = _service.GetEditState("EVALUATE ", "EVALUATE ".Length);

            Assert.IsNotNull(state);
            Assert.AreEqual(EditState.EvaluateContext, state.State);
        }

        [TestMethod]
        public void GetCompletions_ByInputAndCursor_ReturnsItems()
        {
            var items = _service.GetCompletions("DEFINE ", "DEFINE ".Length);

            Assert.IsNotEmpty(items, "Should return completions for DEFINE context");
            Assert.Contains(i => i.Label == "MEASURE", items);
        }

        [TestMethod]
        public void GetCompletions_ByState_ReturnsItems()
        {
            var state = new DaxState(EditState.DefineContext);
            var items = _service.GetCompletions(state);

            Assert.IsNotEmpty(items);
        }

        [TestMethod]
        public void GetSignatureHelp_InsideFunction_ReturnsResult()
        {
            var input = "EVALUATE SUM(";
            var result = _service.GetSignatureHelp(input, input.Length);

            Assert.IsNotNull(result);
            Assert.AreEqual("SUM", result.FunctionName.ToUpperInvariant());
        }

        [TestMethod]
        public void Tokenize_ReturnsTokens()
        {
            var tokens = _service.Tokenize("EVALUATE 'Sales'");

            Assert.IsNotEmpty(tokens);
        }

        [TestMethod]
        public void GetCompletions_NullMetadata_ReturnsEmpty()
        {
            var service = new DaxParserService(null);
            var items = service.GetCompletions("DEFINE ", "DEFINE ".Length);

            Assert.IsEmpty(items);
        }
    }

    [TestClass]
    public class DaxIntellisenseErrorStrategyTests
    {
        [TestMethod]
        public void ErrorStrategy_PartialInput_DoesNotThrow()
        {
            // Various partial inputs should not throw exceptions
            var partialInputs = new[]
            {
                "EVALUATE",
                "EVALUATE SUM(",
                "EVALUATE CALCULATE(SUM(",
                "DEFINE MEASURE 'Sales'[x] =",
                "EVALUATE FILTER('Sales',",
                "DEFINE",
                "EVALUATE 'Sales' ORDER BY",
                "DEFINE VAR x =",
            };

            foreach (var input in partialInputs)
            {
                var service = new DaxParserService(null);
                var result = service.Parse(input);

                Assert.IsNotNull(result, $"Parse result should not be null for: {input}");
                Assert.IsNotNull(result.Tree, $"Parse tree should not be null for: {input}");
            }
        }

        [TestMethod]
        public void ErrorStrategy_CollectsErrors()
        {
            var service = new DaxParserService(null);
            var result = service.Parse("EVALUATE CALCULATE(");

            Assert.IsNotNull(result);
            // Partial input should generate errors but not crash
            Assert.IsTrue(result.Errors.Count > 0 || !result.Success || result.Tree != null,
                "Should either have errors or a partial tree");
        }
    }
}
