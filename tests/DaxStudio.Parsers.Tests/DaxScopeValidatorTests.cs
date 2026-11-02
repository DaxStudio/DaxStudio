using DaxStudio.Parsers.Dax;
using DaxStudio.Parsers.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Tests
{
    [TestClass]
    public class DaxScopeValidatorTests
    {
        private IModelMetadataProvider _emptyMetadata;

        [TestInitialize]
        public void Setup()
        {
            _emptyMetadata = Substitute.For<IModelMetadataProvider>();
            _emptyMetadata.GetTables().Returns(new List<TableMetadata>());
        }

        private static ParseResult Parse(string input)
        {
            var service = new DaxParserService(null);
            return service.Parse(input);
        }

        // ===================================================================
        // Valid scoping
        // ===================================================================

        [TestMethod]
        public void Scope_SequentialVars_Valid()
        {
            var input = "EVALUATE {VAR var1 = 1 VAR var2 = var1 + 1 RETURN var2}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsEmpty(errors, "Sequential vars should produce no scope errors");
        }

        [TestMethod]
        public void Scope_NestedVarReturn_InnerAccessesOuter()
        {
            var input = @"EVALUATE
{VAR a = 1
RETURN
    VAR b = a + 1
    RETURN b * a}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsEmpty(errors, "Inner scope should access outer variables");
        }

        [TestMethod]
        public void Scope_DefineVars_Sequential_Valid()
        {
            var input = @"DEFINE
    VAR x = 1
    VAR y = x + 1
EVALUATE {y}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsEmpty(errors, "DEFINE-level sequential vars should be valid");
        }

        [TestMethod]
        public void Scope_VarWithFunctionCall_Valid()
        {
            // Function calls (SUM, FILTER, etc.) should not be flagged
            var metadata = Substitute.For<IModelMetadataProvider>();
            metadata.GetTables().Returns(new List<TableMetadata>
            {
                new TableMetadata { Name = "Sales" }
            });

            var input = @"EVALUATE
{VAR total = SUM('Sales'[Amount])
VAR avg = total / COUNTROWS('Sales')
RETURN avg}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, metadata);
            Assert.IsEmpty(errors, "Function calls and table refs should not be flagged");
        }

        [TestMethod]
        public void Scope_VarNamedAfterFunction_Valid()
        {
            // VAR Offset = 5 — function names can be used as variable names
            var input = "EVALUATE {VAR Offset = 5 RETURN Offset + 1}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsEmpty(errors, "Function-named variables should be valid when declared");
        }

        [TestMethod]
        public void Scope_ParametersNotFlagged()
        {
            var input = "EVALUATE {VAR x = @param + 1 RETURN x}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsEmpty(errors, "Parameters (@param) should not be flagged");
        }

        [TestMethod]
        public void Scope_TableRefNotFlagged()
        {
            // A known table name used as an identifier should not be flagged
            var metadata = Substitute.For<IModelMetadataProvider>();
            metadata.GetTables().Returns(new List<TableMetadata>
            {
                new TableMetadata { Name = "Sales" }
            });

            var input = "EVALUATE Sales";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, metadata);
            Assert.IsEmpty(errors, "Known table names should not be flagged");
        }

        // ===================================================================
        // Forward references (within same block)
        // ===================================================================

        [TestMethod]
        public void Scope_ForwardReference_SameBlock()
        {
            // b is referenced before it is declared in the same VAR block
            var input = "EVALUATE {VAR a = b + 1 VAR b = 1 RETURN a}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Should parse syntactically");

            var errors = DaxScopeValidator.Validate(result.Tree);
            Assert.IsNotEmpty(errors);
            Assert.HasCount(1, errors);
            Assert.AreEqual("b", errors[0].Identifier);
            Assert.AreEqual(DaxScopeValidator.ScopeErrorKind.ForwardReference, errors[0].Kind);
        }

        [TestMethod]
        public void Scope_SelfReference_Flagged()
        {
            // x references itself — it's a forward reference (not yet declared)
            var input = "EVALUATE {VAR x = x + 1 RETURN x}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Should parse syntactically");

            var errors = DaxScopeValidator.Validate(result.Tree);
            Assert.IsNotEmpty(errors);
            Assert.HasCount(1, errors);
            Assert.AreEqual("x", errors[0].Identifier);
            Assert.AreEqual(DaxScopeValidator.ScopeErrorKind.ForwardReference, errors[0].Kind);
        }

        [TestMethod]
        public void Scope_DefineVars_ForwardReference()
        {
            // DEFINE-level forward reference: x references y which comes later
            var input = @"DEFINE
    VAR x = y + 1
    VAR y = 1
EVALUATE {x}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Should parse syntactically");

            var errors = DaxScopeValidator.Validate(result.Tree);
            Assert.IsNotEmpty(errors);
            Assert.HasCount(1, errors);
            Assert.AreEqual("y", errors[0].Identifier);
            Assert.AreEqual(DaxScopeValidator.ScopeErrorKind.ForwardReference, errors[0].Kind);
        }

        // ===================================================================
        // Cross-scope references (sibling scopes)
        // ===================================================================

        [TestMethod]
        public void Scope_CrossScopeReference_SiblingBlocks()
        {
            // var1 is in the second expression's scope, not the first
            var input = @"EVALUATE
{VAR var2 = var1 + 1
RETURN var2,
VAR var1 = 1
RETURN var1}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Should parse syntactically — both are valid DAX syntax");

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsNotEmpty(errors);
            Assert.HasCount(1, errors, "Should flag var1 in the first block");
            Assert.AreEqual("var1", errors[0].Identifier);
            Assert.AreEqual(DaxScopeValidator.ScopeErrorKind.UndefinedVariable, errors[0].Kind);
        }

        [TestMethod]
        public void Scope_UnknownIdentifier_WithMetadata()
        {
            // xyz is not a known variable or table — flagged with metadata
            var input = "EVALUATE {VAR a = xyz + 1 RETURN a}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Should parse syntactically");

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsNotEmpty(errors);
            Assert.AreEqual("xyz", errors[0].Identifier);
            Assert.AreEqual(DaxScopeValidator.ScopeErrorKind.UndefinedVariable, errors[0].Kind);
        }

        [TestMethod]
        public void Scope_UnknownIdentifier_WithoutMetadata_NoError()
        {
            // Without metadata, unknown identifiers are not flagged
            // (could be unquoted table names)
            var input = "EVALUATE {VAR a = xyz + 1 RETURN a}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Should parse syntactically");

            var errors = DaxScopeValidator.Validate(result.Tree); // No metadata
            Assert.IsEmpty(errors, "Without metadata, unknown identifiers should not be flagged");
        }

        // ===================================================================
        // Multiple errors
        // ===================================================================

        [TestMethod]
        public void Scope_MultipleForwardRefs()
        {
            // Both b and c are forward-referenced
            var input = "EVALUATE {VAR a = b + c VAR b = 1 VAR c = 2 RETURN a}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Should parse syntactically");

            var errors = DaxScopeValidator.Validate(result.Tree);
            Assert.HasCount(2, errors);
            var ids = errors.Select(e => e.Identifier).OrderBy(x => x).ToList();
            Assert.AreEqual("b", ids[0]);
            Assert.AreEqual("c", ids[1]);
            Assert.IsTrue(errors.All(e => e.Kind == DaxScopeValidator.ScopeErrorKind.ForwardReference));
        }

        // ===================================================================
        // The user's exact examples
        // ===================================================================

        [TestMethod]
        public void Scope_UserExample_Valid()
        {
            // User's first example — should be valid
            var input = @"evaluate
{VAR var1 = 1
VAR var2 = var1 + 1
RETURN var2}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, string.Join("; ", result.Errors));

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsEmpty(errors, "User's valid example should have no scope errors");
        }

        [TestMethod]
        public void Scope_UserExample_Invalid()
        {
            // User's second example — var1 is not in scope in the first block
            var input = @"evaluate
{VAR var2 = var1 + 1
return var2,
VAR var1 = 1
RETURN var1}";
            var result = Parse(input);
            Assert.IsTrue(result.Success, "Both expressions are syntactically valid");

            var errors = DaxScopeValidator.Validate(result.Tree, _emptyMetadata);
            Assert.IsNotEmpty(errors, "var1 in the first block is not in scope");
            Assert.IsTrue(errors.Any(e => e.Identifier == "var1"));
        }
    }
}
