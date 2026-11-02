using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using DaxStudio.Parsers.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Validates DAX variable scoping rules.
    /// DAX uses sequential scoping: a VAR can only reference variables
    /// declared before it in the same or an enclosing scope.
    /// </summary>
    public static class DaxScopeValidator
    {
        public enum ScopeErrorKind
        {
            /// <summary>Variable is referenced before it is declared in the same or enclosing scope.</summary>
            ForwardReference,
            /// <summary>Identifier is not found in any enclosing scope or metadata.</summary>
            UndefinedVariable
        }

        public class ScopeError
        {
            public int Line { get; set; }
            public int Column { get; set; }
            public string Identifier { get; set; }
            public ScopeErrorKind Kind { get; set; }
            public string Message { get; set; }
        }

        private class Scope
        {
            public Scope Parent { get; }
            private readonly HashSet<string> _declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> _future = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public Scope(Scope parent = null) { Parent = parent; }

            public void AddDeclared(string name)
            {
                _declared.Add(name);
                _future.Remove(name);
            }

            public void AddFuture(string name) { _future.Add(name); }

            public bool IsDeclared(string name)
            {
                return _declared.Contains(name) || (Parent?.IsDeclared(name) ?? false);
            }

            public bool IsFuture(string name)
            {
                return _future.Contains(name) || (Parent?.IsFuture(name) ?? false);
            }
        }

        /// <summary>
        /// Validates variable scoping in a DAX parse tree.
        /// </summary>
        /// <param name="tree">The parse tree root (typically DaxQueryContext).</param>
        /// <param name="metadata">Optional metadata for resolving external names (tables).</param>
        /// <returns>List of scope errors found.</returns>
        public static List<ScopeError> Validate(IParseTree tree, IModelMetadataProvider metadata = null)
        {
            var errors = new List<ScopeError>();

            if (tree is DAXParser.DaxQueryContext query)
            {
                var globalScope = new Scope();

                if (query.defineBlock() != null)
                {
                    ProcessDefineBlock(query.defineBlock(), globalScope, errors, metadata);
                }

                foreach (var eval in query.evaluateBlock())
                {
                    ValidateNode(eval, globalScope, errors, metadata);
                }
            }
            else
            {
                ValidateNode(tree, new Scope(), errors, metadata);
            }

            return errors;
        }

        private static void ProcessDefineBlock(
            DAXParser.DefineBlockContext block,
            Scope scope,
            List<ScopeError> errors,
            IModelMetadataProvider metadata)
        {
            var definitions = block.definition();

            // Pre-populate future variables for forward reference detection
            foreach (var def in definitions)
            {
                if (def.variableDefinition() != null)
                {
                    var name = def.variableDefinition().identifierOrKeyword()?.GetText();
                    if (name != null) scope.AddFuture(name);
                }
            }

            // Process definitions sequentially
            foreach (var def in definitions)
            {
                if (def.variableDefinition() != null)
                {
                    var varDef = def.variableDefinition();
                    ValidateNode(varDef.expression(), scope, errors, metadata);
                    var name = varDef.identifierOrKeyword()?.GetText();
                    if (name != null) scope.AddDeclared(name);
                }
                else if (def.functionDefinition() != null)
                {
                    ProcessFunctionDefinition(def.functionDefinition(), scope, errors, metadata);
                }
                else
                {
                    // Measure, table, column definitions — validate their expressions
                    ValidateNode(def, scope, errors, metadata);
                }
            }
        }

        private static void ProcessFunctionDefinition(
            DAXParser.FunctionDefinitionContext funcDef,
            Scope parentScope,
            List<ScopeError> errors,
            IModelMetadataProvider metadata)
        {
            var funcScope = new Scope(parentScope);

            // Add function parameters to scope
            if (funcDef.parameterDefList() != null)
            {
                foreach (var param in funcDef.parameterDefList().parameterDef())
                {
                    var name = param.identifierOrKeyword()?.GetText();
                    if (name != null) funcScope.AddDeclared(name);
                }
            }

            // Validate the function body expression
            if (funcDef.expression() != null)
            {
                ValidateNode(funcDef.expression(), funcScope, errors, metadata);
            }
        }

        private static void ValidateNode(
            IParseTree node,
            Scope scope,
            List<ScopeError> errors,
            IModelMetadataProvider metadata)
        {
            if (node == null) return;

            // VAR/RETURN blocks get their own scope
            if (node is DAXParser.VarReturnExprContext varReturn)
            {
                ProcessVarReturnExpr(varReturn, scope, errors, metadata);
                return;
            }

            // Function definitions get their own scope (for parameters)
            if (node is DAXParser.FunctionDefinitionContext funcDef)
            {
                ProcessFunctionDefinition(funcDef, scope, errors, metadata);
                return;
            }

            // Bare identifier used as expression — potential variable reference
            if (node is DAXParser.IdentifierExprContext identExpr)
            {
                CheckIdentifierExpr(identExpr, scope, errors, metadata);
                return;
            }

            // Built-in function name used as bare expression (e.g. VAR Offset = 5; RETURN Offset)
            if (node is DAXParser.BuiltInFunctionRefExprContext builtInRef)
            {
                var builtIn = builtInRef.builtInFunction();
                if (builtIn != null)
                {
                    CheckReference(builtIn.GetText(), builtIn.Start, scope, errors, metadata);
                }
                return;
            }

            // Recurse into children
            for (int i = 0; i < node.ChildCount; i++)
            {
                ValidateNode(node.GetChild(i), scope, errors, metadata);
            }
        }

        private static void ProcessVarReturnExpr(
            DAXParser.VarReturnExprContext ctx,
            Scope parentScope,
            List<ScopeError> errors,
            IModelMetadataProvider metadata)
        {
            var localScope = new Scope(parentScope);

            // Pre-populate future variables
            var varDefs = ctx.variableDefinition();
            foreach (var varDef in varDefs)
            {
                var name = varDef.identifierOrKeyword()?.GetText();
                if (name != null) localScope.AddFuture(name);
            }

            // Process each definition sequentially
            foreach (var varDef in varDefs)
            {
                // Validate the RHS expression before declaring this variable
                ValidateNode(varDef.expression(), localScope, errors, metadata);

                // Declare the variable (makes it available for subsequent definitions)
                var name = varDef.identifierOrKeyword()?.GetText();
                if (name != null) localScope.AddDeclared(name);
            }

            // Validate the RETURN expression (all variables now in scope)
            ValidateNode(ctx.expression(), localScope, errors, metadata);
        }

        private static void CheckIdentifierExpr(
            DAXParser.IdentifierExprContext ctx,
            Scope scope,
            List<ScopeError> errors,
            IModelMetadataProvider metadata)
        {
            var funcName = ctx.functionName();
            if (funcName == null) return;

            // Only check simple (non-dotted) identifiers
            // Dotted identifiers are UDF references, not variable references
            if (funcName.dottedIdentifier() != null) return;

            var idCtx = funcName.identifierOrKeyword();
            if (idCtx == null) return;

            CheckReference(idCtx.GetText(), idCtx.Start, scope, errors, metadata);
        }

        private static void CheckReference(
            string name,
            IToken token,
            Scope scope,
            List<ScopeError> errors,
            IModelMetadataProvider metadata)
        {
            // Already declared in current or enclosing scope — valid
            if (scope.IsDeclared(name)) return;

            // Forward reference: will be declared later in the same or enclosing scope
            if (scope.IsFuture(name))
            {
                errors.Add(new ScopeError
                {
                    Line = token.Line,
                    Column = token.Column,
                    Identifier = name,
                    Kind = ScopeErrorKind.ForwardReference,
                    Message = $"Variable '{name}' is referenced before it is declared"
                });
                return;
            }

            // If metadata is available, check if it's a known table name
            if (metadata != null)
            {
                bool isKnownTable = metadata.GetTables().Any(t =>
                    t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (!isKnownTable)
                {
                    errors.Add(new ScopeError
                    {
                        Line = token.Line,
                        Column = token.Column,
                        Identifier = name,
                        Kind = ScopeErrorKind.UndefinedVariable,
                        Message = $"'{name}' is not defined in the current scope"
                    });
                }
            }
        }
    }
}
