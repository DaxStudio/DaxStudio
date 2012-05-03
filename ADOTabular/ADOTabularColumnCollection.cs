using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections;
using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular
{
    public enum ADOTabularColumnType
    {
        Column,
        Measure
    }

    public class ADOTabularColumnCollection: IEnumerable<ADOTabularColumn>,IEnumerable
    {
        private ADOTabularTable _table;
        private ADOTabularConnection _adoTabConn;
        public ADOTabularColumnCollection(ADOTabularConnection adoTabConn, ADOTabularTable table)
        {
            _table = table;
            _adoTabConn = adoTabConn;
        }

        private DataTable GetColumnsTable()
        {
            /*            
select *
from $system.mdschema_hierarchies
where hierarchy_origin = 2
and cube_name = 'Model'
and [Dimension_unique_name] = '[Product]'
             */
            AdomdRestrictionCollection resColl = new AdomdRestrictionCollection();
            resColl.Add("HIERARCHY_ORIGIN", 2);
            resColl.Add("CUBE_NAME", string.Format("{0}",_table.Model.Name));
            resColl.Add("DIMENSION_UNIQUE_NAME",string.Format("[{0}]", _table.Name));
            return _adoTabConn.GetSchemaDataSet("MDSCHEMA_HIERARCHIES", resColl).Tables[0];
        }

        private DataTable GetMeasuresTable()
        {
            AdomdRestrictionCollection resColl = new AdomdRestrictionCollection();
            resColl.Add("CUBE_NAME", string.Format("{0}",_table.Model.Name));
            resColl.Add("MEASUREGROUP_NAME",string.Format("{0}", _table.Name));
            return _adoTabConn.GetSchemaDataSet("MDSCHEMA_MEASURES", resColl).Tables[0];
        }

        public IEnumerator<ADOTabularColumn> GetEnumerator()
        {
            // Add attributes as columns
            foreach (DataRow dr in GetColumnsTable().Rows)
            {
                //if (dr["COLUMN_NAME"] != "RowNumber")
                //{
                yield return new ADOTabularColumn(_adoTabConn, _table, dr,ADOTabularColumnType.Column);
                //}
            }
            // Add measures to column collection
            foreach (DataRow dr in GetMeasuresTable().Rows)
            {
                yield return new ADOTabularColumn(_adoTabConn, _table, dr, ADOTabularColumnType.Measure);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
