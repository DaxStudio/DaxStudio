using System;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularColumn:IADOTabularObject
    {

        public ADOTabularColumn(ADOTabularTable table, DataRow dr, ADOTabularColumnType colType)
        {
            Table = table; 
            ColumnType = colType;
            if (colType == ADOTabularColumnType.Column)
            {
                Caption = dr["HIERARCHY_NAME"].ToString();
                IsVisible = bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString());
                Description = dr["DESCRIPTION"].ToString();
            }
            else
            {
                Caption = dr["MEASURE_NAME"].ToString();
                IsVisible = bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString());
                Description = dr["DESCRIPTION"].ToString();
            }
        }

        public ADOTabularColumn( ADOTabularTable table, string caption,  string description,
                                bool isVisible, ADOTabularColumnType columnType, string contents)
        {
            Table = table;
            Caption = caption;
            Description = description;
            IsVisible = isVisible;
            ColumnType = columnType;
            Contents = contents;
        }

        public ADOTabularColumnType ColumnType { get; private set; }

        public ADOTabularTable Table { get; private set; }

        public string Caption { get; private set; }

        public string Contents { get; private set; }

        public string DaxName {
            get
            {
                // for measures we exclude the table name
                return ColumnType == ADOTabularColumnType.Column  
                    ? string.Format("{0}[{1}]", Table.DaxName, Caption)
                    : string.Format("[{0}]",Caption);
            }
        }

        public string Description { get; private set; }

        public bool IsVisible { get; private set; }

        public Type DataType { get; set; }

        public MetadataImages MetadataImage
        {
            get
            {
                if (ColumnType == ADOTabularColumnType.Column)
                {
                    return IsVisible ? MetadataImages.Column : MetadataImages.HiddenColumn;
                }
                else
                {
                    return IsVisible ? MetadataImages.Measure : MetadataImages.HiddenMeasure;
                }
            }
        }
    }
}
