using System.Collections.Generic;
using System.Data;
using System.Collections;
using System.Linq;
//using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.AdomdClientWrappers;

namespace ADOTabular
{
    public enum ADOTabularColumnType
    {
        Column,
        Measure
    }

    public class ADOTabularColumnCollection: IEnumerable<ADOTabularColumn>
    {
        private readonly ADOTabularTable _table;
        private readonly ADOTabularConnection _adoTabConn;
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
            var resColl = new AdomdRestrictionCollection
                              {
                                  {"HIERARCHY_ORIGIN", 2},
                                  {"CUBE_NAME", string.Format("{0}", _table.Model.Name)},
                                  {"DIMENSION_UNIQUE_NAME", string.Format("[{0}]", _table.Caption)},
                                  {"HIERARCHY_VISIBILITY", _adoTabConn.ShowHiddenObjects ? (int)(MdschemaVisibility.Visible | MdschemaVisibility.NonVisible) : (int)(MdschemaVisibility.Visible)} 
                              };
            return _adoTabConn.GetSchemaDataSet("MDSCHEMA_HIERARCHIES", resColl).Tables[0];
        }

        private DataTable GetMeasuresTable()
        {
            var resColl = new AdomdRestrictionCollection
                              {
                                  {"CUBE_NAME", string.Format("{0}", _table.Model.Name)},
                                  {"MEASUREGROUP_NAME", string.Format("{0}", _table.Caption)},
                                  {"MEASURE_VISIBILITY", _adoTabConn.ShowHiddenObjects ? (int)(MdschemaVisibility.Visible | MdschemaVisibility.NonVisible) : (int)(MdschemaVisibility.Visible)} 
                              };
            return _adoTabConn.GetSchemaDataSet("MDSCHEMA_MEASURES", resColl).Tables[0];
        }

        public IEnumerator<ADOTabularColumn> GetEnumerator()
        {
            // Add attributes as columns
            foreach (var dr in GetColumnsTable().Rows.Cast<DataRow>().Where(dr => dr["HIERARCHY_NAME"].ToString() != "RowNumber"))
            {
                yield return new ADOTabularColumn(_table, dr,ADOTabularColumnType.Column);
            }
            // Add measures to column collection
            foreach (var dr in GetMeasuresTable().Rows.Cast<DataRow>().Where(dr => dr["MEASURE_NAME"].ToString() != string.Format("_Count {0}",_table.Caption)))
            {
                yield return new ADOTabularColumn(_table, dr, ADOTabularColumnType.Measure);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
