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
                    // The list shows the plain (display) name, while the text inserted into the editor
                    // uses the pre-computed DaxName - unquoted when the name doesn't require quoting. The
                    // opening quote the user typed to trigger the list is removed when a non-quoted name
                    // is chosen.
                    items.Add(new CompletionItem(
                        table.Name,
                        CompletionItemKind.Table,
                        table.Description,
                        FormatTableName(table)));
                }
            }

            // Calendars are referenced with the same quoted-identifier syntax as tables.
            var calendars = _metadata.GetCalendars();
            if (calendars != null)
            {
                foreach (var c in calendars)
                {
                    if (cleanPartial == null || c.Name.StartsWith(cleanPartial, StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(new CompletionItem(
                            c.Name,
                            CompletionItemKind.Calendar,
                            $"Calendar from {c.TableName}",
                            $"'{c.Name}'"));
                    }
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
                    col.Name,
                    CompletionItemKind.Column,
                    $"{col.DataType} - {col.Description}",
                    $"[{col.Name}]"));
            }
            return items;
        }

        private IReadOnlyList<CompletionItem> GetColumnAndMeasureCompletions(string tableName, string partialText)
        {
            var items = new List<CompletionItem>();
            // Strip quotes from table name and brackets from partial text
            var cleanTable = tableName?.Trim('\'');
            var cleanPartial = partialText?.TrimStart('[');

            // Qualified reference (Table[...]) — only that table's columns are valid in this context.
            if (!string.IsNullOrEmpty(cleanTable))
            {
                var columns = _metadata.GetColumns(cleanTable);
                foreach (var col in columns)
                {
                    if (cleanPartial == null || col.Name.StartsWith(cleanPartial, StringComparison.OrdinalIgnoreCase))
                    {
                        items.Add(new CompletionItem(
                            col.Name,
                            CompletionItemKind.Column,
                            $"{col.DataType} - {col.Description}",
                            $"[{col.Name}]"));
                    }
                }
                return items;
            }

            // Unqualified reference ([...]) — suggest measures from across the model.
            var measures = _metadata.GetMeasures();
            foreach (var m in measures)
            {
                if (cleanPartial == null || m.Name.StartsWith(cleanPartial, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new CompletionItem(
                        m.Name,
                        CompletionItemKind.Measure,
                        m.Description,
                        $"[{m.Name}]"));
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
                items.Add(new CompletionItem(t.Name, CompletionItemKind.Table, t.Description, FormatTableName(t)));
            }

            // Measures
            var measures = _metadata.GetMeasures();
            foreach (var m in measures)
            {
                items.Add(new CompletionItem(m.Name, CompletionItemKind.Measure, m.Description, $"[{m.Name}]"));
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
                    items.Add(new CompletionItem(m, CompletionItemKind.Measure, "Defined measure", $"[{m}]"));
                }
            }

            // DEFINE FUNCTION user-defined functions declared in the current query
            if (state.DefinedFunctions != null)
            {
                foreach (var fn in state.DefinedFunctions)
                {
                    items.Add(new CompletionItem(fn.Name, CompletionItemKind.Function, "Defined function"));
                }
            }

            // DAX keywords (DEFINE, EVALUATE, ORDER BY, VAR, RETURN, ...)
            items.AddRange(GetKeywordCompletions());

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

            // User defined (model) functions - may return a table
            var udfs = _metadata.GetUserDefinedFunctions();
            foreach (var u in udfs)
            {
                items.Add(new CompletionItem(u.Name, CompletionItemKind.Function, u.Description));
            }

            // Tables
            var tables = _metadata.GetTables();
            foreach (var t in tables)
            {
                items.Add(new CompletionItem(t.Name, CompletionItemKind.Table, t.Description, FormatTableName(t)));
            }

            // DEFINE FUNCTION user-defined functions declared in the current query (may return a table)
            if (state.DefinedFunctions != null)
            {
                foreach (var fn in state.DefinedFunctions)
                {
                    items.Add(new CompletionItem(fn.Name, CompletionItemKind.Function, "Defined function"));
                }
            }

            // DAX keywords valid at the start of a table expression
            items.AddRange(GetKeywordCompletions());

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
                    c.Name,
                    CompletionItemKind.Calendar,
                    $"Calendar from {c.TableName}",
                    $"'{c.Name}'"));
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
                        $"{table.Name}[{col.Name}]",
                        CompletionItemKind.Column,
                        col.Description,
                        $"{FormatTableName(table)}[{col.Name}]"));
                }
            }
            return items;
        }

        private IReadOnlyList<CompletionItem> GetTopLevelCompletions()
        {
            return GetKeywordCompletions();
        }

        // DAX query/definition keywords surfaced in completion lists. Mirrors the set offered by the
        // legacy regex-based intellisense provider so behaviour is consistent between the two engines.
        private static readonly (string Keyword, string Description)[] Keywords =
        {
            ("DEFINE",   "Begin a DEFINE block"),
            ("EVALUATE", "Begin an EVALUATE statement"),
            ("MEASURE",  "Define a measure"),
            ("VAR",      "Define a variable"),
            ("RETURN",   "Return the result of a preceding VAR block"),
            ("COLUMN",   "Define a calculated column"),
            ("TABLE",    "Define a calculated/virtual table"),
            ("FUNCTION", "Define a user-defined function"),
            ("ORDER BY", "Sort the results of an EVALUATE statement"),
            ("START AT", "Specify the starting point for an ORDER BY"),
            ("ASC",      "Sort ascending"),
            ("DESC",     "Sort descending"),
        };

        private static IReadOnlyList<CompletionItem> GetKeywordCompletions()
        {
            return Keywords
                .Select(k => new CompletionItem(k.Keyword, CompletionItemKind.Keyword, k.Description))
                .ToList();
        }

        // Table names only require single quotes when they contain a space/special character, start with
        // a digit, or collide with a reserved word. When the metadata provider supplies a pre-computed
        // DaxName (which already applies the reserved-word rule) we use it; otherwise fall back to a
        // syntactic check based on the name characters.
        private static string FormatTableName(TableMetadata table)
        {
            if (table == null) return string.Empty;
            if (!string.IsNullOrEmpty(table.DaxName)) return table.DaxName;
            return TableNameNeedsQuoting(table.Name)
                ? $"'{table.Name?.Replace("'", "''")}'"
                : table.Name;
        }

        private static bool TableNameNeedsQuoting(string name)
        {
            const string validStart = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_";
            const string validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_0123456789";
            if (string.IsNullOrEmpty(name)) return true;
            if (validStart.IndexOf(name[0]) < 0) return true;
            return name.Any(c => validChars.IndexOf(c) < 0);
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
    /// Represents a single completion suggestion. <see cref="Label"/> is the display name shown in the
    /// completion list (unquoted/unbracketed), while <see cref="InsertText"/> is the DAX syntax that is
    /// inserted into the editor (e.g. <c>'Product Category'</c> or <c>[Sales Amount]</c>).
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
