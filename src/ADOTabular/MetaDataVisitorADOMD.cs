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
                    , new ADOTabularModel(_conn, dr["CUBE_NAME"].ToString(), dr["CUBE_CAPTION"].ToString(), dr["DESCRIPTION"].ToString(), dr["CUBE_NAME"].ToString()));
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
                        ,dr["DIMENSION_NAME"].ToString()
                        ,dr["DIMENSION_CAPTION"].ToString()
                        ,dr["DESCRIPTION"].ToString()
                        ,bool.Parse(dr["DIMENSION_IS_VISIBLE"].ToString())
                        , false // Private
                        , false // ShowAsVariationsOnly
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
                        , dr["HIERARCHY_CAPTION"].ToString()
                        ,dr["DESCRIPTION"].ToString()
                        ,bool.Parse(dr["HIERARCHY_IS_VISIBLE"].ToString())
                        ,ADOTabularObjectType.Column
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
                        , dr["MEASURE_CAPTION"].ToString()
                        ,dr["DESCRIPTION"].ToString()
                        ,bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString())
                        ,ADOTabularObjectType.Measure
                        ,"")
                        );
            }
            return ret;
        }

        public SortedDictionary<string, ADOTabularMeasure> Visit(ADOTabularMeasureCollection measures)
        {
            //RRomano: Better way to reuse this method in the two visitors? Create an abstract class of a visitor so that code can be shared (csdl doesnt seem to have the DAX expression)

            var ret = VisitMeasures(measures, this._conn);

            return ret;
        }

        internal static SortedDictionary<string, ADOTabularMeasure> VisitMeasures(ADOTabularMeasureCollection measures, IADOTabularConnection conn)
        {
            var ret = new SortedDictionary<string, ADOTabularMeasure>();

            var resCollMeasures = new AdomdRestrictionCollection
                {
                    {"CATALOG_NAME", conn.Database.Name},
                    {"CUBE_NAME", conn.Database.Models.BaseModel.Name},
                    {"MEASUREGROUP_NAME", string.Format("{0}", measures.Table.Caption)},
                    {
                        "MEASURE_VISIBILITY",
                        conn.ShowHiddenObjects
                            ? (int) (MdschemaVisibility.Visible | MdschemaVisibility.NonVisible)
                            : (int) (MdschemaVisibility.Visible)
                    }
                };

            DataTable dtMeasures = conn.GetSchemaDataSet("MDSCHEMA_MEASURES", resCollMeasures).Tables[0];

            foreach (DataRow dr in dtMeasures.Rows)
            {
                ret.Add(dr["MEASURE_NAME"].ToString()
                    , new ADOTabularMeasure(measures.Table
                        , dr["MEASURE_NAME"].ToString()
                        , dr["MEASURE_NAME"].ToString()
                        , dr["MEASURE_CAPTION"].ToString()
                        , dr["DESCRIPTION"].ToString()
                        , bool.Parse(dr["MEASURE_IS_VISIBLE"].ToString())
                        , dr["EXPRESSION"].ToString()
                        )
                        );
            }

            return ret;
        }

        public void Visit(ADOTabularFunctionGroupCollection functionGroups)
        {
            throw new System.NotImplementedException();
        }


        public void Visit(ADOTabularKeywordCollection aDOTabularKeywordCollection)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(MetadataInfo.DaxMetadata daxMetadata) {
            throw new System.NotImplementedException();
        }

    }

}
