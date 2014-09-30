using System.Collections.Generic;
using System.Data;
using ADOTabular.AdomdClientWrappers;

namespace ADOTabular
{
    class MetaDataVisitorADOMD : IMetaDataVisitor
    {
        private readonly ADOTabularConnection _conn;

        public MetaDataVisitorADOMD(ADOTabularConnection conn) 
        {
            _conn = conn;
        }
        public SortedDictionary<string, ADOTabularModel> Visit(ADOTabularModelCollection models)
        {
            var ret = new SortedDictionary<string, ADOTabularModel>();
            var resColl = new AdomdRestrictionCollection { { "CUBE_SOURCE", 1 } };
            var dtModels = _conn.GetSchemaDataSet("MDSCHEMA_CUBES", resColl).Tables[0];
            foreach (DataRow dr in dtModels.Rows)
            {
                ret.Add(dr["CUBE_NAME"].ToString()
                    , new ADOTabularModel(_conn, dr["CUBE_NAME"].ToString(), dr["DESCRIPTION"].ToString(), dr["CUBE_NAME"].ToString()));
            }
            return ret;
        }

        public void Visit(ADOTabularTableCollection tables)
        {
            //var ret = new SortedDictionary<string, ADOTabularTable>();
            var resColl = new AdomdRestrictionCollection
                {
                    {"CUBE_NAME", tables.Model.Name },
                    {
                        "DIMENSION_VISIBILITY",
                        _conn.ShowHiddenObjects
                            ? (int) (MdschemaVisibility.Visible | MdschemaVisibility.NonVisible)
                            : (int) (MdschemaVisibility.Visible)
                    }
                };
            DataTable dtTables = _conn.GetSchemaDataSet("MDSCHEMA_DIMENSIONS", resColl).Tables[0];
            foreach (DataRow dr in dtTables.Rows)
            {
                tables.Add(
                    new ADOTabularTable(_conn, dr["DIMENSION_NAME"].ToString()
                        ,dr["DESCRIPTION"].ToString()
                        ,bool.Parse(dr["DIMENSION_IS_VISIBLE"].ToString())
                    )
                );
            }
            
        }

        public SortedDictionary<string,ADOTabularColumn> Visit(ADOTabularColumnCollection columns)
        {
            var ret = new SortedDictionary<string, ADOTabularColumn>();
            var resColl = new AdomdRestrictionCollection
                              {
                                  {"HIERARCHY_ORIGIN", 2},
                                  {"CUBE_NAME", string.Format("{0}", columns.Table.Model.Name)},
                                  {"DIMENSION_UNIQUE_NAME", string.Format("[{0}]", columns.Table.Caption)},
                                  {"HIERARCHY_VISIBILITY", _conn.ShowHiddenObjects ? (int)(MdschemaVisibility.Visible | MdschemaVisibility.NonVisible) : (int)(MdschemaVisibility.Visible)} 
                              };
            DataTable dtHier = _conn.GetSchemaDataSet("MDSCHEMA_HIERARCHIES", resColl).Tables[0];
            foreach (DataRow dr in dtHier.Rows)
            {
                ret.Add(dr[""].ToString()
                    , new ADOTabularColumn(columns.Table
                        ,dr["HIERARCHY_NAME"].ToString()
                        ,dr["HIERARCHY_NAME"].ToString()
                        ,dr["DESCRIPTION"].ToString()
                        ,bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString())
                        ,ADOTabularColumnType.Column
                        ,"")
                        );
            }
            var resCollMeasures = new AdomdRestrictionCollection
                {
                    {"CUBE_NAME", string.Format("{0}", columns.Table.Model.Name)},
                    {"MEASUREGROUP_NAME", string.Format("{0}", columns.Table.Caption)},
                    {
                        "MEASURE_VISIBILITY",
                        _conn.ShowHiddenObjects
                            ? (int) (MdschemaVisibility.Visible | MdschemaVisibility.NonVisible)
                            : (int) (MdschemaVisibility.Visible)
                    }
                };
            DataTable dtMeasures = _conn.GetSchemaDataSet("MDSCHEMA_MEASURES", resCollMeasures).Tables[0];
            foreach (DataRow dr in dtMeasures.Rows)
            {
                ret.Add(dr["MEASURE_NAME"].ToString()
                    , new ADOTabularColumn(columns.Table
                        ,dr["MEASURE_NAME"].ToString()
                        ,dr["MEASURE_NAME"].ToString()
                        ,dr["DESCRIPTION"].ToString()
                        ,bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString())
                        ,ADOTabularColumnType.Measure
                        ,"")
                        );
            }
            return ret;
        }

        public void Visit(ADOTabularFunctionGroupCollection functionGroups)
        {
            throw new System.NotImplementedException();
        }
    }

}
