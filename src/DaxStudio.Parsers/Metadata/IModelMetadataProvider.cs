using System.Collections.Generic;

using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.Metadata
{
    /// <summary>
    /// Provides runtime metadata about the connected model for intellisense.
    /// Implementations inject data from ADOTabularModel or other sources.
    /// </summary>
    public interface IModelMetadataProvider
    {
        IReadOnlyList<TableMetadata> GetTables();
        IReadOnlyList<ColumnMetadata> GetColumns(string tableName);
        IReadOnlyList<MeasureMetadata> GetMeasures();
        IReadOnlyList<MeasureMetadata> GetMeasures(string tableName);
        IReadOnlyList<UdfMetadata> GetUserDefinedFunctions();
        IReadOnlyList<CalendarMetadata> GetCalendars();
        IReadOnlyList<FunctionSignature> GetBuiltInFunctions();
    }

    public class TableMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsHidden { get; set; }

        public TableMetadata() { }
        public TableMetadata(string name, string description = "", bool isHidden = false)
        {
            Name = name;
            Description = description;
            IsHidden = isHidden;
        }
    }

    public class ColumnMetadata
    {
        public string TableName { get; set; }
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Description { get; set; }
        public bool IsHidden { get; set; }

        public ColumnMetadata() { }
        public ColumnMetadata(string tableName, string name, string dataType = "", string description = "", bool isHidden = false)
        {
            TableName = tableName;
            Name = name;
            DataType = dataType;
            Description = description;
            IsHidden = isHidden;
        }
    }

    public class MeasureMetadata
    {
        public string TableName { get; set; }
        public string Name { get; set; }
        public string Expression { get; set; }
        public string Description { get; set; }
        public string DisplayFolder { get; set; }

        public MeasureMetadata() { }
        public MeasureMetadata(string tableName, string name, string expression = "", string description = "")
        {
            TableName = tableName;
            Name = name;
            Expression = expression;
            Description = description;
        }
    }

    public class UdfMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Expression { get; set; }
        public IReadOnlyList<UdfParameter> Parameters { get; set; }

        public UdfMetadata() { Parameters = new List<UdfParameter>(); }
        public UdfMetadata(string name, string description, IReadOnlyList<UdfParameter> parameters)
        {
            Name = name;
            Description = description;
            Parameters = parameters ?? new List<UdfParameter>();
        }
    }

    public class UdfParameter
    {
        public string Name { get; set; }
        public UdfTypeCategory TypeCategory { get; set; }
        public UdfTypeSubtype TypeSubtype { get; set; }
        public UdfParameterMode Mode { get; set; }

        public UdfParameter() { }
        public UdfParameter(string name, UdfTypeCategory typeCategory = UdfTypeCategory.AnyVal,
            UdfTypeSubtype typeSubtype = UdfTypeSubtype.Variant, UdfParameterMode mode = UdfParameterMode.Val)
        {
            Name = name;
            TypeCategory = typeCategory;
            TypeSubtype = typeSubtype;
            Mode = mode;
        }
    }

    public enum UdfTypeCategory
    {
        AnyVal,
        Scalar,
        Table,
        AnyRef
    }

    public enum UdfTypeSubtype
    {
        Variant,
        Int64,
        Decimal,
        Double,
        String,
        Boolean,
        DateTime,
        Numeric,
        Currency
    }

    public enum UdfParameterMode
    {
        Val,
        Expr
    }

    public class CalendarMetadata
    {
        public string Name { get; set; }
        public string TableName { get; set; }
        public IReadOnlyList<string> Categories { get; set; }

        public CalendarMetadata() { Categories = new List<string>(); }
        public CalendarMetadata(string name, string tableName, IReadOnlyList<string> categories = null)
        {
            Name = name;
            TableName = tableName;
            Categories = categories ?? new List<string>();
        }
    }

    public class FunctionSignature
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ReturnType { get; set; }
        public IReadOnlyList<FunctionParameter> Parameters { get; set; }

        public FunctionSignature() { Parameters = new List<FunctionParameter>(); }
        public FunctionSignature(string name, string description, string returnType, IReadOnlyList<FunctionParameter> parameters)
        {
            Name = name;
            Description = description;
            ReturnType = returnType;
            Parameters = parameters ?? new List<FunctionParameter>();
        }
    }

    public class FunctionParameter
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string Description { get; set; }
        public bool IsOptional { get; set; }
        public bool IsRepeating { get; set; }

        public FunctionParameter() { }
        public FunctionParameter(string name, string dataType, string description = "", bool isOptional = false, bool isRepeating = false)
        {
            Name = name;
            DataType = dataType;
            Description = description;
            IsOptional = isOptional;
            IsRepeating = isRepeating;
        }
    }
}
