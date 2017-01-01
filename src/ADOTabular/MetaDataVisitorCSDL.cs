using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml;
using ADOTabular.AdomdClientWrappers;
using System.Linq;
using System.Xml.Linq;

namespace ADOTabular
{
    public class MetaDataVisitorCSDL : IMetaDataVisitor
    {
        private readonly ADOTabularConnection _conn;
        private Dictionary<string, Dictionary<string, string>> _hierStructure;

        public MetaDataVisitorCSDL(ADOTabularConnection conn)
        {
            _conn = conn;
        }

        SortedDictionary<string, ADOTabularModel> IMetaDataVisitor.Visit(ADOTabularModelCollection models)
        {
            var ret = new SortedDictionary<string, ADOTabularModel>();
            var resColl = new AdomdRestrictionCollection { { "CUBE_SOURCE", 1 } };
            var dtModels = _conn.GetSchemaDataSet("MDSCHEMA_CUBES", resColl).Tables[0];
            foreach (DataRow dr in dtModels.Rows)
            {
                ret.Add(dr["CUBE_NAME"].ToString()
                    , new ADOTabularModel(_conn, dr["CUBE_NAME"].ToString(), dr["CUBE_CAPTION"].ToString(), dr["DESCRIPTION"].ToString(), dr["BASE_CUBE_NAME"].ToString()));
            }
            return ret;
        }

        void IMetaDataVisitor.Visit(ADOTabularTableCollection tables)
        {
            var resColl = new AdomdRestrictionCollection { { "CATALOG_NAME", _conn.Database.Name } };
            // restrict the metadata to the selected perspective
            if (tables.Model.IsPerspective)
                resColl.Add(new AdomdRestriction("PERSPECTIVE_NAME", tables.Model.Name));
            // if we are SQL 2012 SP1 or greater ask for v1.1 of the Metadata (includes KPI & Hierarchy information)

            if (_conn.ServerVersion.VersionGreaterOrEqualTo("11.0.3368.0"))
                resColl.Add(new AdomdRestriction("VERSION", "2.0"));
            else if (_conn.ServerVersion.VersionGreaterOrEqualTo("11.0.3000.0")
                || (_conn.IsPowerPivot && _conn.ServerVersion.VersionGreaterOrEqualTo("11.0.2830.0")))
                resColl.Add(new AdomdRestriction("VERSION", "1.1"));
            var ds = _conn.GetSchemaDataSet("DISCOVER_CSDL_METADATA", resColl);
            string csdl = ds.Tables[0].Rows[0]["Metadata"].ToString();

            /*
            //  debug code
            using (StreamWriter outfile = new StreamWriter( @"d:\data\csdl.xml"))
            {
                outfile.Write(csdl);
            }
            */

            // get hierarchy structure
            var hierResCol = new AdomdRestrictionCollection { { "CATALOG_NAME", _conn.Database.Name }, { "CUBE_NAME", _conn.Database.Models.BaseModel.Name }, { "HIERARCHY_VISIBILITY", 3 } };
            var dsHier = _conn.GetSchemaDataSet("MDSCHEMA_HIERARCHIES", hierResCol);

            _hierStructure = new Dictionary<string, Dictionary<string, string>>();
            foreach (DataRow row in dsHier.Tables[0].Rows)
            {
                var dimUName = row["DIMENSION_UNIQUE_NAME"].ToString();
                var dimName = dimUName.Substring(1, dimUName.Length - 2); // remove square brackets
                var hierName = row["HIERARCHY_NAME"].ToString();
                Dictionary<string, string> hd;
                if (!_hierStructure.ContainsKey(dimName))
                {
                    hd = new Dictionary<string, string>();

                    _hierStructure.Add(dimName, hd);
                }
                else
                {
                    hd = _hierStructure[dimName];
                }
                hd.Add(hierName, row["STRUCTURE_TYPE"].ToString());
            }

            using (XmlReader rdr = new XmlTextReader(new StringReader(csdl)))
            {
                GenerateTablesFromXmlReader(tables, rdr);
            }
        }

