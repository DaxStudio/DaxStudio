using System;
using System.Collections.Generic;
using System.Linq;
using DaxStudio.Parsers.Metadata;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Dax
{
    /// <summary>
    /// Maps EditState to appropriate completion items using the metadata provider.
    /// </summary>
    public class DaxCompletionProvider
    {
        private readonly IModelMetadataProvider _metadata;

        public DaxCompletionProvider(IModelMetadataProvider metadata)
        {
            _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        }

        /// <summary>
        /// Gets completion items for the given DaxState.
        /// </summary>
        public IReadOnlyList<CompletionItem> GetCompletions(Metadata.DaxState state)
        {
            switch (state.State)
            {
                case Metadata.EditState.PartialTable:
                    return GetTableCompletions(state.PartialText);

                case Metadata.EditState.CompleteTable:
                    return GetColumnCompletions(state.CurrentTable);

                case Metadata.EditState.PartialColumn:
                case Metadata.EditState.PartialMeasure:
                    return GetColumnAndMeasureCompletions(state.CurrentTable, state.PartialText);

                case Metadata.EditState.FunctionArgument:
                case Metadata.EditState.NextArgument:
                    return GetExpressionCompletions(state);

                case Metadata.EditState.ExpressionStart:
                case Metadata.EditState.AfterOperator:
                case Metadata.EditState.ReturnExpression:
                    return GetExpressionCompletions(state);

                case Metadata.EditState.DefineContext:
                    return GetDefineKeywordCompletions();

                case Metadata.EditState.EvaluateContext:
                    return GetTableExpressionCompletions(state);

                case Metadata.EditState.OrderByContext:
                    return GetColumnCompletionsAll();

                case Metadata.EditState.ParameterType:
                    return GetTypeKeywordCompletions();

                case Metadata.EditState.PeriodArgument:
                    return GetPeriodCompletions();

                case Metadata.EditState.CalendarArgument:
                    return GetCalendarCompletions();

                case Metadata.EditState.TopLevel:
                    return GetTopLevelCompletions();

                case Metadata.EditState.Identifier:
                    return GetIdentifierCompletions(state);

                case Metadata.EditState.TableConstructor:
                    return GetExpressionCompletions(state);

                case Metadata.EditState.FunctionDefinition:
                case Metadata.EditState.VarDefinition:
                    return new List<CompletionItem>();

                default:
                    return GetExpressionCompletions(state);
            }
        }

        private IReadOnlyList<CompletionItem> GetTableCompletions(string partialText)
        {
            var items = new List<CompletionItem>();
            var tables = _metadata.GetTables();
            // Strip leading quote from partial text (lexer includes it)
            var cleanPartial = partialText?.TrimStart('\'');
            foreach (var table in tables)
            {
                if (cleanPartial == null || table.Name.StartsWith(cleanPartial, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new CompletionItem(
                        $"'{table.Name}'",
                        CompletionItemKind.Table,
                        table.Description));
                }
            }
            return items;
        }

        private IReadOnlyList<CompletionItem> GetColumnCompletions(string tableName)
        {
            var items = new List<CompletionItem>();
            if (string.IsNullOrEmpty(tableName)) return items;

            // Strip quotes from table name (lexer includes them)
            var cleanTable = tableName.Trim('\'');
            var columns = _metadata.GetColumns(cleanTable);
            foreach (var col in columns)
            {
                items.Add(new CompletionItem(
                    $"[{col.Name}]",
                    CompletionItemKind.Column,
                    $"{col.DataType} - {col.Description}"));
            }

            var measures = _metadata.GetMeasures(cleanTable);
            foreach (var m in measures)
            {
                items.Add(new CompletionItem(
                    $"[{m.Name}]",
                    CompletionItemKind.Measure,
                    m.Description));
            }
            return items;
        }

        private IReadOnlyList<CompletionItem> GetColumnAndMeasureCompletions(string tableName, string partialText)
        {
            var items = new List<CompletionItem>();
            // Strip quotes from table name and brackets from partial text
            var cleanTable = tableName?.Trim('\'');
            var cleanPartial = partialText?.TrimStart('[');

            if (!string.IsNullOrEmpty(cleanTable))
            {
                var columns = _metadata.GetColumns(cleanTable);
                foreach (var col in columns)
                {
                    if (cleanPartial == null || col.Name.StartsWith(cleanPartial, StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(new CompletionItem(
                            $"[{col.Name}]",
                            CompletionItemKind.Column,
                            $"{col.DataType} - {col.Description}"));
                    }
                }
            }

            var measures = _metadata.GetMeasures();
            foreach (var m in measures)
            {
                if (cleanPartial == null || m.Name.StartsWith(cleanPartial, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new CompletionItem(
                        $"[{m.Name}]",
                        CompletionItemKind.Measure,
                        m.Description));
                }
            }

            return items;
        }

        private IReadOnlyList<CompletionItem> GetExpressionCompletions(Metadata.DaxState state)
        {
            var items = new List<CompletionItem>();

            // Functions
            var builtIns = _metadata.GetBuiltInFunctions();
            foreach (var f in builtIns)
            {
                items.Add(new CompletionItem(f.Name, CompletionItemKind.Function, f.Description));
            }

            // UDFs
            var udfs = _metadata.GetUserDefinedFunctions();
            foreach (var u in udfs)
            {
                items.Add(new CompletionItem(u.Name, CompletionItemKind.Function, u.Description));
            }

            // Tables
            var tables = _metadata.GetTables();
            foreach (var t in tables)
            {
                items.Add(new CompletionItem($"'{t.Name}'", CompletionItemKind.Table, t.Description));
            }

            // Measures
            var measures = _metadata.GetMeasures();
            foreach (var m in measures)
            {
                items.Add(new CompletionItem($"[{m.Name}]", CompletionItemKind.Measure, m.Description));
            }

            // In-scope variables
            if (state.Variables != null)
            {
                foreach (var v in state.Variables)
                {
                    items.Add(new CompletionItem(v, CompletionItemKind.Variable, "Variable"));
                }
            }

            // Defined measures
            if (state.DefinedMeasures != null)
            {
                foreach (var m in state.DefinedMeasures)
                {
                    items.Add(new CompletionItem($"[{m}]", CompletionItemKind.Measure, "Defined measure"));
                }
            }

            return items;
        }

        private IReadOnlyList<CompletionItem> GetTableExpressionCompletions(Metadata.DaxState state)
        {
            var items = new List<CompletionItem>();

            // Table-returning functions
            var builtIns = _metadata.GetBuiltInFunctions();
            foreach (var f in builtIns)
            {
                items.Add(new CompletionItem(f.Name, CompletionItemKind.Function, f.Description));
            }

            // Tables
            var tables = _metadata.GetTables();
            foreach (var t in tables)
            {
                items.Add(new CompletionItem($"'{t.Name}'", CompletionItemKind.Table, t.Description));
            }

            return items;
        }

        private IReadOnlyList<CompletionItem> GetDefineKeywordCompletions()
        {
            return new List<CompletionItem>
            {
                new CompletionItem("MEASURE", CompletionItemKind.Keyword, "Define a measure"),
                new CompletionItem("VAR", CompletionItemKind.Keyword, "Define a variable"),
                new CompletionItem("TABLE", CompletionItemKind.Keyword, "Define a virtual table"),
                new CompletionItem("COLUMN", CompletionItemKind.Keyword, "Define a virtual column"),
                new CompletionItem("FUNCTION", CompletionItemKind.Keyword, "Define a user-defined function")
            };
        }

        private IReadOnlyList<CompletionItem> GetTypeKeywordCompletions()
        {
            return new List<CompletionItem>
            {
                new CompletionItem("SCALAR", CompletionItemKind.Keyword, "Scalar value type"),
                new CompletionItem("TABLE", CompletionItemKind.Keyword, "Table type"),
                new CompletionItem("ANYVAL", CompletionItemKind.Keyword, "Any value type"),
                new CompletionItem("ANYREF", CompletionItemKind.Keyword, "Any reference type"),
                new CompletionItem("INT64", CompletionItemKind.Keyword, "64-bit integer subtype"),
                new CompletionItem("DECIMAL", CompletionItemKind.Keyword, "Decimal subtype"),
                new CompletionItem("DOUBLE", CompletionItemKind.Keyword, "Double precision subtype"),
                new CompletionItem("STRING", CompletionItemKind.Keyword, "String subtype"),
                new CompletionItem("BOOLEAN", CompletionItemKind.Keyword, "Boolean subtype"),
                new CompletionItem("DATETIME", CompletionItemKind.Keyword, "DateTime subtype"),
                new CompletionItem("NUMERIC", CompletionItemKind.Keyword, "Numeric subtype"),
                new CompletionItem("CURRENCY", CompletionItemKind.Keyword, "Currency subtype"),
                new CompletionItem("VARIANT", CompletionItemKind.Keyword, "Variant subtype"),
                new CompletionItem("VAL", CompletionItemKind.Keyword, "Eager evaluation mode"),
                new CompletionItem("EXPR", CompletionItemKind.Keyword, "Lazy evaluation mode")
            };
        }

        private IReadOnlyList<CompletionItem> GetPeriodCompletions()
        {
            return new List<CompletionItem>
            {
                new CompletionItem("YEAR", CompletionItemKind.Keyword, "Year period"),
                new CompletionItem("QUARTER", CompletionItemKind.Keyword, "Quarter period"),
                new CompletionItem("MONTH", CompletionItemKind.Keyword, "Month period"),
                new CompletionItem("WEEK", CompletionItemKind.Keyword, "Week period"),
                new CompletionItem("DAY", CompletionItemKind.Keyword, "Day period")
            };
        }

        private IReadOnlyList<CompletionItem> GetCalendarCompletions()
        {
            var items = new List<CompletionItem>();
            var calendars = _metadata.GetCalendars();
            foreach (var c in calendars)
            {
                items.Add(new CompletionItem(
                    $"'{c.Name}'",
                    CompletionItemKind.Calendar,
                    $"Calendar from {c.TableName}"));
            }
            return items;
        }

        private IReadOnlyList<CompletionItem> GetColumnCompletionsAll()
        {
            var items = new List<CompletionItem>();
            var tables = _metadata.GetTables();
            foreach (var table in tables)
            {
                var columns = _metadata.GetColumns(table.Name);
                foreach (var col in columns)
                {
                    items.Add(new CompletionItem(
                        $"'{table.Name}'[{col.Name}]",
                        CompletionItemKind.Column,
                        col.Description));
                }
            }
            return items;
        }

        private IReadOnlyList<CompletionItem> GetTopLevelCompletions()
        {
            return new List<CompletionItem>
            {
                new CompletionItem("DEFINE", CompletionItemKind.Keyword, "Begin a DEFINE block"),
                new CompletionItem("EVALUATE", CompletionItemKind.Keyword, "Begin an EVALUATE statement")
            };
        }

        private IReadOnlyList<CompletionItem> GetIdentifierCompletions(Metadata.DaxState state)
        {
            // Same as expression completions but filtered by partial text
            var all = GetExpressionCompletions(state);
            if (string.IsNullOrEmpty(state.PartialText)) return all;

            return all.Where(item =>
                item.Label.StartsWith(state.PartialText, StringComparison.OrdinalIgnoreCase) ||
                item.Label.Contains(state.PartialText)).ToList();
        }
    }

    /// <summary>
    /// Represents a single completion suggestion.
    /// </summary>
    public class CompletionItem
    {
        public string Label { get; set; }
        public CompletionItemKind Kind { get; set; }
        public string Description { get; set; }
        public string InsertText { get; set; }

        public CompletionItem() { }
        public CompletionItem(string label, CompletionItemKind kind, string description = "", string insertText = null)
        {
            Label = label;
            Kind = kind;
            Description = description;
            InsertText = insertText ?? label;
        }
    }

    public enum CompletionItemKind
    {
        Function,
        Table,
        Column,
        Measure,
        Variable,
        Keyword,
        Calendar
    }
}
