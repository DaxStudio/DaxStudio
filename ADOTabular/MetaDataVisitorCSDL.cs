using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml;
using ADOTabular.AdomdClientWrappers;

namespace ADOTabular
{
    class MetaDataVisitorCSDL : IMetaDataVisitor
    {
        private readonly ADOTabularConnection _conn;

        public MetaDataVisitorCSDL(ADOTabularConnection conn)
        {
            _conn = conn;
        }
        
        Dictionary<string, ADOTabularModel> IMetaDataVisitor.Visit(ADOTabularModelCollection models)
        {
            var ret = new Dictionary<string, ADOTabularModel>();
            var resColl = new AdomdRestrictionCollection { { "CUBE_SOURCE", 1 } };
            var dtModels = _conn.GetSchemaDataSet("MDSCHEMA_CUBES", resColl).Tables[0];
            foreach (DataRow dr in dtModels.Rows)
            {
                ret.Add(dr["CUBE_NAME"].ToString()
                    , new ADOTabularModel(_conn, dr["CUBE_NAME"].ToString(), dr["DESCRIPTION"].ToString(), dr["BASE_CUBE_NAME"].ToString()));
            }
            return ret;
        }

        Dictionary<string, ADOTabularTable> IMetaDataVisitor.Visit(ADOTabularTableCollection tables)
        {
            var resColl = new AdomdRestrictionCollection { { "CATALOG_NAME", _conn.Database.Name } };
            // restrict the metadata to the selected perspective
            if (tables.Model.IsPerspective)
                resColl.Add(new AdomdRestriction("PERSPECTIVE_NAME",tables.Model.Name));
            // if we are SQL 2012 SP1 or greater ask for v1.1 of the Metadata (includes KPI & Hierarchy information)
            if (_conn.ServerVersion.VersionGreaterOrEqualTo("11.0.3000.0"))
                resColl.Add(new AdomdRestriction("VERSION", "1.1"));
            var tabs = new Dictionary<string, ADOTabularTable>();
            var ds = _conn.GetSchemaDataSet("DISCOVER_CSDL_METADATA", resColl);
            string csdl = ds.Tables[0].Rows[0]["Metadata"].ToString();
            XmlReader rdr = new XmlTextReader( new StringReader(csdl) );
            if (rdr.NameTable != null)
            {
                var eEntitySet = rdr.NameTable.Add("EntitySet" );
                var eEntityType = rdr.NameTable.Add("EntityType");
                var eProperty = rdr.NameTable.Add("Property");
                var eMeasure = rdr.NameTable.Add("Measure");
            
                while (rdr.Read())
                {
                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == eEntitySet)
                    {
                        var tab = BuildTableFromEntitySet(rdr, eEntitySet);
                        tabs.Add(tab.Caption, tab);
                    }
                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == eEntityType)
                    {
                        AddColumnsToTable(rdr,tabs, eEntityType, eProperty,eMeasure);
                    }
                }
            }

            return tabs;
        }

        

        private ADOTabularTable BuildTableFromEntitySet(XmlReader rdr, string eEntitySet)
        {
            string caption = "";
            string description = "";
            bool isVisible = true;
            string daxname = "";
            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == eEntitySet))
            {
                while (rdr.MoveToNextAttribute())
                {
                    switch (rdr.LocalName)
                    {
                        case "Caption":
                            caption = rdr.Value;
                            break;
                        case "Description":
                            description = rdr.Value;
                            break;
                        case "Hidden":
                            isVisible = !bool.Parse(rdr.Value);
                            break;
                        case "Name":
                            daxname = rdr.Value;
                            break;
                    }
                }
                rdr.Read();
            }
            if (caption.Length == 0)
                caption = daxname;
            var tab = new ADOTabularTable(_conn, caption, description, isVisible);
            tab.InternalId = daxname;
            return tab;
        }

        private void AddColumnsToTable(XmlReader rdr, Dictionary<string,ADOTabularTable> tables, string eEntityType, string eProperty, string eMeasure )
        {
            string caption = "";
            string description = "";
            bool isVisible = true;
            string daxName = "";
            string tableId = "";
            string dataType = "";
            string contents = "";

            var colType = ADOTabularColumnType.Column;
            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == eEntityType))
            {
                if (rdr.NodeType == XmlNodeType.Element && rdr.LocalName == eEntityType)
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                            case "Name":
                                tableId = rdr.Value;
                                break;
                        }
                    }
                }

                if (rdr.NodeType == XmlNodeType.Element && (rdr.LocalName == eProperty || rdr.LocalName == eMeasure))
                {
                    
                    if (rdr.LocalName == eMeasure)
                        colType = ADOTabularColumnType.Measure;

                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                            case "Name":
                                daxName = rdr.Value;
                                break;
                            case "Type":
                                dataType = rdr.Value;
                                break;
                            case "Caption":
                                caption = rdr.Value;
                                break;
                            case "Contents":
                                contents = rdr.Value;
                                break;
                            case "Hidden":
                                isVisible = !bool.Parse(rdr.Value);
                                break;
                            case "Description":
                                description = rdr.Value;
                                break;
                                // Precision Scale FormatString
                            //DefaultAggregateFunction
                        }
                    }

                }

                if (rdr.NodeType == XmlNodeType.EndElement & rdr.LocalName == eProperty & rdr.Name == "Property")
                {
                    
                    if (caption.Length == 0)
                        caption = daxName;
                    var tab = GetTableById(tables, tableId);
                    var col = new ADOTabularColumn(tab, caption, description, isVisible, colType, contents);
                    col.DataType = Type.GetType( string.Format("System.{0}",dataType));
                    tab.Columns.Add(col); 

                    // reset temp variables
                    caption = "";
                    description = "";
                    isVisible = true;
                    daxName = "";
                    contents = "";
                    dataType = "";
                    colType = ADOTabularColumnType.Column;
                }
                rdr.Read();
            }
            
        }


        private ADOTabularTable GetTableById(Dictionary<string, ADOTabularTable> tables, string tableId)
        {
            foreach (var t in tables.Values)
            {
                if (t.InternalId == tableId)
                {
                    return t;
                }
            }
            return null;
        }

        Dictionary<string, ADOTabularColumn> IMetaDataVisitor.Visit(ADOTabularColumnCollection columns)
        {
            return new Dictionary<string, ADOTabularColumn>();
        }

        public void Visit(ADOTabularFunctionGroupCollection functionGroups)
        {
            DataRow[] drFuncs = _conn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS").Tables[0].Select("ORIGIN=3 OR ORIGIN=4");
            foreach (DataRow dr in drFuncs)
            {
                functionGroups.AddFunction(dr);
            }
        }
    }

}
