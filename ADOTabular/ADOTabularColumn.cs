using System.Data;

namespace ADOTabular
{
    public class ADOTabularColumn
    {
        private readonly ADOTabularTable _table;
        private readonly string _columnCaption;
        private readonly string _columnName;
        private readonly bool _visible;
        private readonly ADOTabularColumnType _type;
        public ADOTabularColumn(ADOTabularTable table, DataRow dr, ADOTabularColumnType colType)
        {
            _table = table;
            _type = colType;
            if (colType == ADOTabularColumnType.Column)
            {
                _columnCaption = dr["HIERARCHY_NAME"].ToString();
                _columnName = string.Format("'{0}'[{1}]", table.Caption, _columnCaption);
                _visible = bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString());
            }
            else
            {
                _columnCaption = dr["MEASURE_NAME"].ToString();
                _columnName = string.Format("'{0}'[{1}]", table.Caption, _columnCaption);
                _visible = bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString());
            }
        }

        public ADOTabularColumnType Type
        {
            get { return _type; }
        }

        public ADOTabularTable Table
        {
            get { return _table; }
        }

        public string Caption
        {
            get { return _columnCaption; }
        }
        public string Name
        {
            get { return _columnName; }
        }

        public bool IsVisible
        {
            get { return _visible; }
        }
    }
}
