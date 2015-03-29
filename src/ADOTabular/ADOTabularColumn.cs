using System;
using System.Data;

namespace ADOTabular
{


    public  class ADOTabularColumn:IADOTabularObject
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

        public string Description { get; private set; }

        public bool IsVisible { get; private set; }
 
        public Type DataType { get; set; }

        public string DataTypeName { get { return DataType==null?"n/a":DataType.ToString().Replace("System.", ""); } }

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
                    default:
                        return IsVisible ? MetadataImages.Measure : MetadataImages.HiddenMeasure;
                
                }
            }
        }
    }
}
