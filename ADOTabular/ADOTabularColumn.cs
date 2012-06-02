using System.Data;

namespace ADOTabular
{
    public class ADOTabularColumn
    {
        public ADOTabularColumn(ADOTabularTable table, DataRow dr, ADOTabularColumnType colType)
        {
            Table = table;
            Type = colType;
            if (colType == ADOTabularColumnType.Column)
            {
                Caption = dr["HIERARCHY_NAME"].ToString();
                Name = string.Format("'{0}'[{1}]", table.Caption, Caption);
                IsVisible = bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString());
                Description = dr["DESCRIPTION"].ToString();
            }
            else
            {
                Caption = dr["MEASURE_NAME"].ToString();
                Name = string.Format("'{0}'[{1}]", table.Caption, Caption);
                IsVisible = bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString());
                Description = dr["DESCRIPTION"].ToString();
            }
        }

        public ADOTabularColumnType Type { get; private set; }

        public ADOTabularTable Table { get; private set; }

        public string Caption { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public bool IsVisible { get; private set; }
    }
}
