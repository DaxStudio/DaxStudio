using ADOTabular.AdomdClientWrappers;
using ADOTabular.Enums;
using ADOTabular.Extensions;
using ADOTabular.Interfaces;
using ADOTabular.MetadataInfo;
using ADOTabular.Utils;
using Microsoft.AnalysisServices.Tabular;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace ADOTabular
{
    public class MetaDataVisitorCSDL : IMetaDataVisitor
    {
        private readonly IADOTabularConnection _conn;
        private Dictionary<string, Dictionary<string, string>> _hierStructure;

        public MetaDataVisitorCSDL(IADOTabularConnection conn)
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
                    , new ADOTabularModel(_conn, models.Database, dr["CUBE_NAME"].ToString(), dr["CUBE_CAPTION"].ToString(), dr["DESCRIPTION"].ToString(), dr["BASE_CUBE_NAME"].ToString()));
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
                resColl.Add(new AdomdRestriction("VERSION", "2.5"));
            else if (_conn.ServerVersion.VersionGreaterOrEqualTo("11.0.3000.0")
                || (_conn.IsPowerPivot && _conn.ServerVersion.VersionGreaterOrEqualTo("11.0.2830.0")))
                resColl.Add(new AdomdRestriction("VERSION", "1.1"));
            Log.Debug("{class} {method} {message}", nameof(MetaDataVisitorCSDL), "Visit(ADOTabularTableCollection tables)", "Start DISCOVER_CSDL_METADATA call");
            var ds = _conn.GetSchemaDataSet("DISCOVER_CSDL_METADATA", resColl);
            Log.Debug("{class} {method} {message}", nameof(MetaDataVisitorCSDL), "Visit(ADOTabularTableCollection tables)", "End DISCOVER_CSDL_METADATA call");
            string csdl = ds.Tables[0].Rows[0]["Metadata"].ToString();

            /*
            //  debug code
            using (StreamWriter outfile = new StreamWriter( @"d:\data\csdl.xml"))
            {
                outfile.Write(csdl);
            }
            */

            // get hierarchy structure
            if (!_conn.ServerVersion.VersionGreaterOrEqualTo("11.0.3000.0")
                || (_conn.IsPowerPivot && !_conn.ServerVersion.VersionGreaterOrEqualTo("11.0.2830.0")))
            {
                // only call MDSCHEMA_HIERARCHIES for very old server versions
                GetHierarchiesFromDmv();
            }
            using XmlReader rdr = new XmlTextReader(new StringReader(csdl)) { DtdProcessing = DtdProcessing.Prohibit };
            
            GenerateTablesFromXmlReader(tables, rdr);
            
            // update the expressions async on a background thread
            PopulateMeasureExpressionsAsync(tables.Model,_conn).Forget();
        }

        private async Task PopulateMeasureExpressionsAsync(ADOTabularModel model, IADOTabularConnection conn)
        {
            await Task.Run(() => {
                Log.Debug("{class} {method} {message}", nameof(MetaDataVisitorCSDL), nameof(PopulateMeasureExpressionsAsync), "Start PopulateMeasureExpressionsAsync call");
                // TODO get measure definitions asynch
                var measureDict = model.MeasureExpressions;
                measureDict.Clear();
                var formatStringsDict = model.MeasureFormatStringExpressions;
                formatStringsDict.Clear();
                GetMeasuresFromDmv(measureDict, formatStringsDict, conn);
                Log.Debug("{class} {method} {message}", nameof(MetaDataVisitorCSDL), nameof(PopulateMeasureExpressionsAsync), "End PopulateMeasureExpressionsAsync call");
            });
        }



        internal static void GetMeasuresFromDmv(Dictionary<string,string> measureExpressions, Dictionary<string, string> measureFormatStringsExpressions, IADOTabularConnection conn)
        {
            // need to check if the DMV collection has the TMSCHEMA_MEASURES view, 
            // and if this is a connection with admin rights
            // and if it is not a PowerPivot model (as they seem to throw an error about the model needing to be in the "new" tabular mode)
            if (conn.DynamicManagementViews.Any(dmv => dmv.Name == "TMSCHEMA_MEASURES") && conn.IsAdminConnection && !conn.IsTestingRls && !conn.IsPowerPivot) GetTmSchemaMeasures(measureExpressions, measureFormatStringsExpressions, conn);
            else GetMdSchemaMeasures(measureExpressions, conn);
        }

        private static void GetMdSchemaMeasures(Dictionary<string,string> measureExpressions, IADOTabularConnection conn)
        {
            
            var resCollMeasures = new AdomdRestrictionCollection
                {
                    {"CATALOG_NAME", conn.Database.Name},
                    {"CUBE_NAME", conn.Database.Models.BaseModel.Name},
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
                measureExpressions.Add(dr["MEASURE_NAME"].ToString()
                                    ,  dr["EXPRESSION"].ToString()
                                    );
            }

            
        }

        private static void GetTmSchemaMeasures(Dictionary<string, string> measureExpressions, Dictionary<string, string> measureFormatStringsExpressions, IADOTabularConnection conn)
        {

            var resCollMeasures = new AdomdRestrictionCollection
                {
                    {"DatabaseName", conn.Database.Name}
                };

            if (!conn.ShowHiddenObjects) resCollMeasures.Add(new AdomdRestriction("IsHidden", false));

            // then get all the measures for the current table
            DataTable dtMeasures = conn.GetSchemaDataSet("TMSCHEMA_MEASURES", resCollMeasures).Tables[0];

            // Add format string definitions if available
            DataTable dtFormatStringDefinitions = null;
            int.TryParse(conn.Database.CompatibilityLevel, out int iCompatLevel);
            // FormatString definitions are only available in compat level 1470 or above
            if (conn.DynamicManagementViews.Any(dmv => dmv.Name == "TMSCHEMA_FORMAT_STRING_DEFINITIONS") && iCompatLevel >= 1470)
            {
                dtFormatStringDefinitions = conn.GetSchemaDataSet("TMSCHEMA_FORMAT_STRING_DEFINITIONS", resCollMeasures).Tables[0];
            }

            foreach (DataRow dr in dtMeasures.Rows)
            {
                measureExpressions.Add(
                    dr["Name"].ToString(),
                    dr["Expression"].ToString()
                );

                // Add format string if available
                if (dtFormatStringDefinitions != null  && dtFormatStringDefinitions.Rows.Count > 0 && dr.Table.Columns["FormatStringDefinitionID"] != null)
                {
                    ulong? id = dr["FormatStringDefinitionID"] as ulong?;
                    if (id.HasValue && id > 0)
                    {
                        var formatStrings = dtFormatStringDefinitions.Select($"ID = {id}");
                        if (formatStrings.Length > 0)
                        {
                            measureFormatStringsExpressions.Add(
                                dr["Name"].ToString(),
                                formatStrings[0]["Expression"].ToString()
                            );
                        }
                    }
                }
            }



        }

        private void GetHierarchiesFromDmv()
        {
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
        }

        public void GenerateTablesFromXmlReader(ADOTabularTableCollection tabs, XmlReader rdr)
        {
            Log.Debug("{class} {method} {message}", nameof(MetaDataVisitorCSDL), nameof(GenerateTablesFromXmlReader), "Start GenerateTablesFromXmlReader call");
            if (tabs == null) throw new ArgumentNullException(nameof(tabs));
            if (rdr == null) throw new ArgumentNullException(nameof(rdr));

            
            // clear out the flat cache of column names
            _conn.Columns.Clear();

            if (rdr.NameTable == null)
            {
                return;
            }
            var eEntityContainer = rdr.NameTable.Add("EntityContainer");
            var eEntitySet = rdr.NameTable.Add("EntitySet");
            var eEntityType = rdr.NameTable.Add("EntityType");
            var eAssociationSet = rdr.NameTable.Add("AssociationSet");

            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Element)
                {
                    switch (rdr.LocalName)
                    {
                        case "Schema":
                            GetCSDLVersion(rdr, tabs);
                            break;
                        case "EntityContainer":
                            if (rdr.NamespaceURI == @"http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions")
                                UpdateDatabaseAndModelFromEntityContainer(rdr, tabs, eEntityContainer);
                            break;
                        case "EntitySet":
                            var tab = BuildTableFromEntitySet(rdr, eEntitySet,tabs.Model);
                            tabs.Add(tab);
                            break;
                        case "EntityType":
                            AddColumnsToTable(rdr, tabs, eEntityType);
                            break;
                        case "AssociationSet":
                            if (tabs.Model.CSDLVersion >= 2.5)
                                BuildRelationshipFromAssociationSet(rdr, tabs, eAssociationSet);
                            break;
                        case "Association":
                            if (tabs.Model.CSDLVersion >= 2.5)
                                UpdateRelationshipFromAssociation(rdr, tabs);
                            break;
                    }

                }

            }

            // post processing of metadata
            foreach (var t in tabs)
            {
                TagKpiComponentColumns(t);
                if (tabs.Model.CSDLVersion >= 2.5 )
                    UpdateTomRelationships(t);
            }
            Log.Debug("{class} {method} {message}", nameof(MetaDataVisitorCSDL), nameof(GenerateTablesFromXmlReader), "End GenerateTablesFromXmlReader call");
        }

        private void GetCSDLVersion(XmlReader rdr, ADOTabularTableCollection tabs)
        {
            var version = rdr.GetAttribute("Version", "http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions");
            tabs.Model.CSDLVersion = Convert.ToDouble(version, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void UpdateTomRelationships(ADOTabularTable table)
        {
            var tomTable = table.Model.TOMModel.Tables[table.Name];
            
            foreach (var r in table.Relationships)
            {
                if (string.IsNullOrWhiteSpace( r.FromColumn) || string.IsNullOrWhiteSpace(r.ToColumn)) break;

                var toTable = r.ToTable;
                var toTomTable = table.Model.TOMModel.Tables[toTable.Name];
                var relationship = new SingleColumnRelationship
                {
                    FromColumn = tomTable.Columns.First(c => c.Name == table.Columns.GetByPropertyRef(r.FromColumn).Name),
                    ToColumn = toTomTable.Columns.First(c => c.Name == toTable.Columns.GetByPropertyRef(r.ToColumn).Name),
                    FromCardinality = getCardinality(r.FromColumnMultiplicity),
                    ToCardinality = getCardinality(r.ToColumnMultiplicity),
                    CrossFilteringBehavior = getCrossFilteringBehavior(r.CrossFilterDirection),
                    IsActive = r.IsActive
                };
                table.Model.TOMModel.Relationships.Add(relationship);
            }
        }

        private RelationshipEndCardinality getCardinality(string multiplicity)
        {
            return multiplicity switch
            {
                "*" => RelationshipEndCardinality.Many,
                "0..1" => RelationshipEndCardinality.One,
                "1" => RelationshipEndCardinality.One,
                _ => RelationshipEndCardinality.None,
            };
        }

        private CrossFilteringBehavior getCrossFilteringBehavior(string crossFilterDirection)
        {
            return crossFilterDirection switch
            {
                "Both" => CrossFilteringBehavior.BothDirections,
                _ => CrossFilteringBehavior.OneDirection,
            };
        }

        // Read the "Culture" attribute from <bi:EntityContainer>
        private static void UpdateDatabaseAndModelFromEntityContainer(XmlReader rdr, ADOTabularTableCollection tabs, string eEntityContainer)
        {
            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == eEntityContainer))
            {
                if (rdr.LocalName == eEntityContainer 
                    && rdr.NamespaceURI == @"http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions")
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                            case "Culture":
                                tabs.Model.Database.Culture = rdr.Value;
                                tabs.Model.TOMModel.Culture = rdr.Value;
                                break;
                        }
                    }
                    // read through the rest of the nodes until we get to the end element </bi:EntityContainer>
                    while (!(rdr.NodeType == XmlNodeType.EndElement 
                          && rdr.LocalName == eEntityContainer))
                    {
                        if (rdr.NodeType == XmlNodeType.Element && rdr.LocalName == "ModelCapabilities") PopulateModelCapabilitiesFromXml(tabs.Model, rdr);
                        rdr.Read();
                    }
                    
                }
                rdr.Read();
            }
        }

        private static void PopulateModelCapabilitiesFromXml(ADOTabularModel model, XmlReader rdr)
        {
            // read through the rest of the nodes until we get to the end element </bi:EntityContainer>
            while (!(rdr.NodeType == XmlNodeType.EndElement
                  && rdr.LocalName == "ModelCapabilities"))
            {
                if (rdr.NodeType == XmlNodeType.Element)
                {
                    bool enabled;
                    switch (rdr.LocalName)
                    {
                        case "Variables":
                            enabled = rdr.ReadElementContentAsBoolean();
                            model.Capabilities.Variables = enabled;
                            break;
                        case nameof(model.Capabilities.TableConstructor):
                            enabled = rdr.ReadElementContentAsBoolean();
                            model.Capabilities.TableConstructor = enabled;
                            break;
                        case "DAXFunctions":
                            PopulateDAXFunctionsFromXml(model, rdr);
                            break;
                        default:
                            rdr.Read();
                            break;
                    }
                }
                else
                {
                    rdr.Read();
                }
            }
        }

        private static void PopulateDAXFunctionsFromXml(ADOTabularModel model, XmlReader rdr)
        {
            
            while (!(rdr.NodeType == XmlNodeType.EndElement
                          && rdr.LocalName == "DAXFunctions"))
            {
                if (rdr.NodeType == XmlNodeType.Element)
                {
                    bool enabled;
                    switch (rdr.LocalName)
                    {
                        case "SummarizeColumns":
                            enabled = rdr.ReadElementContentAsBoolean();
                            model.Capabilities.DAXFunctions.SummarizeColumns = enabled;
                            break;
                        case "TreatAs":
                            enabled = rdr.ReadElementContentAsBoolean();
                            model.Capabilities.DAXFunctions.TreatAs = enabled;
                            break;
                        case "SubstituteWithIndex":
                            enabled = rdr.ReadElementContentAsBoolean();
                            model.Capabilities.DAXFunctions.SubstituteWithIndex = enabled;
                            break;
                        default:
                            rdr.Read();
                            break;
                    }
                }
                else
                {
                    rdr.Read();
                }
            }
        }

        private static void UpdateRelationshipFromAssociation(XmlReader rdr, ADOTabularTableCollection tabs)
        {
            if (tabs.Model.CSDLVersion < 2.5) return;
            
            string refName = string.Empty;
            string toColumnRef = string.Empty;
            string fromColumnRef = string.Empty;
            string toColumnMultiplicity = string.Empty;
            string fromColumnMultiplicity = string.Empty;

            while (rdr.MoveToNextAttribute())
            {
                switch (rdr.LocalName)
                {
                    case "Name":
                        refName = rdr.Value;
                        break;
                }
            }

            if (rdr.EOF) return;

                rdr.ReadToFollowing("ReferentialConstraint");
                
                var referentialConstraints = ReadReferentialConstraints(rdr, tabs);

                rdr.ReadToFollowing("End");
                var end1 = GetAssociationEnd(rdr);
                
                rdr.ReadToFollowing("End");
                var end2 = GetAssociationEnd(rdr);

                if (end1.Role == referentialConstraints.fromRole)
                {
                    referentialConstraints.fromMultiplicity = end1.Multiplicity;
                    referentialConstraints.toMultiplicity = end2.Multiplicity;
                }

                if (end1.Role == referentialConstraints.toRole)
                {
                    referentialConstraints.toMultiplicity = end1.Multiplicity;
                    referentialConstraints.fromMultiplicity = end2.Multiplicity;
                }


            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == "Association") && !rdr.EOF)
            {
                rdr.Read();
            }

            // Find relationship and update it
            foreach (var tab in tabs)
            {
                foreach (var rel in tab.Relationships)
                {
                    if (rel.InternalName == refName)
                    {
                        rel.ToColumn = referentialConstraints.toColumnRef;
                        rel.ToColumnMultiplicity = referentialConstraints.toMultiplicity;
                        rel.FromColumn = referentialConstraints.fromColumnRef;
                        rel.FromColumnMultiplicity = referentialConstraints.fromMultiplicity;
                        return;
                    }
                }
            }

        }

        private static (string fromRole, string fromColumnRef, string fromMultiplicity, string toRole, string toColumnRef, string toMultiplicity) ReadReferentialConstraints(XmlReader rdr, ADOTabularTableCollection tabs)
        {
            var result = (fromRole: string.Empty, fromColumnRef: string.Empty, fromMultiplicity: string.Empty,
                          toRole: string.Empty, toColumnRef: string.Empty, toMultiplicity: string.Empty);

            while ((rdr.NodeType != XmlNodeType.EndElement
                   || rdr.LocalName != "ReferentialConstraint") && !rdr.EOF)
            {
                if (rdr.NodeType == XmlNodeType.Element && rdr.LocalName == "Principal")
                {
                    result.toRole = rdr.GetAttribute("Role");
                    rdr.ReadToFollowing("PropertyRef");
                    result.toColumnRef = rdr.GetAttribute("Name");
                }

                if (rdr.NodeType == XmlNodeType.Element && rdr.LocalName == "Dependent")
                {
                    result.fromRole = rdr.GetAttribute("Role");
                    rdr.ReadToFollowing("PropertyRef");
                    result.fromColumnRef = rdr.GetAttribute("Name");
                }

                rdr.Read();
            }

            return result;
        }

        private static void BuildRelationshipFromAssociationSet(XmlReader rdr, ADOTabularTableCollection tabs, string eAssociationSet)
        {
            string refName = string.Empty;
            string fromTableRef = "";
            string toTableRef = "";
            string crossFilterDir = "";
            bool isActive = true;

            while (rdr.MoveToNextAttribute())
            {
                switch (rdr.LocalName)
                {
                    case "Name":
                        refName = rdr.Value;
                        break;
                }
            }

            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == eAssociationSet))
            {
                // todo read entitySet as From/To table
                if (rdr.LocalName == "End")
                {
                    fromTableRef = GetRelationshipTableRef(rdr);
                    toTableRef = GetRelationshipTableRef(rdr);
                }
                if (rdr.LocalName == "AssociationSet" && rdr.NamespaceURI == @"http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions")
                {

                    crossFilterDir = rdr.GetAttribute("CrossFilterDirection");
                    isActive = rdr.GetAttribute("State") != "Inactive";
                }
                rdr.Read();
            }

            var fromTable = tabs.GetById(fromTableRef);
            fromTable.Relationships.Add(new ADOTabularRelationship() {
                FromTable = tabs.GetById(fromTableRef),
                ToTable = tabs.GetById(toTableRef),
                InternalName = refName,
                CrossFilterDirection = crossFilterDir,
                IsActive = isActive
            });

        }

        private static string GetRelationshipTableRef(XmlReader rdr)
        {
            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == "End"))
            {
                while (rdr.MoveToNextAttribute())
                {
                    if (rdr.LocalName == "EntitySet")
                    {
                        string entitySet = rdr.Value;
                        rdr.Skip(); // jump to the end of the current Element
                        rdr.MoveToContent(); // move past any whitespace to the next Element
                        return entitySet;
                    }
                }
                rdr.Read();
            }
            return "";
        }

        private static (string Role, string Multiplicity) GetAssociationEnd(XmlReader rdr)
        {
            var result = (Role: string.Empty, Multiplicity: string.Empty);

            result.Role = rdr.GetAttribute("Role");
            result.Multiplicity = rdr.GetAttribute("Multiplicity");

            return result;

        }


        private ADOTabularTable BuildTableFromEntitySet(XmlReader rdr, string eEntitySet, ADOTabularModel model)
        {
            string caption = null;
            string description = "";
            string refName = null;
            bool isVisible = true;
            string name = null;
            bool isPrivate = false;
            bool showAsVariationsOnly = false;

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
                            refName = rdr.Value;
                            break;
                        case "Private":
                            isPrivate = bool.Parse(rdr.Value);
                            break;
                        case "ShowAsVariationsOnly":
                            showAsVariationsOnly = bool.Parse(rdr.Value);
                            break;
                    }
                }
                if (rdr.LocalName == "Summary")
                {
                    description = (string)rdr.ReadElementContentAs(typeof(string),null);
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
            var tab = new ADOTabularTable(_conn, model, refName, name, caption, description, isVisible, isPrivate, showAsVariationsOnly);

            return tab;
        }

        private static void TagKpiComponentColumns(ADOTabularTable tab)
        {
            List<ADOTabularColumn> invalidKpis = new List<ADOTabularColumn>();

            foreach (var c in tab.Columns)
            {
                if (c.ObjectType == ADOTabularObjectType.KPI)
                {
                    var k = (ADOTabularKpi)c;
                    if (k.Goal == null && k.Status == null)
                        invalidKpis.Add(c);
                    if (k.Goal != null)
                        k.Goal.ObjectType = ADOTabularObjectType.KPIGoal;
                    if (k.Status != null)
                        k.Status.ObjectType = ADOTabularObjectType.KPIStatus;
                }
            }
            foreach (var invalidKpi in invalidKpis)
            {
                tab.Columns.Remove(invalidKpi);
                
                if (!tab.Measures.ContainsKey(invalidKpi.Name))
                {
                    var newMeasure = new ADOTabularMeasure(tab, invalidKpi.InternalReference, invalidKpi.Name, invalidKpi.Caption, invalidKpi.Description, invalidKpi.IsVisible, invalidKpi.MeasureExpression, null);
                    tab.Measures.Add(newMeasure);
                }
            }
        }

        private void AddColumnsToTable(XmlReader rdr
            , ADOTabularTableCollection tables
            , string eEntityType)
        {
            

            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == eEntityType))
            {
                var eProperty = rdr.NameTable.Add("Property");
                var eMeasure = rdr.NameTable.Add("Measure");
                var eSummary = rdr.NameTable.Add("Summary");
                var eStatistics = rdr.NameTable.Add("Statistics");
                var eMinValue = rdr.NameTable.Add("MinValue");
                var eMaxValue = rdr.NameTable.Add("MaxValue");
                var eOrderBy = rdr.NameTable.Add("OrderBy");
                var eGroupBy = rdr.NameTable.Add("GroupBy");

                // this routine effectively processes and <EntityType> element and it's children
                string caption = "";
                string description = "";
                bool isVisible = true;
                string name = null;
                string refName = string.Empty;
                string tableId = string.Empty;
                string dataType = string.Empty;
                string contents = string.Empty;
                string minValue = string.Empty;
                string maxValue = string.Empty;
                string formatString = string.Empty;
                string keyRef = string.Empty;
                string defaultAggregateFunction = string.Empty;
                string orderBy = string.Empty;
                List<string> groupBy = new List<string>();
                long stringValueMaxLength = 0;
                long distinctValueCount = 0;
                bool nullable = true;
                DataType dataTypeEnum = DataType.Unknown;

                ADOTabularTable tab = null;

                IFormatProvider invariantCulture = System.Globalization.CultureInfo.InvariantCulture;

                List<ADOTabularVariation> variations = new List<ADOTabularVariation>();

                KpiDetails kpi = new KpiDetails();

                var colType = ADOTabularObjectType.Column;
                while (!(rdr.NodeType == XmlNodeType.EndElement
                         && rdr.LocalName == eEntityType))
                {
                    //while (rdr.NodeType == XmlNodeType.Whitespace)
                    //{
                    //    rdr.Read();
                    //}

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.Name == eEntityType)
                    {
                        while (rdr.MoveToNextAttribute())
                        {
                            switch (rdr.LocalName)
                            {
                                case "Name":
                                    tableId = rdr.Value;
                                    tab = tables.GetById(tableId);
                                    break;
                            }
                        }
                    }

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == "Key")
                    {
                        // TODO - store table Key
                        keyRef = GetKeyReference(rdr);
                    }

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == "Hierarchy")
                    {
                        ProcessHierarchy(rdr, tab);

                    }

                    if (rdr.NodeType == XmlNodeType.Element 
                        && rdr.Name == "bi:EntityType")
                    {
                        // gets the DataCategory (eg. "Time" for date tables)
                        var contentAttr = rdr.GetAttribute("Contents");
                        tab.DataCategory = contentAttr;
                    }

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == "DisplayFolder")
                    {
                        ProcessDisplayFolder(rdr,tab,tab);
                    }

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == "Kpi")
                    {
                        kpi = ProcessKpi(rdr);
                    }

                if (rdr.NodeType == XmlNodeType.Element
                    && (rdr.LocalName == eProperty
                    || rdr.LocalName == eMeasure
                    || rdr.LocalName == eSummary
                    || rdr.LocalName == eOrderBy
                    || rdr.LocalName == eGroupBy
                    || rdr.LocalName == eStatistics
                    || rdr.LocalName == eMinValue
                    || rdr.LocalName == eMaxValue))
                {

                        if (rdr.LocalName == eMeasure)
                            colType = ADOTabularObjectType.Measure;

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
                                    if (!Enum.TryParse(dataType, out dataTypeEnum))
                                    {
                                        dataTypeEnum = DataType.Unknown;
                                    }
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
                                    distinctValueCount = long.Parse(rdr.Value, invariantCulture);
                                    break;
                                case "StringValueMaxLength":
                                    stringValueMaxLength = long.Parse(rdr.Value, invariantCulture);
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
                                //TODO - Add RowCount
                            }
                        }

                    }

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == "Variations") {
                        variations = ProcessVariations(rdr);
                    }

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == "OrderBy")
                    {
                        orderBy = ProcessOrderBy(rdr);
                    }

                    if (rdr.NodeType == XmlNodeType.Element
                        && rdr.LocalName == "GroupBy")
                    {
                        groupBy = ProcessGroupBy(rdr);
                    }

                    if (rdr.NodeType == XmlNodeType.EndElement
                        && rdr.LocalName == eProperty
                        && rdr.LocalName == "Property")
                    {

                        if (caption.Length == 0)
                            caption = refName;
                        if (!string.IsNullOrWhiteSpace(caption))
                        {
                        
                            if (kpi.IsBlank())
                            {
                                var col = new ADOTabularColumn(tab, refName, name, caption, description, isVisible, colType, contents)
                                {
                                    SystemType = Type.GetType($"System.{dataType}"),
                                    DataType = dataTypeEnum,
                                    Nullable = nullable,
                                    MinValue = minValue,
                                    MaxValue = maxValue,
                                    DistinctValues = distinctValueCount,
                                    FormatString = formatString,
                                    StringValueMaxLength = stringValueMaxLength,
                                    OrderByRef = orderBy
                                };
                                col.Variations.AddRange(variations);
                                col.GroupByRefs.AddRange(groupBy);
                                tables.Model.AddRole(col);
                                tab.Columns.Add(col);
                                _conn.Columns.Add(col.DaxName.TrimStart('\'').Replace("'[","["), col);
                            }
                            else
                            {
                                colType = ADOTabularObjectType.KPI;
                                var kpiCol = new ADOTabularKpi(tab, refName, name, caption, description, isVisible, colType, contents, kpi)
                                {
                                    SystemType = Type.GetType($"System.{dataType}")
                                };
                                tab.Columns.Add(kpiCol);
                                _conn.Columns.Add(kpiCol.DaxName, kpiCol);
                            }
                        }


                        // reset temp column variables
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
                        colType = ADOTabularObjectType.Column;
                        variations = new List<ADOTabularVariation>();
                        dataTypeEnum = DataType.Unknown;
                        orderBy = string.Empty;
                        groupBy.Clear();
                    }
                    if (!rdr.Read()) break;// quit the read loop if there is no more data
                }

                // Set Key column
                var keyCol = tab?.Columns.GetByPropertyRef(keyRef);
                if(keyCol != null) keyCol.IsKey = true;
            }

            //TODO - link up back reference to backing measures for KPIs

        }

        private static string GetKeyReference(XmlReader rdr)
        {
            var keyRef = "";
            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Element && rdr.Name == "PropertyRef")
                {
                    keyRef = rdr.GetAttribute("Name");
                    rdr.Read();
                    break;
                }
                if (rdr.NodeType == XmlNodeType.EndElement && rdr.Name == "Key") break;
            }
            return keyRef;
        }

        private static string ProcessOrderBy(XmlReader rdr)
        {
            var orderBy = string.Empty;
            
            while (!(rdr.NodeType == XmlNodeType.EndElement && rdr.LocalName == "OrderBy"))
            {
                if (rdr.NodeType == XmlNodeType.Element && rdr.LocalName == "PropertyRef")
                {
                    rdr.MoveToAttribute("Name");
                    rdr.ReadAttributeValue();
                    orderBy = rdr.Value;
                }

                rdr.Read();
            }

            return orderBy;
        }

        private static List<string> ProcessGroupBy(XmlReader rdr)
        {
            var groupBy = new List<string>();

            while (!(rdr.NodeType == XmlNodeType.EndElement && rdr.LocalName == "GroupBy"))
            {
                if (rdr.NodeType == XmlNodeType.Element && rdr.LocalName == "PropertyRef")
                {
                    rdr.MoveToAttribute("Name");
                    rdr.ReadAttributeValue();
                    groupBy.Add(rdr.Value);
                }

                rdr.Read();
            }

            return groupBy;
        }

        private static List<ADOTabularVariation> ProcessVariations(XmlReader rdr)
        {
            string _name;
            bool isDefault = false;
            string navigationPropertyRef = string.Empty;
            string defaultHierarchyRef = string.Empty;

            List<ADOTabularVariation> _variations = new List<ADOTabularVariation>();
            while (!(rdr.NodeType == XmlNodeType.EndElement 
                && rdr.LocalName == "Variations")) {

                if (rdr.NodeType == XmlNodeType.Element 
                    && rdr.LocalName == "Variation")
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                            case "Name":
#pragma warning disable IDE0059 // Unnecessary assignment of a value
                                // NOTE (marcorus): Is this assignment necessary? Why _name is not used later?
                                _name = rdr.Value;
#pragma warning restore IDE0059 // Unnecessary assignment of a value
                                break;
                            case "Default":
                                isDefault = bool.Parse( rdr.Value);
                                break;
                        }
                    }

                }

                if(rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "NavigationPropertyRef")
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        if (rdr.LocalName == "Name") navigationPropertyRef = rdr.Value;
                    }        
                }

                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "DefaultHierarchyRef")
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        if (rdr.LocalName == "Name") defaultHierarchyRef = rdr.Value;
                    }
                }


                if (rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "Variation")
                {
                    _variations.Add(new ADOTabularVariation() { NavigationPropertyRef = navigationPropertyRef, DefaultHierarchyRef = defaultHierarchyRef, IsDefault = isDefault });
                    isDefault = false;
                    navigationPropertyRef = string.Empty;
                    defaultHierarchyRef = string.Empty;
                }
                rdr.Read();
            }
            return _variations;
        }

        private void ProcessDisplayFolder(XmlReader rdr, ADOTabularTable table, IADOTabularFolderReference parent)
        {
            var folderReference = "";
            string folderCaption = null;
            string objRef = "";

            IADOTabularFolderReference folder = null;

            while (!(rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "DisplayFolder"))
            {

               

                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == "DisplayFolder")
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                          
                            case "Name":
                                folderReference = rdr.Value;
                                break;
                            case "Caption":
                                folderCaption = rdr.Value;
                                break;
                        }
                    }
                    // create folder and add to parent's folders
                    folder = new ADOTabularDisplayFolder(folderCaption, folderReference);
                    parent.FolderItems.Add(folder);

                    rdr.ReadToNextElement();

                    // recurse down to child items
                    ProcessDisplayFolder(rdr, table, folder);

                    if (folder.IsVisible && parent is ADOTabularDisplayFolder parentFolder) parentFolder.IsVisible = true;

                    rdr.Read();
                    //rdr.ReadToNextElement(); // read the end element
                }

                // Reset DisplayFolder local variables
                folderCaption = null;
                folderReference = string.Empty;
                    
                if ((rdr.NodeType == XmlNodeType.Element)
                    && (rdr.LocalName == "PropertyRef" 
                        || rdr.LocalName == "HierarchyRef"
                    ))
                {
                    while (rdr.MoveToNextAttribute())
                    {
                        switch (rdr.LocalName)
                        {
                            case "Name":
                                objRef = rdr.Value;
                                break;
                        }
                    }

                    // create reference object
                    IADOTabularObjectReference reference = new ADOTabularObjectReference(String.Empty, objRef);
                    parent.FolderItems.Add(reference);
                    var column = table.Columns.GetByPropertyRef(objRef);
                    if (column != null) { 
                        column.IsInDisplayFolder = true;
                        if (column.IsVisible && parent is ADOTabularDisplayFolder displayFolder) displayFolder.IsVisible = true;
                    }
                    objRef = "";

                    rdr.Read();
                }

                if (rdr.LocalName != "DisplayFolder" && rdr.LocalName != "PropertyRef" && rdr.LocalName != "DisplaFolders")

                {
                    if (rdr.NodeType != XmlNodeType.Element && rdr.NodeType != XmlNodeType.EndElement)
                        rdr.ReadToNextElement();
                    //else
                    //    rdr.Read();
                }

                if (rdr.NodeType == XmlNodeType.EndElement && rdr.LocalName == "DisplayFolders")
                {
                    rdr.Read();
                    break;
                }

                //rdr.Read();

            }
            
        }

        private static KpiDetails ProcessKpi(XmlReader rdr)
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

        private void ProcessHierarchy(XmlReader rdr, ADOTabularTable table)
        {
            var hierName = "";
            string hierCap = null;
            var hierIsVisible = true;
            ADOTabularHierarchy hier = null;
            ADOTabularLevel lvl;
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
                                hierIsVisible = !bool.Parse(rdr.Value);
                                break;
                            case "Name":
                                hierName = rdr.Value;
                                break;
                            case "Caption":
                                hierCap = rdr.Value;
                                break;
                        }
                    }
                    string structure = GetHierarchyStructure(table, hierName, hierCap);
                    hier = new ADOTabularHierarchy(table, hierName, hierName, hierCap ?? hierName, "", hierIsVisible, ADOTabularObjectType.Hierarchy, "", structure);
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

                lvl = new ADOTabularLevel(table.Columns.GetByPropertyRef(lvlRef))
                {
                    LevelName = lvlName,
                    Caption = lvlCaption
                };
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

        private string GetHierarchyStructure(ADOTabularTable table, string hierName, string hierCap)
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
            if (functionGroups == null) throw new ArgumentNullException(nameof(functionGroups));
            var catalogRestriction = new AdomdRestriction("CATALOG_NAME", _conn.Database.Name);
            var restrictions = new AdomdRestrictionCollection { catalogRestriction };
            DataRow[] drFuncs = _conn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", restrictions, false).Tables[0].Select("ORIGIN = 2 OR ORIGIN = 3 OR ORIGIN = 4");
            foreach (DataRow dr in drFuncs)
            {
                functionGroups.AddFunction(dr);
            }
            AddUndocumentedFunctions(functionGroups);
        }

        private void AddUndocumentedFunctions(ADOTabularFunctionGroupCollection functionGroups)
        {
            var ssas2016 = new Version(13,0,0,0);
            if (Version.Parse(_conn.ServerVersion) >= ssas2016)
            {
                using DataTable paramTable = CreateParameterTable();

                paramTable.Rows.Add(new[] { "Rows", "FALSE", "FALSE", "FALSE" });
                paramTable.Rows.Add(new[] { "Skip", "FALSE", "FALSE", "FALSE" });
                paramTable.Rows.Add(new[] { "Table", "FALSE", "FALSE", "FALSE" });
                paramTable.Rows.Add(new[] { "OrderByExpression", "TRUE", "FALSE", "FALSE" });
                paramTable.Rows.Add(new[] { "Order", "FALSE", "TRUE", "FALSE" });

                functionGroups.AddFunction("FILTER", "TOPNSKIP", "Retrieves a number of rows from a table efficiently, skipping a number of rows. Compared to TOPN, the TOPNSKIP function is less flexible, but much faster.", paramTable.Select());
            }
        }

        private static DataTable CreateParameterTable()
        {
            var paramTable = new DataTable();
            paramTable.Columns.Add("NAME", typeof(string));
            paramTable.Columns.Add("OPTIONAL", typeof(string));
            paramTable.Columns.Add("REPEATING", typeof(string));
            paramTable.Columns.Add("REPEATABLE", typeof(string));
            return paramTable;
        }

        public void Visit(ADOTabularKeywordCollection keywords)
        {
            if (keywords == null) throw new ArgumentNullException(nameof(keywords));

            var drKeywords = _conn.GetSchemaDataSet("DISCOVER_KEYWORDS", null, false).Tables[0];
            var drFunctions = _conn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS", null, false).Tables[0].Select("ORIGIN=3 OR ORIGIN=4");

            var kwords = from keyword in drKeywords.AsEnumerable()
                         join function in drFunctions.AsEnumerable() on keyword["Keyword"] equals function["FUNCTION_NAME"] into a
                         from kword in a.DefaultIfEmpty()
                         where kword == null
                         select new { Keyword = (string)keyword["Keyword"] , Matched = (kword==null) };

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

        private static long? GetLong(IDataRecord dr, int column)
        {
            return dr.IsDBNull(column) ? (long?)null : dr.GetInt64(column);
        }
        private static string GetXmlString(IDataRecord dr, int column) {
            // Use the original AdomdDataReader (we don't have to use the proxy here!)
            if (!(dr.GetValue(column) is Microsoft.AnalysisServices.AdomdClient.AdomdDataReader mdXmlField))
            {
                return null;
            }
            XElement piXml = new XElement("PARAMETERINFO");
            while (mdXmlField.Read()) {
                XElement datanode = new XElement("Parameter");
                for (int col = 0; col < mdXmlField.FieldCount; col++) {
                    string fieldName = mdXmlField.GetName(col);
                    if (!string.IsNullOrEmpty(fieldName)) {
                        var fieldContent = mdXmlField[col];
                        if (fieldContent != null) {
                            datanode.Add(new XElement(mdXmlField.GetName(col), fieldContent.ToString()));
                        }
                    }
                }
                piXml.Add(datanode);
            }
            string s = piXml.ToString();
            return s;
        }
        public void Visit(MetadataInfo.DaxMetadata daxMetadata) {
            if (daxMetadata == null) throw new ArgumentNullException(nameof(daxMetadata));

            string ssasVersion = GetSsasVersion();
            Product productInfo = GetProduct(ssasVersion);
            daxMetadata.Version = new MetadataInfo.SsasVersion {
                SSAS_VERSION = ssasVersion,
                CAPTURE_DATE = DateTime.Now,
                PRODUCT_TYPE = productInfo.Type,
                PRODUCT_NAME = productInfo.Name
            };
            AdomdDataReader result = _conn.ExecuteReader("SELECT * FROM $SYSTEM.MDSCHEMA_FUNCTIONS",null);
            while (result.Read()) {
                // Filters only DAX functions
                int? origin = GetInt(result, result.GetOrdinal("ORIGIN"));
                if (origin == null) continue;
                if (origin != 3 && origin != 4) continue;

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

        private struct Product {
            public string Type;
            public string Name;
        }

        private Product GetProduct(string ssasVersion) {
            string serverName = _conn.ServerName;
            string serverId = _conn.ServerId;
            Product product;
            product.Type = null;
            product.Name = null;
            if (_conn.Type == AdomdType.Excel) {
                product.Type = "Excel";
                if (ssasVersion.StartsWith("16.", StringComparison.InvariantCultureIgnoreCase))
                {
                    product.Name = "Excel Microsoft 365";
                }
                else if (ssasVersion.StartsWith("13.",StringComparison.InvariantCultureIgnoreCase)) {
                    product.Name = "Excel 2016";
                }
                else if (ssasVersion.StartsWith("11.", StringComparison.InvariantCultureIgnoreCase)) {
                    product.Name = "Excel 2013";
                }
                else {
                    product.Name = product.Type;
                }
            }
            else if (serverName.StartsWith("asazure://", StringComparison.InvariantCultureIgnoreCase)) {
                product.Type = "Azure AS";
                product.Name = product.Type;
            }
            else if (serverName.IsPowerBIService() )  // Power BI Premium Internal
            {
                product.Type = "Power BI Service";
                product.Name = product.Type;
            }
            else if (serverId.Contains(@"\AnalysisServicesWorkspace")) {
                product.Type = "Power BI";
                product.Name = product.Type;
            }
            else if (serverId.Contains(@"\DataToolsInstance")) {
                product.Type = "SSDT";
                product.Name = product.Type;
            }
            else {
                product.Type = "SSAS Tabular";
                if (ssasVersion.StartsWith("17.", StringComparison.InvariantCultureIgnoreCase))
                {
                    product.Name = "SSAS 2025";
                }
                else if (ssasVersion.StartsWith("16.", StringComparison.InvariantCultureIgnoreCase))
                {
                    product.Name = "SSAS 2022";
                }
                else if (ssasVersion.StartsWith("15.", StringComparison.InvariantCultureIgnoreCase))
                {
                    product.Name = "SSAS 2019";
                }
                else if (ssasVersion.StartsWith("14.", StringComparison.InvariantCultureIgnoreCase)) {
                    product.Name = "SSAS 2017";
                }
                else if (ssasVersion.StartsWith("13.", StringComparison.InvariantCultureIgnoreCase)) {
                    product.Name = "SSAS 2016";
                }
                else if (ssasVersion.StartsWith("12.", StringComparison.InvariantCultureIgnoreCase)) {
                    product.Name = "SSAS 2014";
                }
                else if (ssasVersion.StartsWith("11.", StringComparison.InvariantCultureIgnoreCase)) {
                    product.Name = "SSAS 2012";
                }
                else if (ssasVersion.StartsWith("10.", StringComparison.InvariantCultureIgnoreCase)) {
                    product.Name = "SSAS 2012";
                }
                else {
                    product.Name = product.Type;
                }
            }
            return product;
        }

        public SortedDictionary<string, ADOTabularMeasure> Visit(ADOTabularMeasureCollection measures)
        {
            if (measures == null) throw new ArgumentNullException(nameof(measures));
            //RRomano: Better way to reuse this method in the two visitors? 
            // Create an abstract class of a visitor so that code can be shared 
            // (csdl doesn't seem to have the DAX expression)

            var ret = MetaDataVisitorADOMD.VisitMeasures(measures, this._conn);

            return ret;
        }

        public void Visit(MetadataInfo.DaxColumnsRemap daxColumnsRemap)
        {
            if (daxColumnsRemap == null) throw new ArgumentNullException(nameof(daxColumnsRemap));

            // Clear remapping
            daxColumnsRemap.RemapNames.Clear();
            const string QUERY_REMAP_COLUMNS = @"SELECT COLUMN_ID AS COLUMN_ID, ATTRIBUTE_NAME AS COLUMN_NAME FROM $SYSTEM.DISCOVER_STORAGE_TABLE_COLUMNS WHERE COLUMN_TYPE = 'BASIC_DATA'";

            // Load remapping
            using AdomdDataReader result = _conn.ExecuteReader(QUERY_REMAP_COLUMNS, null);
            while (result.Read())
            {
                string columnId = GetString(result, 0);
                string columnName = GetString(result, 1);

                // PowerPivot does not include the table id in the columnid so if two 
                // tables have a column with the same name this can throw a duplicate key error
                // the IF check prevents this.
                if (!daxColumnsRemap.RemapNames.ContainsKey(columnId))
                    daxColumnsRemap.RemapNames.Add(columnId, columnName);
                
            }
        }
        public void Visit(MetadataInfo.DaxTablesRemap daxTablesRemap)
        {
            if (daxTablesRemap == null) throw new ArgumentNullException(nameof(daxTablesRemap));

            // Clear remapping
            daxTablesRemap.RemapNames.Clear();
            const string QUERY_REMAP_TABLES = @"select TABLE_ID AS TABLE_ID, DIMENSION_NAME AS TABLE_NAME from $SYSTEM.DISCOVER_STORAGE_TABLES WHERE RIGHT ( LEFT ( TABLE_ID, 2 ), 1 ) <> '$'";

            // Load remapping
            using AdomdDataReader result = _conn.ExecuteReader(QUERY_REMAP_TABLES, null);
            while (result.Read())
            {
                string columnId = GetString(result, 0);
                string columnName = GetString(result, 1);

                // Safety check - if two tables have the same name
                // this can throw a duplicate key error; the IF check prevents this.
                if (!daxTablesRemap.RemapNames.ContainsKey(columnId))
                    daxTablesRemap.RemapNames.Add(columnId, columnName);

            }
        }

        public ADOTabularDatabase Visit(ADOTabularConnection conn)
        {
            return null;
        }

        public void Visit(ADOTabularCalendarCollection calendars)
        {
            // Clear remapping
            calendars.Clear();
            const string QUERY_CALENDARS = @"select [TableID], [Name] from $SYSTEM.TMSCHEMA_CALENDARS";
            //if (int.Parse(_conn.Database.CompatibilityLevel) < 1702) return;
            if (!_conn.DynamicManagementViews.Contains("TMSCHEMA_CALENDARS")) return;
            // Load remapping
            try
            {
                using AdomdDataReader result = _conn.ExecuteReader(QUERY_CALENDARS, null);
                while (result.Read())
                {
                    int? tableId = GetInt(result, 0);
                    string calendarName = GetString(result, 1);

                    // Safety check - if two tables have the same name
                    // this can throw a duplicate key error; the IF check prevents this.
                    if (!calendars.Contains(calendarName))
                        calendars.Add(new ADOTabularCalendar(tableId, calendarName));

                }
            }
            catch (Exception ex)
            {
                // Ignore errors here - could be a compatibility level issue or a permission issue accessing the DMV
                System.Diagnostics.Debug.WriteLine($"Error reading calendars: {ex.Message}");
            }
        }
    }
}
