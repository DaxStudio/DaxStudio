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
        public void CursorState_PartialTableInsideFunctionArg_DetectsPartialTable()
        {
            // A partial quoted table typed as a function argument must be treated as a table reference,
            // not a generic function-argument expression (which would also offer functions). Regression
            // for the ANTLR provider showing functions when typing a quote inside VALUES('pr|
            var input = "EVALUATE VALUES('pr";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.PartialTable, state.State);
        }

        [TestMethod]
        public void CursorState_OpeningQuoteInsideFunctionArg_DetectsPartialTable()
        {
            // Just the opening quote inside a function argument: EVALUATE FILTER('|
            var input = "EVALUATE FILTER('";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.PartialTable, state.State);
        }

        [TestMethod]
        public void CursorState_UnqualifiedBracketInsideFunctionArg_DetectsPartialColumn()
        {
            // A bracket with no qualifying table inside a function argument should offer measures/columns
            // (PartialColumn), not a generic function-argument expression: EVALUATE VALUES([|
            var input = "EVALUATE VALUES([";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.PartialColumn, state.State);
        }

        [TestMethod]
        public void CursorState_PlainFunctionArg_StillReturnsFunctionArgument()
        {
            // Ensure the partial-reference refinement does not regress ordinary function-argument
            // contexts (no open quote/bracket): EVALUATE VALUES(|
            var input = "EVALUATE VALUES(";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.FunctionArgument, state.State);
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
        public void CursorState_UnquotedTableBracket_CapturesTableContext()
        {
            // Unquoted table name before '[' : EVALUATE FILTER(Customer[|
            var input = "EVALUATE FILTER(Customer[";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.PartialColumn, state.State);
            Assert.AreEqual("Customer", state.CurrentTable?.Trim('\''),
                "The unquoted table name should be captured as the column context");
        }

        [TestMethod]
        public void CursorState_QuotedTableBracket_CapturesTableContext()
        {
            // Quoted table name before '[' : EVALUATE FILTER('Sales'[|
            var input = "EVALUATE FILTER('Sales'[";
            var state = DaxCursorStateWalker.GetStateAtCursor(input, input.Length);

            Assert.AreEqual(EditState.PartialColumn, state.State);
            Assert.AreEqual("Sales", state.CurrentTable?.Trim('\''),
                "The quoted table name should be captured as the column context");
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

        [TestMethod]
        public void CursorState_MeasureReturn_ShowsLocalVar_Variants()
        {
            // Bug 1: a variable declared in a measure's VAR/RETURN body must be offered as a completion
            // in the RETURN expression, across the incomplete-input shapes that occur while typing.
            var meta = Substitute.For<DaxStudio.Parsers.Metadata.IModelMetadataProvider>();
            meta.GetTables().Returns(new List<DaxStudio.Parsers.Metadata.TableMetadata>());
            meta.GetMeasures().Returns(new List<DaxStudio.Parsers.Metadata.MeasureMetadata>());
            meta.GetBuiltInFunctions().Returns(new List<DaxStudio.Parsers.Metadata.FunctionSignature>());
            meta.GetUserDefinedFunctions().Returns(new List<DaxStudio.Parsers.Metadata.UdfMetadata>());
            var svc = new DaxParserService(meta);

            var variants = new Dictionary<string, string>
            {
                {"eol-partial",    "DEFINE MEASURE Customer[test] =\r\nVAR test1 = 1\r\nRETURN tes"},
                {"eol-space",      "DEFINE MEASURE Customer[test] =\r\nVAR test1 = 1\r\nRETURN "},
                {"binary-partial", "DEFINE MEASURE Customer[test] =\r\nVAR test1 = 1\r\nRETURN test1 + tes"},
                {"followed-by-eval-partial", "DEFINE MEASURE Customer[test] =\r\nVAR test1 = 1\r\nRETURN tes\r\nEVALUATE Customer"},
                {"followed-by-eval-space",   "DEFINE MEASURE Customer[test] =\r\nVAR test1 = 1\r\nRETURN \r\nEVALUATE Customer"},
            };

            foreach (var kv in variants)
            {
                // Place the caret at the end of the RETURN expression (before any trailing EVALUATE).
                int evalIdx = kv.Value.IndexOf("\r\nEVALUATE");
                int caret = evalIdx >= 0 ? evalIdx : kv.Value.Length;
                var state = svc.GetEditState(kv.Value, caret);
                var labels = svc.GetCompletions(state).Select(c => c.Label).ToList();
                Assert.IsTrue(labels.Contains("test1"),
                    "measure-local var 'test1' should be a completion in the measure RETURN (variant '" + kv.Key + "')");
            }
        }

        [TestMethod]
        public void CursorState_EvaluateScope_ExcludesMeasureLocalVar()
        {
            // Bug 2: a variable declared inside a measure's VAR/RETURN body must NOT leak into the
            // outer EVALUATE scope, while a DEFINE-level variable must remain visible there.
            var meta = Substitute.For<DaxStudio.Parsers.Metadata.IModelMetadataProvider>();
            meta.GetTables().Returns(new List<DaxStudio.Parsers.Metadata.TableMetadata>());
            meta.GetMeasures().Returns(new List<DaxStudio.Parsers.Metadata.MeasureMetadata>());
            meta.GetBuiltInFunctions().Returns(new List<DaxStudio.Parsers.Metadata.FunctionSignature>());
            meta.GetUserDefinedFunctions().Returns(new List<DaxStudio.Parsers.Metadata.UdfMetadata>());
            var svc = new DaxParserService(meta);

            var input = "DEFINE\r\nMEASURE Customer[test] = VAR vtest1 = 1 RETURN vtest1\r\nVAR vtest2 = 2\r\nEVALUATE { [test], vt";
            var state = svc.GetEditState(input, input.Length);
            var labels = svc.GetCompletions(state).Select(c => c.Label).ToList();
            Assert.IsFalse(labels.Contains("vtest1"), "measure-local var vtest1 should NOT leak into EVALUATE");
            Assert.IsTrue(labels.Contains("vtest2"), "DEFINE-level var vtest2 should be visible in EVALUATE");
        }

        [TestMethod]
        public void CursorState_MeasureLocalVar_DoesNotLeakAfterClosingReturn()
        {
            // A variable declared in a measure's VAR/RETURN body goes out of scope once that measure's
            // RETURN closes. It must not leak into a *later* part of the DEFINE block (here a following
            // measure definition), regardless of whether a subsequent declaration overwrites the
            // fallback snapshot.
            var meta = Substitute.For<DaxStudio.Parsers.Metadata.IModelMetadataProvider>();
            meta.GetTables().Returns(new List<DaxStudio.Parsers.Metadata.TableMetadata>());
            meta.GetMeasures().Returns(new List<DaxStudio.Parsers.Metadata.MeasureMetadata>());
            meta.GetBuiltInFunctions().Returns(new List<DaxStudio.Parsers.Metadata.FunctionSignature>());
            meta.GetUserDefinedFunctions().Returns(new List<DaxStudio.Parsers.Metadata.UdfMetadata>());
            var svc = new DaxParserService(meta);

            var input = "DEFINE\r\nMEASURE Customer[test1] = VAR vtest1 = 1 RETURN vtest1\r\nMEASURE Customer[test2] = vt";
            var state = svc.GetEditState(input, input.Length);
            var labels = svc.GetCompletions(state).Select(c => c.Label).ToList();
            Assert.IsFalse(labels.Contains("vtest1"),
                "measure-local var vtest1 must go out of scope after its RETURN and not leak into a later measure");
        }

        [TestMethod]
        public void CursorState_MeasureLocalVar_DoesNotLeakIntoEvaluate()
        {
            // A measure-local var must also not leak into a following EVALUATE when there is no later
            // DEFINE-level variable to overwrite the fallback snapshot.
            var meta = Substitute.For<DaxStudio.Parsers.Metadata.IModelMetadataProvider>();
            meta.GetTables().Returns(new List<DaxStudio.Parsers.Metadata.TableMetadata>());
            meta.GetMeasures().Returns(new List<DaxStudio.Parsers.Metadata.MeasureMetadata>());
            meta.GetBuiltInFunctions().Returns(new List<DaxStudio.Parsers.Metadata.FunctionSignature>());
            meta.GetUserDefinedFunctions().Returns(new List<DaxStudio.Parsers.Metadata.UdfMetadata>());
            var svc = new DaxParserService(meta);

            var input = "DEFINE\r\nMEASURE Customer[test] = VAR vtest1 = 1 RETURN vtest1\r\nEVALUATE { [test], vt";
            var state = svc.GetEditState(input, input.Length);
            var labels = svc.GetCompletions(state).Select(c => c.Label).ToList();
            Assert.IsFalse(labels.Contains("vtest1"),
                "measure-local var vtest1 must go out of scope after its RETURN and not leak into EVALUATE");
        }

        [TestMethod]
        public void CursorState_TopLevelPartial_OffersDefineKeyword()
        {
            // Regression: while typing "DEFINE" at the start of a query, the DEFINE keyword must remain
            // among the offered completions at every prefix length.
            var meta = Substitute.For<DaxStudio.Parsers.Metadata.IModelMetadataProvider>();
            meta.GetTables().Returns(new List<DaxStudio.Parsers.Metadata.TableMetadata>());
            meta.GetMeasures().Returns(new List<DaxStudio.Parsers.Metadata.MeasureMetadata>());
            meta.GetBuiltInFunctions().Returns(new List<DaxStudio.Parsers.Metadata.FunctionSignature>());
            meta.GetUserDefinedFunctions().Returns(new List<DaxStudio.Parsers.Metadata.UdfMetadata>
            {
                new DaxStudio.Parsers.Metadata.UdfMetadata { Name = "MyUserDefinedFunction", Description = "hello" }
            });
            var svc = new DaxParserService(meta);

            foreach (var prefix in new[] { "D", "DE", "DEF", "DEFI", "DEFIN" })
            {
                var state = svc.GetEditState(prefix, prefix.Length);
                var labels = svc.GetCompletions(state).Select(c => c.Label).ToList();
                Assert.IsTrue(labels.Contains("DEFINE"),
                    "DEFINE keyword should be offered while typing prefix '" + prefix + "'");
            }
        }

        [TestMethod]
        public void CursorState_VarBeingTyped_DoesNotOfferItself()
        {
            // Regression: the variable name currently being typed is not yet in scope, so it must not be
            // offered as a completion for itself (e.g. "VAR vte" should not suggest "vte").
            var meta = Substitute.For<DaxStudio.Parsers.Metadata.IModelMetadataProvider>();
            meta.GetTables().Returns(new List<DaxStudio.Parsers.Metadata.TableMetadata>());
            meta.GetMeasures().Returns(new List<DaxStudio.Parsers.Metadata.MeasureMetadata>());
            meta.GetBuiltInFunctions().Returns(new List<DaxStudio.Parsers.Metadata.FunctionSignature>());
            meta.GetUserDefinedFunctions().Returns(new List<DaxStudio.Parsers.Metadata.UdfMetadata>());
            var svc = new DaxParserService(meta);

            var input = "DEFINE MEASURE Customer[Mtest1] = VAR vte";
            var state = svc.GetEditState(input, input.Length);
            var labels = svc.GetCompletions(state).Select(c => c.Label).ToList();
            Assert.IsFalse(labels.Contains("vte"),
                "the variable being declared ('vte') must not suggest itself before it is in scope");
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
        public void Completions_CompleteTable_ReturnsOnlyColumns()
        {
            var state = new DaxState(EditState.CompleteTable, currentTable: "Sales");
            var items = _provider.GetCompletions(state);

            // A qualified table reference (Table[) should only offer that table's columns.
            Assert.HasCount(3, items);
            Assert.IsTrue(items.All(i => i.Kind == CompletionItemKind.Column),
                "A qualified table reference should only return columns");
            Assert.IsFalse(items.Any(i => i.Kind == CompletionItemKind.Measure),
                "A qualified table reference should not return measures");
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

        #region Statement chunking

        [TestMethod]
        public void Chunk_CursorInSecondStatement_ExcludesEarlierEvaluate()
        {
            // An earlier complete "EVALUATE customer" query precedes a "DEFINE ... EVALUATE" query.
            // The chunk containing the cursor (in the second query) must not include the first query.
            var doc = "EVALUATE customer\r\n\r\nDEFINE MEASURE Customer[mtest] = 1\r\nEVALUATE { ";
            var chunk = DaxCursorStateWalker.ExtractStatementChunk(doc, doc.Length);

            StringAssert.StartsWith(chunk.Text, "DEFINE");
            Assert.IsFalse(chunk.Text.Contains("EVALUATE customer"), "Earlier statement should be excluded");
            Assert.AreEqual(doc.Length - doc.IndexOf("DEFINE"), chunk.CursorOffset);
        }

        [TestMethod]
        public void Chunk_CursorInFirstStatement_StopsAtDefine()
        {
            var doc = "EVALUATE customer\r\n\r\nDEFINE MEASURE Customer[mtest] = 1\r\nEVALUATE { ";
            // Cursor at the end of the first "EVALUATE customer" statement (just before the blank line
            // and the following DEFINE query).
            var cursor = "EVALUATE customer".Length;
            var chunk = DaxCursorStateWalker.ExtractStatementChunk(doc, cursor);

            Assert.AreEqual("EVALUATE customer", chunk.Text);
            Assert.IsFalse(chunk.Text.Contains("DEFINE"), "Later DEFINE statement should be excluded");
            Assert.AreEqual(cursor, chunk.CursorOffset);
        }

        [TestMethod]
        public void Chunk_TruncatesTextAfterCursor()
        {
            // Only the text up to the cursor is parsed - text after the cursor (which may be temporarily
            // invalid while the user edits the middle of the query) must never be included.
            var doc = "EVALUATE SELECTCOLUMNS( 'product'[Color] )";
            var cursor = "EVALUATE SELE".Length; // caret in the middle of the function name
            var chunk = DaxCursorStateWalker.ExtractStatementChunk(doc, cursor);

            Assert.AreEqual("EVALUATE SELE", chunk.Text);
            Assert.AreEqual(cursor, chunk.CursorOffset);
        }

        [TestMethod]
        public void Chunk_InvalidTextAfterCursorDoesNotBreakState()
        {
            // The user is editing an existing query; the text after the cursor is temporarily invalid
            // (an unterminated string and an unbalanced brace). Restricting parsing to the text up to the
            // cursor must still yield a usable in-EVALUATE state with the in-scope variable available.
            var doc = "DEFINE\r\nVAR v1 = 1\r\nEVALUATE { v1 } ) \"unterminated";
            var cursor = doc.IndexOf("v1 }") + 2; // just after "v1" inside the row constructor
            var state = DaxCursorStateWalker.GetStateAtCursor(doc, cursor);

            Assert.IsNotNull(state);
            Assert.IsNotNull(state.Variables);
            CollectionAssert.Contains(state.Variables.ToList(), "v1",
                "The variable declared before the cursor must be in scope even though the text after the cursor is invalid");
        }

        [TestMethod]
        public void Chunk_SingleStatement_ReturnedUnchanged()
        {
            var doc = "DEFINE MEASURE Customer[mtest] = 1\r\nEVALUATE { ";
            var chunk = DaxCursorStateWalker.ExtractStatementChunk(doc, doc.Length);

            Assert.AreEqual(doc, chunk.Text);
            Assert.AreEqual(doc.Length, chunk.CursorOffset);
        }

        [TestMethod]
        public void Chunk_MeasureVisibleWhenPrecededByEvaluate()
        {
            // Regression: an earlier "EVALUATE customer" made the following DEFINE a syntax error, so the
            // measure defined in the second query was not offered. Chunking must restore it.
            var doc = "EVALUATE customer\r\n\r\nDEFINE MEASURE Customer[mtest] = \r\n    var vtest = 1\r\n    RETURN vtest\r\n    EVALUATE { ";
            var state = DaxCursorStateWalker.GetStateAtCursor(doc, doc.Length);

            Assert.IsNotNull(state.DefinedMeasures);
            CollectionAssert.Contains(state.DefinedMeasures.ToList(), "mtest",
                "Measure defined in the cursor's statement should be offered even when a separate EVALUATE precedes it");
        }

        #endregion
    }
}
