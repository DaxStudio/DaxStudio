using DaxStudio.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;

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
        }

        public string InternalReference { get; private set; }

        public ADOTabularObjectType ObjectType { get; internal set; }

        public ADOTabularTable Table { get; private set; }

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

        public string OutputColumnName
        {
            get { return DaxName.Replace("'", ""); }
        }

        public string Description { get; set; }

        public bool IsVisible { get; private set; }

        public bool IsInDisplayFolder { get; set; }
 
        public Type DataType { get; set; }

        public bool Nullable { get; internal set; }
        public long DistinctValues { get; internal set; }
        public string MinValue { get; internal set; }
        public string MaxValue { get; internal set; }
        public string FormatString { get; internal set; }
        public string DefaultAggregateFunction { get; internal set; }
        public long StringValueMaxLength { get; internal set; }
        public string DataTypeName { get { return DataType==null?"n/a":DataType.ToString().Replace("System.", ""); } }

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

                var measure = this.Table.Measures.SingleOrDefault(s => s.Name.Equals(this.Name, StringComparison.OrdinalIgnoreCase));                

                return (measure?.Expression);
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
                    default:
                        return IsVisible ? MetadataImages.Measure : MetadataImages.HiddenMeasure;
                
                }
            }
        }
        

        public void UpdateBasicStats(ADOTabularConnection connection)
        {
            Contract.Requires(connection != null, "The connection parameter must not be null");

            string qry;
            switch (Type.GetTypeCode(DataType))
            {
                case TypeCode.Boolean:
                    qry = $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", \"False\",\"Max\", \"True\", \"DistinctCount\", COUNTROWS(DISTINCT({DaxName})) )";
                    break;
                case TypeCode.Empty:
                    qry = $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", \"\",\"Max\", \"\", \"DistinctCount\", COUNTROWS(DISTINCT({DaxName})) )";
                    break;
                case TypeCode.String:
                    qry = $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", FIRSTNONBLANK({DaxName},1),\"Max\", LASTNONBLANK({DaxName},1), \"DistinctCount\", COUNTROWS(DISTINCT({DaxName})) )";
                    break;
                default:
                    qry = $"{Constants.InternalQueryHeader}\nEVALUATE ROW(\"Min\", MIN({DaxName}),\"Max\", MAX({DaxName}), \"DistinctCount\", DISTINCTCOUNT({DaxName}) )";
                    break;

            }

            using (var dt = connection.ExecuteDaxQueryDataTable(qry))
            {

                MinValue = dt.Rows[0][0].ToString();
                MaxValue = dt.Rows[0][1].ToString();
                if (dt.Rows[0][2] == DBNull.Value)
                {
                    DistinctValues = 0;
                }
                else
                {
                    DistinctValues = (long)dt.Rows[0][2];
                }
            }
        }


        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            Contract.Requires(connection != null, "The connection parameter must not be null");

            string qryTemplate = $"{Constants.InternalQueryHeader}\nEVALUATE SAMPLE({{0}}, ALL({{1}}), RAND()) ORDER BY {{1}}";
            if (connection.AllFunctions.Contains("TOPNSKIP"))
                qryTemplate = $"{Constants.InternalQueryHeader}\nEVALUATE TOPNSKIP({{0}}, 0, ALL({{1}}), RAND()) ORDER BY {{1}}";

            var qry = string.Format(CultureInfo.InvariantCulture, qryTemplate, sampleSize * 2, DaxName);
            using (var dt = connection.ExecuteDaxQueryDataTable(qry))
            {
                List<string> _tmp = new List<string>(sampleSize * 2);
                foreach (DataRow dr in dt.Rows)
                {
                    _tmp.Add(string.Format(CultureInfo.InvariantCulture, string.Format(CultureInfo.InvariantCulture, "{{0:{0}}}", FormatString), dr[0]));
                }
                return _tmp.Distinct().Take(sampleSize).ToList();
            }
        }

        // used for relationship links
        public string Role { get; internal set; }
        public List<ADOTabularVariation> Variations { get; internal set; }
        public bool IsKey { get; internal set; } = false;
    }
}
