using System.Collections.Generic;
using System.Linq;
using ADOTabular;
using DaxStudio.Parsers.Metadata;

namespace DaxStudio.UI.Utils.Intellisense
{
    /// <summary>
    /// Bridges the ADOTabular model metadata onto the parser's <see cref="IModelMetadataProvider"/>
    /// abstraction so the ANTLR-based completion engine can suggest tables, columns, measures,
    /// functions and calendars from the connected model.
    /// </summary>
    public class AdoTabularMetadataProvider : IModelMetadataProvider
    {
        private readonly ADOTabularModel _model;
        private readonly ADOTabularFunctionGroupCollection _functionGroups;

        public AdoTabularMetadataProvider(ADOTabularModel model, ADOTabularFunctionGroupCollection functionGroups)
        {
            _model = model;
            _functionGroups = functionGroups;
        }

        public IReadOnlyList<TableMetadata> GetTables()
        {
            var result = new List<TableMetadata>();
            if (_model == null) return result;

            foreach (var table in _model.Tables)
            {
                result.Add(new TableMetadata(table.Name, table.Description ?? string.Empty, !table.IsVisible, table.DaxName));
            }
            return result;
        }

        public IReadOnlyList<ColumnMetadata> GetColumns(string tableName)
        {
            var result = new List<ColumnMetadata>();
            if (_model == null || string.IsNullOrEmpty(tableName)) return result;

            var table = FindTable(tableName);
            if (table == null) return result;

            foreach (var col in table.Columns)
            {
                if (col.ObjectType != ADOTabularObjectType.Column) continue;
                result.Add(new ColumnMetadata(
                    table.Name,
                    col.Name,
                    col.DataType == 0 ? string.Empty : col.DataTypeName,
                    col.Description ?? string.Empty,
                    !col.IsVisible));
            }
            return result;
        }

        public IReadOnlyList<MeasureMetadata> GetMeasures()
        {
            var result = new List<MeasureMetadata>();
            if (_model == null) return result;

            foreach (var table in _model.Tables)
            {
                AddMeasures(table, result);
            }
            return result;
        }

        public IReadOnlyList<MeasureMetadata> GetMeasures(string tableName)
        {
            var result = new List<MeasureMetadata>();
            if (_model == null || string.IsNullOrEmpty(tableName)) return result;

            var table = FindTable(tableName);
            if (table == null) return result;

            AddMeasures(table, result);
            return result;
        }

        public IReadOnlyList<UdfMetadata> GetUserDefinedFunctions()
        {
            // User defined functions defined in the model are returned by MDSCHEMA_FUNCTIONS in the
            // "USERDEFINED" interface/group, so surface those here (with their description and parameter
            // names) rather than mixing them in with the built-in DAX functions.
            var result = new List<UdfMetadata>();
            if (_functionGroups == null) return result;

            foreach (var group in _functionGroups)
            {
                if (!IsUserDefinedGroup(group)) continue;

                foreach (var func in group.Functions)
                {
                    List<UdfParameter> parameters;
                    try
                    {
                        parameters = func.Parameters?
                            .Select(p => new UdfParameter(p.Name))
                            .ToList() ?? new List<UdfParameter>();
                    }
                    catch (System.Exception ex)
                    {
                        Serilog.Log.Debug(ex, "{class} {method} Unable to read parameters for user defined function {function}",
                            nameof(AdoTabularMetadataProvider), nameof(GetUserDefinedFunctions), func.Name);
                        parameters = new List<UdfParameter>();
                    }

                    result.Add(new UdfMetadata(func.Name, func.Description ?? string.Empty, parameters));
                }
            }
            return result;
        }

        public IReadOnlyList<CalendarMetadata> GetCalendars()
        {
            var result = new List<CalendarMetadata>();
            if (_model == null) return result;

            foreach (var cal in _model.Calendars)
            {
                result.Add(new CalendarMetadata(cal.Name, cal.Name));
            }
            return result;
        }

        public IReadOnlyList<FunctionSignature> GetBuiltInFunctions()
        {
            var result = new List<FunctionSignature>();
            if (_functionGroups == null) return result;

            foreach (var group in _functionGroups)
            {
                // User defined (model) functions are surfaced separately via GetUserDefinedFunctions()
                // so they can carry their model description - skip them here to avoid duplicates.
                if (IsUserDefinedGroup(group)) continue;

                foreach (var func in group.Functions)
                {
                    // Enumerating the parameters can throw for some functions because the underlying
                    // PARAMETERINFO rowset is not always well-formed. Completion only needs the function
                    // name/description, so fall back to an empty parameter list if enumeration fails.
                    List<FunctionParameter> parameters;
                    try
                    {
                        parameters = func.Parameters?
                            .Select(p => new FunctionParameter(
                                p.Name,
                                string.Empty,
                                p.Description ?? string.Empty,
                                p.Optional,
                                p.Repeatable))
                            .ToList() ?? new List<FunctionParameter>();
                    }
                    catch (System.Exception ex)
                    {
                        Serilog.Log.Debug(ex, "{class} {method} Unable to read parameters for function {function}",
                            nameof(AdoTabularMetadataProvider), nameof(GetBuiltInFunctions), func.Name);
                        parameters = new List<FunctionParameter>();
                    }

                    result.Add(new FunctionSignature(
                        func.Name,
                        func.Description ?? string.Empty,
                        string.Empty,
                        parameters));
                }
            }
            return result;
        }

        // The interface/group name MDSCHEMA_FUNCTIONS uses for user defined (model) functions.
        private const string UserDefinedFunctionGroup = "USERDEFINED";

        private static bool IsUserDefinedGroup(ADOTabularFunctionGroup group)
        {
            return string.Equals(group?.Caption, UserDefinedFunctionGroup, System.StringComparison.OrdinalIgnoreCase);
        }

        private ADOTabularTable FindTable(string tableName)
        {
            var clean = tableName.Trim('\'');
            return _model.Tables.FirstOrDefault(t =>
                string.Equals(t.Name, clean, System.StringComparison.CurrentCultureIgnoreCase));
        }

        private static void AddMeasures(ADOTabularTable table, ICollection<MeasureMetadata> result)
        {
            foreach (var col in table.Columns)
            {
                if (col.ObjectType != ADOTabularObjectType.Measure) continue;
                result.Add(new MeasureMetadata(
                    table.Name,
                    col.Name,
                    string.Empty,
                    col.Description ?? string.Empty));
            }
        }
    }
}
