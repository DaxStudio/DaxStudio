using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ADOTabular
{


    public  class ADOTabularColumn:IADOTabularColumn
    {

        public ADOTabularColumn(ADOTabularTable table, DataRow dr, ADOTabularColumnType colType)
        {
            Table = table; 
            ColumnType = colType;
            if (colType == ADOTabularColumnType.Column)
            {
                Caption = dr["HIERARCHY_CAPTION"].ToString();
                Name = dr["HIERARCHY_NAME"].ToString();
                IsVisible = bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString());
                Description = dr["DESCRIPTION"].ToString();
            }
            else
            {
                Caption = dr["MEASURE_CAPTION"].ToString();
                Name = dr["MEASURE_NAME"].ToString();
                IsVisible = bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString());
                Description = dr["DESCRIPTION"].ToString();
            }
        }

        public ADOTabularColumn( ADOTabularTable table, string internalReference, string name, string caption,  string description,
                                bool isVisible, ADOTabularColumnType columnType, string contents)
        {
            Table = table;
            InternalReference = internalReference;
            Name = name ?? internalReference;
            Caption = caption ?? internalReference ?? name;
            Description = description;
            IsVisible = isVisible;
            ColumnType = columnType;
            Contents = contents;
        }

        public string InternalReference { get; private set; }

        public ADOTabularColumnType ColumnType { get; internal set; }

        public ADOTabularTable Table { get; private set; }

        public string Caption { get; private set; }
        public string Name { get; private set; }

        public string Contents { get; private set; }

        public virtual string DaxName {
            get
            {
                // for measures we exclude the table name
                return ColumnType == ADOTabularColumnType.Column  
                    ? string.Format("{0}[{1}]", Table.DaxName, Name)
                    : string.Format("[{0}]",Name);
            }
        }

        public string OutputColumnName
        {
            get { return DaxName.Replace("'", ""); }
        }

        public string Description { get; set; }

        public bool IsVisible { get; private set; }
 
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
                if (this.MetadataImage != MetadataImages.Measure)
                {
                    return null;
                }

                // Return the measure expression from the table measures dictionary (the measures are loaded and cached on the use of the Table.Measures property)

                var measure = this.Table.Measures.SingleOrDefault(s => s.Name.Equals(this.Name, StringComparison.OrdinalIgnoreCase));                

                return (measure != null ? measure.Expression : null);
            }
        }

        public MetadataImages MetadataImage
        {
            get
            {
                switch (ColumnType)
                {
                    case ADOTabularColumnType.Column:
                        return IsVisible ? MetadataImages.Column : MetadataImages.HiddenColumn;
                    case ADOTabularColumnType.Hierarchy:
                        return MetadataImages.Hierarchy;
                    case ADOTabularColumnType.KPI:
                        return MetadataImages.Kpi;
                    case ADOTabularColumnType.Level:
                        return MetadataImages.Column;
                    case ADOTabularColumnType.KPIGoal:
                    case ADOTabularColumnType.KPIStatus:
                        return MetadataImages.Measure;
                    case ADOTabularColumnType.UnnaturalHierarchy:
                        return MetadataImages.UnnaturalHierarchy;
                    default:
                        return IsVisible ? MetadataImages.Measure : MetadataImages.HiddenMeasure;
                
                }
            }
        }
        

        public void UpdateBasicStats(ADOTabularConnection connection)
        {

            var qry = string.Format("EVALUATE ROW(\"Min\", MIN({0}),\"Max\", MAX({0}), \"DistinctCount\", DISTINCTCOUNT({0}) )", DaxName);
            if (DataType == typeof(string))
            {
                qry = string.Format("EVALUATE ROW(\"Min\", \"\",\"Max\", \"\", \"DistinctCount\", COUNTROWS(DISTINCT({0})) )", DaxName);
            }
            var dt = connection.ExecuteDaxQueryDataTable(qry);
            MinValue = dt.Rows[0][0].ToString();
            MaxValue = dt.Rows[0][1].ToString();
            DistinctValues = (long)dt.Rows[0][2]; 
        }

        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            var qry = string.Format("EVALUATE TOPNSKIP({0}, 0, ALL({1}), RAND())", sampleSize * 2, DaxName);
            var dt = connection.ExecuteDaxQueryDataTable(qry);
            List<string> _tmp = new List<string>(10);
            foreach(DataRow dr in dt.Rows)
            {
                _tmp.Add(string.Format(string.Format("{{0:{0}}}", FormatString), dr[0]));
            }
            return _tmp.Distinct().Take(sampleSize).ToList();
            
        }
    }
}
