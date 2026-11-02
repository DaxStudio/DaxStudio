using System.Collections.Generic;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Metadata
{
    /// <summary>
    /// Describes the type of intellisense context at the cursor position.
    /// </summary>
    public enum EditState
    {
        /// <summary>Unknown or no specific context.</summary>
        Unknown,

        /// <summary>Inside a partial table reference ('Tab...). Suggest table names.</summary>
        PartialTable,

        /// <summary>After a complete table reference ('Table'). Suggest [Column] or operators.</summary>
        CompleteTable,

        /// <summary>Inside a partial column/measure reference ([Col...). Suggest columns/measures.</summary>
        PartialColumn,

        /// <summary>Inside a partial measure reference ([Mea...). Suggest measures.</summary>
        PartialMeasure,

        /// <summary>Inside a function call argument position. Suggest expressions, functions, refs.</summary>
        FunctionArgument,

        /// <summary>At the start of an expression (after =, after RETURN, etc.). Suggest everything.</summary>
        ExpressionStart,

        /// <summary>After a binary operator. Suggest expressions.</summary>
        AfterOperator,

        /// <summary>Inside a DEFINE block, at definition start. Suggest MEASURE/VAR/TABLE/COLUMN/FUNCTION.</summary>
        DefineContext,

        /// <summary>At the start of an EVALUATE block. Suggest table expressions.</summary>
        EvaluateContext,

        /// <summary>In an ORDER BY clause. Suggest columns.</summary>
        OrderByContext,

        /// <summary>Inside a FUNCTION definition. Suggest parameter names/types.</summary>
        FunctionDefinition,

        /// <summary>After ':' in a UDF parameter type annotation. Suggest type keywords.</summary>
        ParameterType,

        /// <summary>In a time intelligence function's calendar argument position. Suggest calendars.</summary>
        CalendarArgument,

        /// <summary>In a time intelligence function's period argument position. Suggest YEAR/QUARTER/MONTH/WEEK/DAY.</summary>
        PeriodArgument,

        /// <summary>Typing an unquoted identifier. Suggest variables, functions, tables.</summary>
        Identifier,

        /// <summary>After a comma in an argument list. Suggest next argument.</summary>
        NextArgument,

        /// <summary>At the top level, before any DEFINE or EVALUATE. Suggest DEFINE/EVALUATE.</summary>
        TopLevel,

        /// <summary>Inside a table constructor { }. Suggest expressions.</summary>
        TableConstructor,

        /// <summary>Inside a VAR definition. Suggest expressions for the value.</summary>
        VarDefinition,

        /// <summary>After RETURN keyword. Suggest expression.</summary>
        ReturnExpression
    }

    /// <summary>
    /// Captures the complete cursor context for generating intellisense completions.
    /// </summary>
    public class DaxState
    {
        /// <summary>The type of editing context at the cursor.</summary>
        public EditState State { get; set; }

        /// <summary>The name of the enclosing function (if inside a function call), or null.</summary>
        public string CurrentFunction { get; set; }

        /// <summary>Zero-based index of the current argument in the enclosing function, or -1.</summary>
        public int ArgumentIndex { get; set; }

        /// <summary>The current table context (for column completion), or null.</summary>
        public string CurrentTable { get; set; }

        /// <summary>The partial text being typed (for filtering suggestions).</summary>
        public string PartialText { get; set; }

        /// <summary>In-scope variable names at the cursor position.</summary>
        public IReadOnlyList<string> Variables { get; set; }

        /// <summary>In-scope DEFINE measures at the cursor position.</summary>
        public IReadOnlyList<string> DefinedMeasures { get; set; }

        /// <summary>In-scope DEFINE FUNCTION names (with parameters) at the cursor position.</summary>
        public IReadOnlyList<DefinedFunctionInfo> DefinedFunctions { get; set; }

        /// <summary>The nesting depth of function calls at the cursor.</summary>
        public int FunctionNestingDepth { get; set; }

        public DaxState()
        {
            State = EditState.Unknown;
            ArgumentIndex = -1;
            Variables = new List<string>();
            DefinedMeasures = new List<string>();
            DefinedFunctions = new List<DefinedFunctionInfo>();
        }

        public DaxState(EditState state, string currentFunction = null, int argumentIndex = -1,
            string currentTable = null, string partialText = null)
        {
            State = state;
            CurrentFunction = currentFunction;
            ArgumentIndex = argumentIndex;
            CurrentTable = currentTable;
            PartialText = partialText;
            Variables = new List<string>();
            DefinedMeasures = new List<string>();
            DefinedFunctions = new List<DefinedFunctionInfo>();
        }
    }
}
