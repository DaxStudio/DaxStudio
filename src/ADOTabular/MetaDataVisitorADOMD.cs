using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Interfaces;

namespace ADOTabular
{
    public class MetaDataVisitorADOMD : IMetaDataVisitor
    {
        private readonly IADOTabularConnection _conn;

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
                    , new ADOTabularModel(_conn, models.Database, dr["CUBE_NAME"].ToString(), dr["CUBE_CAPTION"].ToString(), dr["DESCRIPTION"].ToString(), dr["CUBE_NAME"].ToString()));
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
                            : (int) MdschemaVisibility.Visible
                    }
                };
            DataTable dtTables = _conn.GetSchemaDataSet("MDSCHEMA_DIMENSIONS", resColl).Tables[0];
            foreach (DataRow dr in dtTables.Rows)
            {
                tables.Add(
                    new ADOTabularTable(_conn
                        ,tables.Model
                        ,dr["DIMENSION_NAME"].ToString()
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
                                  {"CUBE_NAME",  columns.Table.Model.Name},
                                  {"DIMENSION_UNIQUE_NAME", $"[{columns.Table.Caption}"},
                                  {"HIERARCHY_VISIBILITY", _conn.ShowHiddenObjects ? (int)(MdschemaVisibility.Visible | MdschemaVisibility.NonVisible) : (int)MdschemaVisibility.Visible} 
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
                    {"CUBE_NAME",  columns.Table.Model.Name},
                    {"MEASUREGROUP_NAME", columns.Table.Caption},
                    {
                        "MEASURE_VISIBILITY",
                        _conn.ShowHiddenObjects
                            ? (int) (MdschemaVisibility.Visible | MdschemaVisibility.NonVisible)
                            : (int) MdschemaVisibility.Visible
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
            // need to check if the DMV collection has the TMSCHEMA_MEASURES view, 
            // and if this is a connection with admin rights
            // and if it is not a PowerPivot model (as they seem to throw an error about the model needing to be in the "new" tabular mode)
            if (conn.DynamicManagementViews.Any(dmv => dmv.Name == "TMSCHEMA_MEASURES") && conn.IsAdminConnection && !conn.IsPowerPivot) return GetTmSchemaMeasures(measures, conn);
            return GetMdSchemaMeasures(measures, conn);
        }

        private static SortedDictionary<string, ADOTabularMeasure> GetMdSchemaMeasures(ADOTabularMeasureCollection measures, IADOTabularConnection conn)
        {
            var ret = new SortedDictionary<string, ADOTabularMeasure>();

            var resCollMeasures = new AdomdRestrictionCollection
                {
                    {"CATALOG_NAME", conn.Database.Name},
                    {"CUBE_NAME", conn.Database.Models.BaseModel.Name},
                    {"MEASUREGROUP_NAME",  measures.Table.Name},
                    {
                        "MEASURE_VISIBILITY",
                        conn.ShowHiddenObjects
                            ? (int) (MdschemaVisibility.Visible | MdschemaVisibility.NonVisible)
                            : (int) MdschemaVisibility.Visible
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
                        , null
                        )
                        );
            }

            return ret;
        }

        private static SortedDictionary<string, ADOTabularMeasure> GetTmSchemaMeasures(ADOTabularMeasureCollection measures, IADOTabularConnection conn)
        {
            var resCollTables = new AdomdRestrictionCollection
                {
                    {"Name",  measures.Table.Name},
                };

            // need to look up the TableID in TMSCHEMA_TABLES
            DataTable dtTables = conn.GetSchemaDataSet("TMSCHEMA_TABLES", resCollTables).Tables[0];
            var tableId = dtTables.Rows[0].Field<ulong>("ID");

            var ret = new SortedDictionary<string, ADOTabularMeasure>();
            var resCollMeasures = new AdomdRestrictionCollection
                {
                    {"DatabaseName", conn.Database.Name},
                    {"TableID",  tableId},
                };

            if (!conn.ShowHiddenObjects) resCollMeasures.Add(new AdomdRestriction("IsHidden", false));

            // then get all the measures for the current table
            DataTable dtMeasures = conn.GetSchemaDataSet("TMSCHEMA_MEASURES", resCollMeasures).Tables[0];
            const string COLUMNNAME_FormatStringDefinitionID = "FormatStringDefinitionID";
            bool hasFormatStringId = dtMeasures.Columns.Contains(COLUMNNAME_FormatStringDefinitionID);
            
            // Add format string definitions if available
            DataTable dtFormatStringDefinitions = null;
            int.TryParse(conn.Database.CompatibilityLevel, out int iCompatLevel);
            // FormatString definitions are only available in compat level 1470 or above
            if (conn.DynamicManagementViews.Any(dmv => dmv.Name == "TMSCHEMA_FORMAT_STRING_DEFINITIONS") && iCompatLevel >= 1470)
            {
                // TMSCHEMA_FORMAT_STRING_DEFINITIONS only gets DatabaseName as restriction
                dtFormatStringDefinitions = conn.GetSchemaDataSet("TMSCHEMA_FORMAT_STRING_DEFINITIONS", new AdomdRestrictionCollection
                {
                    {"DatabaseName", conn.Database.Name}
                }).Tables[0];
            }

            foreach (DataRow dr in dtMeasures.Rows)
            {
                string formatStringExpression = null;

                // Add format string if available
                if (dtFormatStringDefinitions != null && hasFormatStringId)
                {
                    ulong? id = dr[COLUMNNAME_FormatStringDefinitionID] as ulong?;
                    if (id.HasValue && id > 0)
                    {
                        var formatStrings = dtFormatStringDefinitions.Select($"ID = {id}");
                        if (formatStrings.Length > 0)
                        {
                            formatStringExpression = formatStrings[0]["Expression"].ToString();
                        }
                    }
                }

                ret.Add(dr["Name"].ToString()
                    , new ADOTabularMeasure(measures.Table
                        , dr["Name"].ToString()
                        , dr["Name"].ToString()
                        , dr["Name"].ToString() // TODO - TMSCHEMA_MEASURES does not have a caption property
                        , dr["Description"].ToString()
                        , !bool.Parse(dr["IsHidden"].ToString())
                        , dr["Expression"].ToString()
                        , formatStringExpression
                        )
                        );
            }

            return ret;
        }

        public void Visit(ADOTabularFunctionGroupCollection functionGroups)
        {
            throw new System.NotImplementedException();
        }


        public void Visit(ADOTabularKeywordCollection keywords)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(MetadataInfo.DaxMetadata daxMetadata) {
            throw new System.NotImplementedException();
        }

        public void Visit(MetadataInfo.DaxColumnsRemap daxColumnsRemap)
        {
            throw new System.NotImplementedException();
        }

        public void Visit(MetadataInfo.DaxTablesRemap daxTablesRemap)
        {
            throw new System.NotImplementedException();
        }

        public ADOTabularDatabase Visit(ADOTabularConnection conn)
        {
            return null;
        }

        public void Visit(ADOTabularCalendarCollection calendars)
        {
            /* Do Nothing */
        }
    }

}
