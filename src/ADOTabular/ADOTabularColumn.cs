using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace ADOTabular
{


    public  class ADOTabularColumn:IADOTabularColumn
    {
        // TODO - can we delete this??
        //public ADOTabularColumn(ADOTabularTable table, DataRow dr, ADOTabularObjectType colType)
        //{
        //    Table = table;
        //    ObjectType = colType;
        //    if (colType == ADOTabularObjectType.Column)
        //    {
        //        Caption = dr["HIERARCHY_CAPTION"].ToString();
        //        Name = dr["HIERARCHY_NAME"].ToString();
        //        IsVisible = bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString());
        //        Description = dr["DESCRIPTION"].ToString();
        //    }
        //    else
        //    {
        //        Caption = dr["MEASURE_CAPTION"].ToString();
        //        Name = dr["MEASURE_NAME"].ToString();
        //        IsVisible = bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString());
        //        Description = dr["DESCRIPTION"].ToString();
        //    }
        //}

        public ADOTabularColumn( ADOTabularTable table, string internalReference, string name, string caption,  string description,
                                bool isVisible, ADOTabularObjectType columnType, string contents)
        {
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
                if ((this.MetadataImage != MetadataImages.Measure) && (this.MetadataImage != MetadataImages.HiddenMeasure))
                {
                    return null;
                }

                // Return the measure expression from the table measures dictionary 
                // (the measures are loaded and cached on the use of the Table.Measures property)

                var measure = this.Table.Measures.SingleOrDefault(s => s.Name.Equals(this.Name, StringComparison.OrdinalIgnoreCase));                

                return (measure != null ? measure.Expression : null);
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

            string qry = "";

            switch (Type.GetTypeCode(DataType))
            {
                case TypeCode.Boolean:
                    qry = string.Format("EVALUATE ROW(\"Min\", \"False\",\"Max\", \"True\", \"DistinctCount\", COUNTROWS(DISTINCT({0})) )", DaxName);
                    break;
                case TypeCode.Empty:
                    qry = string.Format("EVALUATE ROW(\"Min\", \"\",\"Max\", \"\", \"DistinctCount\", COUNTROWS(DISTINCT({0})) )", DaxName);
                    break;
                case TypeCode.String:
                    qry = string.Format("EVALUATE ROW(\"Min\", FIRSTNONBLANK({0},1),\"Max\", LASTNONBLANK({0},1), \"DistinctCount\", COUNTROWS(DISTINCT({0})) )", DaxName);
                    break;
                default:
                    qry = string.Format("EVALUATE ROW(\"Min\", MIN({0}),\"Max\", MAX({0}), \"DistinctCount\", DISTINCTCOUNT({0}) )", DaxName);
                    break;

            }
            
            var dt = connection.ExecuteDaxQueryDataTable(qry);
            
                MinValue = dt.Rows[0][0].ToString();
                MaxValue = dt.Rows[0][1].ToString();
            if (dt.Rows[0][2] == DBNull.Value) {
                DistinctValues = 0;
            }
            else { 
                DistinctValues = (long)dt.Rows[0][2];
            }
        }


        public List<string> GetSampleData(ADOTabularConnection connection, int sampleSize)
        {
            string qryTempalte = "EVALUATE SAMPLE({0}, ALL({1}), RAND()) ORDER BY {1}";
            if (connection.AllFunctions.Contains("TOPNSKIP"))
                qryTempalte = "EVALUATE TOPNSKIP({0}, 0, ALL({1}), RAND()) ORDER BY {1}";

            var qry = string.Format(qryTempalte, sampleSize * 2, DaxName);
            var dt = connection.ExecuteDaxQueryDataTable(qry);
            List<string> _tmp = new List<string>(sampleSize * 2);
            foreach(DataRow dr in dt.Rows)
            {
                _tmp.Add(string.Format(string.Format("{{0:{0}}}", FormatString), dr[0]));
            }
            return _tmp.Distinct().Take(sampleSize).ToList();
        }

        // used for relationship links
        public string Role { get; internal set; }
        public List<ADOTabularVariation> Variations { get; internal set; }
    }
}
