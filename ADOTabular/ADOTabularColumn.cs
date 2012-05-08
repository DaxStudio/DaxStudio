using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public class ADOTabularColumn
    {
        private ADOTabularConnection _adoTabConn;
        private ADOTabularTable _table;
        private string _columnCaption;
        private string _columnName;
        private bool _visible;
        private ADOTabularColumnType _type;
        public ADOTabularColumn(ADOTabularConnection adoTabConn, ADOTabularTable table, DataRow dr, ADOTabularColumnType colType)
        {
            _adoTabConn = adoTabConn;
            _table = table;
            _type = colType;
            if (colType == ADOTabularColumnType.Column)
            {
                _columnCaption = dr["HIERARCHY_NAME"].ToString();
                _columnName = string.Format("'{0}'[{1}]", table.Name, _columnCaption);
                _visible = bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString());
            }
            else
            {
                _columnCaption = dr["MEASURE_NAME"].ToString();
                _columnName = string.Format("'{0}'[{1}]", table.Name, _columnCaption);
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