        public void GenerateTablesFromXmlReader(ADOTabularTableCollection tabs, XmlReader rdr)
        {
            // clear out the flat cache of column names
            _conn.Columns.Clear();
            
            if (rdr.NameTable != null)
            {
                var eEntitySet = rdr.NameTable.Add("EntitySet");
                var eEntityType = rdr.NameTable.Add("EntityType");

                //var eKpi = rdr.NameTable.Add("Kpi");

                while (rdr.Read())
                {
                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == eEntitySet)
                    {
                        var tab = BuildTableFromEntitySet(rdr, eEntitySet);
                        tabs.Add(tab);
                    }
                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == eEntityType)
                    {
                        AddColumnsToTable(rdr, tabs, eEntityType);
                    }
                }
                foreach (var t in tabs)
                {
                    TagKpiComponentColumns(t);
                }
            }

        }



        private ADOTabularTable BuildTableFromEntitySet(XmlReader rdr, string eEntitySet)
        {
            string caption = null;
            string description = "";
            string refname = null;
            bool isVisible = true;
            string name = null;
            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == eEntitySet))
            {
                while (rdr.MoveToNextAttribute())
                {
                    switch (rdr.LocalName)
                    {
                        case "ReferenceName":
                            name = rdr.Value;
                            break;
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
                            refname = rdr.Value;
                            break;
                    }
                }
                rdr.Read();
            }

            // the names of the properties in the CSDL metadata are somewhat confusing
            // Name           - cannot contain spaces and is used for internally referencing
            //                - maps to InternalReference in ADOTabular
            // Reference Name - this is the name used by DAX queries/expressions 
            //                - will be blank if Name does not contain spaces
            //                - if this is missing the Name property is used
            // Caption        - this is what the end user sees (may be translated)
            //                - if this is missing the Name property is used
            var tab = new ADOTabularTable(_conn, refname, name, caption, description, isVisible);

            return tab;
        }

        private void TagKpiComponentColumns(ADOTabularTable tab)
        {
            List<ADOTabularColumn> invalidKpis = new List<ADOTabularColumn>();

            foreach (var c in tab.Columns)
            {
                if (c.ColumnType == ADOTabularColumnType.KPI)
                {
                    var k = (ADOTabularKpi)c;
                    if (k.Goal == null && k.Status == null)
                        invalidKpis.Add(c);
                    if (k.Goal != null)
                        k.Goal.ColumnType = ADOTabularColumnType.KPIGoal;
                    if (k.Status != null)
                        k.Status.ColumnType = ADOTabularColumnType.KPIStatus;
                }
            }
            foreach (var invalidKpi in invalidKpis)
            {
                tab.Columns.Remove(invalidKpi);
                
                if (!tab.Measures.ContainsKey(invalidKpi.Name))
                {
                    var newMeasure = new ADOTabularMeasure(tab, invalidKpi.InternalReference, invalidKpi.Name, invalidKpi.Caption, invalidKpi.Description, invalidKpi.IsVisible, invalidKpi.MeasureExpression);
                    tab.Measures.Add(newMeasure);
                }
            }
        }

        private void AddColumnsToTable(XmlReader rdr
            , ADOTabularTableCollection tables
            , string eEntityType)
        {
            var eProperty = rdr.NameTable.Add("Property");
            var eMeasure = rdr.NameTable.Add("Measure");
            var eSummary = rdr.NameTable.Add("Summary");
            var eStatistics = rdr.NameTable.Add("Statistics");
            var eMinValue = rdr.NameTable.Add("MinValue");
            var eMaxValue = rdr.NameTable.Add("MaxValue");

            // this routine effectively processes and <EntityType> element and it's children
            string caption = "";
            string description = "";
            bool isVisible = true;
            string name = null;
            string refName = "";
            string tableId = "";
            string dataType = "";
            string contents = "";
            string minValue = "";
            string maxValue = "";
            string formatString = "";
            string defaultAggregateFunction = "";
            long stringValueMaxLength = 0;
            long distinctValueCount = 0;
            bool nullable = true;

            KpiDetails kpi = new KpiDetails();

            var colType = ADOTabularColumnType.Column;
            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == eEntityType))
            {
                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == eEntityType)
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

                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "Hierarchy")
                {
                    ProcessHierarchy(rdr, tables.GetById(tableId), eEntityType);

                }


                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "Kpi")
                {
                    kpi = ProcessKpi(rdr, tables.GetById(tableId));
                }

                if (rdr.NodeType == XmlNodeType.Element
                    && (rdr.LocalName == eProperty
                    || rdr.LocalName == eMeasure
                    || rdr.LocalName == eSummary
                    || rdr.LocalName == eStatistics
                    || rdr.LocalName == eMinValue
                    || rdr.LocalName == eMaxValue))
                {

                    if (rdr.LocalName == eMeasure)
                        colType = ADOTabularColumnType.Measure;

                    if (rdr.LocalName == eSummary)
                        description = rdr.ReadElementContentAsString();

                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                            case "Name":
                                refName = rdr.Value;
                                break;
                            case "ReferenceName":  // reference name will always come after the Name and will override it if present
                                name = rdr.Value;
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
                            case "DistinctValueCount":
                                distinctValueCount = long.Parse(rdr.Value);
                                break;
                            case "StringValueMaxLength":
                                stringValueMaxLength = long.Parse(rdr.Value);
                                break;
                            case "FormatString":
                                formatString = rdr.Value;
                                break;
                            case "DefaultAggregateFunction":
                                defaultAggregateFunction = rdr.Value;
                                break;
                            case "Nullable":
                                nullable = bool.Parse(rdr.Value);
                                break;
                                // Precision Scale 

                        }
                    }

                }



                if (rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == eProperty
                    && rdr.LocalName == "Property")
                {

                    if (caption.Length == 0)
                        caption = refName;
                    if (!string.IsNullOrWhiteSpace(caption))
                    {
                        var tab = tables.GetById(tableId);
                        if (kpi.IsBlank())
                        {
                            var col = new ADOTabularColumn(tab, refName, name, caption, description, isVisible, colType, contents);
                            col.DataType = Type.GetType(string.Format("System.{0}", dataType));
                            col.Nullable = nullable;
                            col.MinValue = minValue;
                            col.MaxValue = maxValue;
                            col.DistinctValueCount = distinctValueCount;
                            col.FormatString = formatString;
                            col.StringValueMaxLength = stringValueMaxLength;
                            tab.Columns.Add(col);
                            _conn.Columns.Add(col.OutputColumnName, col);
                        }
                        else
                        {
                            colType = ADOTabularColumnType.KPI;
                            var kpiCol = new ADOTabularKpi(tab, refName, name, caption, description, isVisible, colType, contents, kpi);
                            kpiCol.DataType = Type.GetType(string.Format("System.{0}", dataType));
                            tab.Columns.Add(kpiCol);
                            _conn.Columns.Add(kpiCol.OutputColumnName, kpiCol);
                        }
                    }


                    // reset temp variables
                    kpi = new KpiDetails();
                    refName = "";
                    caption = "";
                    name = null;
                    description = "";
                    isVisible = true;
                    contents = "";
                    dataType = "";
                    stringValueMaxLength = -1;
                    formatString = "";
                    defaultAggregateFunction = "";
                    nullable = true;
                    colType = ADOTabularColumnType.Column;
                }
                rdr.Read();
            }

            //TODO - link up back reference to backing measures for KPIs

        }


        private KpiDetails ProcessKpi(XmlReader rdr, ADOTabularTable table)
        {
            KpiDetails kpi = new KpiDetails();
            while (!(rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "Kpi"))
            {
                while (rdr.MoveToNextAttribute())
                {
                    if (rdr.LocalName == "StatusGraphic")
                    {
                        kpi.Graphic = rdr.Value;
                    }
                }
                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "KpiGoal")
                {
                    while (!(rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "KpiGoal"))
                    {
                        if (rdr.NodeType == XmlNodeType.Element
                            && rdr.LocalName == "PropertyRef")
                        {
                            while (rdr.MoveToNextAttribute())
                            {
                                if (rdr.LocalName == "Name")
                                {
                                    kpi.Goal = rdr.Value;
                                }
                            }
                        }
                        rdr.Read();
                    }
                }


                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "KpiStatus")
                {
                    while (!(rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "KpiStatus"))
                    {
                        if (rdr.NodeType == XmlNodeType.Element
                            && rdr.LocalName == "PropertyRef")
                        {
                            while (rdr.MoveToNextAttribute())
                            {
                                if (rdr.LocalName == "Name")
                                {
                                    kpi.Status = rdr.Value;
                                }
                            }
                        }
                        rdr.Read();
                    }
                }

                rdr.Read();
            }
            return kpi;
        }

        private void ProcessHierarchy(XmlReader rdr, ADOTabularTable table, string eEntityType)
        {
            var hierName = "";
            string hierCap = null;
            var hierHidden = false;
            ADOTabularHierarchy hier = null;
            ADOTabularLevel lvl = null;
            string lvlName = "";
            string lvlCaption = "";
            string lvlRef = "";

            while (!(rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "Hierarchy"))
            {
                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "Hierarchy")
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                            case "Hidden":
                                hierHidden = bool.Parse(rdr.Value);
                                break;
                            case "Name":
                                hierName = rdr.Value;
                                break;
                            case "Caption":
                                hierCap = rdr.Value;
                                break;
                        }
                    }
                    string structure = GetHierarchStructure(table, hierName, hierCap);
                    hier = new ADOTabularHierarchy(table, hierName, hierName, hierCap ?? hierName, "", hierHidden, ADOTabularColumnType.Hierarchy, "", structure);
                    table.Columns.Add(hier);
                    rdr.Read();
                }

                while (!(rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "Level"))
                {
                    if ((rdr.NodeType == XmlNodeType.Element)
                        && (rdr.LocalName == "Level"))
                    {
                        while (rdr.MoveToNextAttribute())
                        {
                            switch (rdr.LocalName)
                            {
                                case "Name":
                                    lvlName = rdr.Value;
                                    break;
                                case "Caption":
                                    lvlCaption = rdr.Value;
                                    break;
                            }
                        }
                    }

                    if ((rdr.NodeType == XmlNodeType.Element)
                        && (rdr.LocalName == "PropertyRef"))
                    {
                        while (rdr.MoveToNextAttribute())
                        {
                            switch (rdr.LocalName)
                            {
                                case "Name":
                                    lvlRef = rdr.Value;
                                    break;
                            }
                        }
                    }

                    rdr.Read();
                } //End of Level
                    
                lvl = new ADOTabularLevel(table.Columns.GetByPropertyRef(lvlRef));
                lvl.LevelName = lvlName;
                lvl.Caption = lvlCaption;
                hier.Levels.Add(lvl);
                lvlName = "";
                lvlCaption = "";
                lvlRef = "";
                while ( true )
                {
                    if (rdr.NodeType == XmlNodeType.Element && rdr.LocalName == "Level") break;
                    if (rdr.NodeType == XmlNodeType.EndElement && rdr.LocalName == "Hierarchy") break;
                    rdr.Read();
                } 
            }
             
        }

        private string GetHierarchStructure(ADOTabularTable table, string hierName, string hierCap)
        {
            if (_hierStructure == null) return "";
            if (_hierStructure.Count == 0) return "";
            if (!_hierStructure.ContainsKey(table.Caption)) return "";
            if (!_hierStructure[table.Caption].ContainsKey(hierCap ?? hierName)) return "";
            
            return _hierStructure[table.Caption][hierCap ?? hierName];
        }

        SortedDictionary<string, ADOTabularColumn> IMetaDataVisitor.Visit(ADOTabularColumnCollection columns)
        {
            return new SortedDictionary<string, ADOTabularColumn>();
        }

        public void Visit(ADOTabularFunctionGroupCollection functionGroups)
        {
            DataRow[] drFuncs = _conn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS",null,false).Tables[0].Select("ORIGIN=3 OR ORIGIN=4");
            foreach (DataRow dr in drFuncs)
            {
                functionGroups.AddFunction(dr);
            }
        }

        public void Visit(ADOTabularKeywordCollection keywords)
        {
            //DataRowCollection drKeywords = _conn.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false).Tables[0].Rows;
            //DataRowCollection drFunctions = _conn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false).Tables[0].Rows;
            var drKeywords = _conn.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false).Tables[0];
            var drFunctions = _conn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false).Tables[0].Select("ORIGIN=3 OR ORIGIN=4");

            //var ds = drKeywords.DataSet.Tables;
            //ds.Add(drFunctions);


            var kwords = from keyword in drKeywords.AsEnumerable()
                           join function in drFunctions.AsEnumerable() on keyword["Keyword"] equals function["FUNCTION_NAME"] into a
                           from kword in a.DefaultIfEmpty()
                           where kword == null
                           select new { Keyword = (string)keyword["Keyword"] , Matched= kword==null?true:false};

            //foreach (DataRow dr in drKeywords)
            foreach (var dr in kwords)
            {
                keywords.Add(dr.Keyword);
            }
            
        }

        private static int? GetInt(IDataRecord dr, int column) {
            return dr.IsDBNull(column) ? (int?)null : dr.GetInt32(column);
        }
        private static string GetString(IDataRecord dr, int column) {
            return dr.IsDBNull(column) ? null : dr.GetString(column);
        }
        private static string GetXmlString(IDataRecord dr, int column) {
            // Use the original AdomdDataReader (we don't have to use the proxy here!)
            Microsoft.AnalysisServices.AdomdClient.AdomdDataReader mdXmlField = dr.GetValue(column) as Microsoft.AnalysisServices.AdomdClient.AdomdDataReader;
            if (mdXmlField == null) {
                return null;
            }
            XElement piXml = new XElement("PARAMETERINFO");
            while (mdXmlField.Read()) {
                XElement datanode = new XElement("Parameter");
                for (int col = 0; col < mdXmlField.FieldCount; col++) {
                    string fieldName = mdXmlField.GetName(col);
                    if (fieldName != "") {
                        var fieldContent = mdXmlField[col];
                        if (fieldContent != null) {
                            datanode.Add(new XElement(mdXmlField.GetName(col), fieldContent.ToString()));
                        }
                    }
                    piXml.Add(datanode);
                }
            }
            return piXml.ToString();
        }
        public void Visit(MetadataInfo.DaxMetadata daxMetadata) { 
            string ssasVersion = GetSsasVersion();
            daxMetadata.Version = new MetadataInfo.SsasVersion {
                SSAS_VERSION = ssasVersion,
                CAPTURE_DATE = DateTime.Now
            };
            AdomdDataReader result = _conn.ExecuteReader("SELECT * FROM $SYSTEM.MDSCHEMA_FUNCTIONS");
            while (result.Read()) {
                // Filters only DAX functions
                int? origin = GetInt(result, result.GetOrdinal("ORIGIN"));
                if (origin == null) continue;
                if (origin != 3 && origin != 4) continue;

                var SSAS_VERSION = ssasVersion;
                var FUNCTION_NAME = GetString(result, result.GetOrdinal("FUNCTION_NAME"));
                var DESCRIPTION = GetString(result, result.GetOrdinal("DESCRIPTION"));
                var PARAMETER_LIST = GetString(result, result.GetOrdinal("PARAMETER_LIST"));
                var RETURN_TYPE = GetInt(result, result.GetOrdinal("RETURN_TYPE"));
                var ORIGIN = origin;
                var INTERFACE_NAME = GetString(result, result.GetOrdinal("INTERFACE_NAME"));
                var LIBRARY_NAME = GetString(result, result.GetOrdinal("LIBRARY_NAME"));
                var DLL_NAME = GetString(result, result.GetOrdinal("DLL_NAME"));
                var HELP_FILE = GetString(result, result.GetOrdinal("HELP_FILE"));
                var HELP_CONTEXT = GetInt(result, result.GetOrdinal("HELP_CONTEXT"));
                var OBJECT = GetString(result, result.GetOrdinal("OBJECT"));
                var CAPTION = GetString(result, result.GetOrdinal("CAPTION"));
                var PARAMETERINFO = GetXmlString(result, result.GetOrdinal("PARAMETERINFO"));
                var DIRECTQUERY_PUSHABLE = (result.FieldCount >= 14 ? GetInt(result, result.GetOrdinal("DIRECTQUERY_PUSHABLE")) : null);

                var function = new MetadataInfo.DaxFunction {
                    SSAS_VERSION = ssasVersion,
                    FUNCTION_NAME = GetString(result, result.GetOrdinal("FUNCTION_NAME")),
                    DESCRIPTION = GetString(result, result.GetOrdinal("DESCRIPTION")),
                    PARAMETER_LIST = GetString(result, result.GetOrdinal("PARAMETER_LIST")),
                    RETURN_TYPE = GetInt(result, result.GetOrdinal("RETURN_TYPE")),
                    ORIGIN = origin,
                    INTERFACE_NAME = GetString(result, result.GetOrdinal("INTERFACE_NAME")),
                    LIBRARY_NAME = GetString(result, result.GetOrdinal("LIBRARY_NAME")),
                    DLL_NAME = GetString(result, result.GetOrdinal("DLL_NAME")),
                    HELP_FILE = GetString(result, result.GetOrdinal("HELP_FILE")),
                    HELP_CONTEXT = GetInt(result, result.GetOrdinal("HELP_CONTEXT")),
                    OBJECT = GetString(result, result.GetOrdinal("OBJECT")),
                    CAPTION = GetString(result, result.GetOrdinal("CAPTION")),
                    PARAMETERINFO = GetXmlString(result, result.GetOrdinal("PARAMETERINFO")),
                    DIRECTQUERY_PUSHABLE = (result.FieldCount >= 14 ? GetInt(result, result.GetOrdinal("DIRECTQUERY_PUSHABLE")) : null)
                };
                daxMetadata.DaxFunctions.Add(function);
            }
        }

        private string GetSsasVersion() {
            var drProperties = _conn.GetSchemaDataSet("DISCOVER_PROPERTIES", null, false).Tables[0].Select("PROPERTYNAME = 'DBMSVersion'");
            var dr = drProperties.Single();
            string ssasVersion = dr["Value"].ToString();
            return ssasVersion;
        }

        public SortedDictionary<string, ADOTabularMeasure> Visit(ADOTabularMeasureCollection measures)
        {
            //RRomano: Better way to reuse this method in the two visitors? Create an abstract class of a visitor so that code can be shared (csdl doesnt seem to have the DAX expression)

            var ret = MetaDataVisitorADOMD.VisitMeasures(measures, this._conn);

            return ret;
        }
    }

}
