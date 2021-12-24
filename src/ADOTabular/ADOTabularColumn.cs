using ADOTabular.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using Microsoft.AnalysisServices.Tabular;

namespace ADOTabular
{


    public  class ADOTabularColumn:IADOTabularColumn
    {
        
        public ADOTabularColumn( ADOTabularTable table, string internalReference, string name, string caption,  string description,
                                bool isVisible, ADOTabularObjectType columnType, string contents)
        {
            Contract.Requires(table != null, "The table parameter must not be null");

            Table = table;
            InternalReference = internalReference;
            Name = name ?? internalReference;
            Caption = caption ?? internalReference ?? name;
            Description = description;
            IsVisible = isVisible;
            ObjectType = columnType;
            Contents = contents;
            Role = $"{Table.InternalReference}_{InternalReference}";
            Variations = new List<ADOTabularVariation>();
            // tag measures that return the formatString for a calculation group
            if (columnType == ADOTabularObjectType.Measure && name == $"_{caption} FormatString")
            {
                ObjectType = ADOTabularObjectType.MeasureFormatString;
                Caption += " (FormatString)";
            }
        }

        public string InternalReference { get; private set; }

        public ADOTabularObjectType ObjectType { get; internal set; }

        public ADOTabularTable Table { get; private set; }

        public string TableName => Table?.Caption;
        public string Caption { get; private set; }
        public string Name { get; private set; }
        public string Contents { get; private set; }

        public virtual string DaxName {
            get
            {
                // for measures we exclude the table name
                return ObjectType == ADOTabularObjectType.Column  
                    ? $"{Table.DaxName}[{Name.Replace("]","]]")}]"
                    : $"[{Name.Replace("]", "]]")}]";
            }
        }

        public string Description { get; set; }

        public bool IsVisible { get; private set; }

        public bool IsInDisplayFolder { get; set; }
 
        public Type SystemType { get; set; }
        public DataType DataType { get; set; }

        public bool Nullable { get; internal set; }
        public long DistinctValues { get; internal set; }
        public string MinValue { get; internal set; }
        public string MaxValue { get; internal set; }
        public string FormatString { get; internal set; }
        public string DefaultAggregateFunction { get; internal set; }
        public long StringValueMaxLength { get; internal set; }
        public string DataTypeName { get { return DataType==0?string.Empty:DataType.ToString().Replace("System.", ""); } }
        
        internal string OrderByRef { get; set; }

        public ADOTabularColumn OrderBy
        {
            get
            {
                if (string.IsNullOrEmpty(OrderByRef)) return null;
                return Table.Columns.GetByPropertyRef(OrderByRef);
            }
        }

        //RRomano: Is it worth it to create the ADOTabularMeasure or reuse this in the ADOTabularColumn?
        public string MeasureExpression
        {
            get
            {
                if ((MetadataImage != MetadataImages.Measure) 
                    && (MetadataImage != MetadataImages.HiddenMeasure )
                    && (MetadataImage != MetadataImages.Kpi)
                    )
                {
                    return null;
                }

                // Return the measure expression from the table measures dictionary 
                // (the measures are loaded and cached on the use of the Table.Measures property)

                //var measure = this.Table.Measures.SingleOrDefault(s => s.Name.Equals(this.Name, StringComparison.OrdinalIgnoreCase));                

                //return measure?.Expression;
                var expression = this.Table.Model.MeasureExpressions[this.Name];
                return expression;
            }
        }

        public MetadataImages MetadataImage
        {
            get
            {
                switch (ObjectType)
                {
                    case ADOTabularObjectType.Column:
                        return IsVisible ? MetadataImages.Column : MetadataImages.HiddenColumn;
                    case ADOTabularObjectType.Hierarchy:
                        return MetadataImages.Hierarchy;
                    case ADOTabularObjectType.KPI:
                        return MetadataImages.Kpi;
                    case ADOTabularObjectType.Level:
                        return MetadataImages.Column;
                    case ADOTabularObjectType.KPIGoal:
                    case ADOTabularObjectType.KPIStatus:
                        return MetadataImages.Measure;
                    case ADOTabularObjectType.UnnaturalHierarchy:
                        return MetadataImages.UnnaturalHierarchy;
                    case ADOTabularObjectType.MeasureFormatString:
                        // TODO - add image for format string
                        return MetadataImages.HiddenMeasure;
                    default:
                        return IsVisible ? MetadataImages.Measure : MetadataImages.HiddenMeasure;
                
                }
            }
        }
        

        public void UpdateBasicStats(ADOTabularConnection connection)
        {
            if (connection == null) return;

            string qry = Type.GetTypeCode(SystemType) switch
            {
                TypeCode.Boolean => $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", \"False\",\"Max\", \"True\", \"DistinctCount\", COUNTROWS(DISTINCT({DaxName})) )",
                TypeCode.Empty => $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", \"\",\"Max\", \"\", \"DistinctCount\", COUNTROWS(DISTINCT({DaxName})) )",
                TypeCode.String => $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", FIRSTNONBLANK({DaxName},1),\"Max\", LASTNONBLANK({DaxName},1), \"DistinctCount\", COUNTROWS(DISTINCT({DaxName})) )",
                _ => $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", MIN({DaxName}),\"Max\", MAX({DaxName}), \"DistinctCount\", DISTINCTCOUNT({DaxName}) )",
            };

            using var dt = connection.ExecuteDaxQueryDataTable(qry);
            MinValue = dt.Rows[0][0].ToString();
            MaxValue = dt.Rows[0][1].ToString();
            DistinctValues = 
                (dt.Rows[0][2] == DBNull.Value) 
                ? 0 
                : (long)dt.Rows[0][2];
        }


        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            
            if (connection == null) return new List<string>() { "<Not Connected>" };

            string qryTemplate = $"{Constants.InternalQueryHeader}\nEVALUATE SAMPLE({{0}}, ALL({{1}}), 1) ORDER BY {{1}}";
            if (connection.AllFunctions.Contains("TOPNSKIP"))
                qryTemplate = $"{Constants.InternalQueryHeader}\nEVALUATE TOPNSKIP({{0}}, 0, ALL({{1}}), 1) ORDER BY {{1}}";

            var qry = string.Format(CultureInfo.InvariantCulture, qryTemplate, sampleSize * 2, DaxName);
            using var dt = connection.ExecuteDaxQueryDataTable(qry);
            List<string> _tmp = new List<string>(sampleSize * 2);
            foreach (DataRow dr in dt.Rows)
            {
                _tmp.Add(string.Format(CultureInfo.InvariantCulture, string.Format(CultureInfo.InvariantCulture, "{{0:{0}}}", FormatString), dr[0]));
            }
            return _tmp.Distinct().Take(sampleSize).ToList();
        }

        // used for relationship links
        public string Role { get; internal set; }
        public List<ADOTabularVariation> Variations { get; internal set; }
        public bool IsKey { get; internal set; }
    }
}
