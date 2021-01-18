using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Utils;
using ADOTabular.Interfaces;
using ADOTabular.Enums;

using Tuple = System.Tuple;

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

            using XmlReader rdr = new XmlTextReader(new StringReader(csdl)) { DtdProcessing = DtdProcessing.Prohibit };
            GenerateTablesFromXmlReader(tables, rdr);
        }

        public void GenerateTablesFromXmlReader(ADOTabularTableCollection tabs, XmlReader rdr)
        {
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
                        case "EntityContainer":
                            if (rdr.NamespaceURI == @"http://schemas.microsoft.com/sqlbi/2010/10/edm/extensions")
                                UpdateDatabaseAndModelFromEntityContainer(rdr, tabs, eEntityContainer);
                            break;
                        case "EntitySet":
                            var tab = BuildTableFromEntitySet(rdr, eEntitySet);
                            tabs.Add(tab);
                            break;
                        case "EntityType":
                            AddColumnsToTable(rdr, tabs, eEntityType);
                            break;
                        case "AssociationSet":
                            BuildRelationshipFromAssociationSet(rdr, tabs, eAssociationSet);
                            break;
                        case "Association":
                            UpdateRelationshipFromAssociation(rdr, tabs);
                            break;
                    }

                }

            }

            // post processing of metadata
            foreach (var t in tabs)
            {
                TagKpiComponentColumns(t);
            }

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
                bool enabled;
                if (rdr.NodeType == XmlNodeType.Element)
                {
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
                bool enabled;
                if (rdr.NodeType == XmlNodeType.Element)
                {
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
            string refname = string.Empty;
            string toColumnRef = string.Empty;
            string fromColumnRef = string.Empty;
            string toColumnMultiplicity = string.Empty;
            string fromColumnMultiplicity = string.Empty;

            while (rdr.MoveToNextAttribute())
            {
                switch (rdr.LocalName)
                {
                    case "Name":
                        refname = rdr.Value;
                        break;
                }
            }

            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == "Association"))
            {
                // todo read entitySet as From/To table
                if (rdr.LocalName == "End")
                {
                    (fromColumnRef, fromColumnMultiplicity) = GetRelationshipColumnRef(rdr);
                    (toColumnRef, toColumnMultiplicity) = GetRelationshipColumnRef(rdr);
                }
                if (rdr.NodeType == XmlNodeType.EndElement
                    && rdr.LocalName == "Association") break;
                
                rdr.Read();
            }

            // Find relationship and update it
            foreach (var tab in tabs)
            {
                foreach (var rel in tab.Relationships)
                {
                    if (rel.InternalName == refname)
                    {
                        rel.ToColumn = toColumnRef;
                        rel.ToColumnMultiplicity = toColumnMultiplicity;
                        rel.FromColumn = fromColumnRef;
                        rel.FromColumnMultiplicity = fromColumnMultiplicity;
                        return;
                    }
                }
            }

        }

        private static void BuildRelationshipFromAssociationSet(XmlReader rdr, ADOTabularTableCollection tabs, string eAssociationSet)
        {
            string refname = string.Empty;
            string fromTableRef = "";
            string toTableRef = "";
            string crossFilterDir = "";

            while (rdr.MoveToNextAttribute())
            {
                switch (rdr.LocalName)
                {
                    case "Name":
                        refname = rdr.Value;
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
                    //rdr.MoveToFirstAttribute();
                    if (rdr.MoveToAttribute("CrossFilterDirection"))
                    {
                        crossFilterDir = rdr.Value;
                    }
                }
                rdr.Read();
            }

            var fromTable = tabs.GetById(fromTableRef);
            fromTable.Relationships.Add(new ADOTabularRelationship() {
                FromTable = fromTableRef,
                ToTable = toTableRef,
                InternalName = refname,
                CrossFilterDirection = crossFilterDir
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

        private static Tuple<string,string> GetRelationshipColumnRef(XmlReader rdr)
        {
            string role = string.Empty;
            string multiplicity = string.Empty;

            while (!(rdr.NodeType == XmlNodeType.EndElement
                     && rdr.LocalName == "End"))
            {
                if (rdr.MoveToAttribute("Role"))
                {
                    role = rdr.Value;
                }

                if (rdr.MoveToAttribute("Multiplicity"))
                {
                    multiplicity = rdr.Value;
                }

                rdr.Skip();
                rdr.MoveToContent();
                return Tuple.Create(role, multiplicity);
            }
            return Tuple.Create( "","");
        }


        private ADOTabularTable BuildTableFromEntitySet(XmlReader rdr, string eEntitySet)
        {
            string caption = null;
            string description = "";
            string refname = null;
            bool isVisible = true;
            string name = null;
            bool _private = false;
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
                            refname = rdr.Value;
                            break;
                        case "Private":
                            _private = bool.Parse(rdr.Value);
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
            var tab = new ADOTabularTable(_conn, refname, name, caption, description, isVisible, _private, showAsVariationsOnly);

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
            string dataType = "";
            string contents = "";
            string minValue = "";
            string maxValue = "";
            string formatString = "";
            string keyRef = "";
            string defaultAggregateFunction = "";
            long stringValueMaxLength = 0;
            long distinctValueCount = 0;
            bool nullable = true;
            ADOTabularTable tab = null;

            IFormatProvider invariantCulture = System.Globalization.CultureInfo.InvariantCulture;

            List<ADOTabularVariation> _variations = new List<ADOTabularVariation>();

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
                                string tableId = rdr.Value;
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
                //    rdr.MoveToAttribute("Contents");
                    var contentAttr = rdr.GetAttribute("Contents");

                    bool isDateTable = contentAttr == "Time";
                    tab.IsDateTable = isDateTable;
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
                    _variations = ProcessVariations(rdr);
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
                                DataType = Type.GetType($"System.{dataType}"),
                                Nullable = nullable,
                                MinValue = minValue,
                                MaxValue = maxValue,
                                DistinctValues = distinctValueCount,
                                FormatString = formatString,
                                StringValueMaxLength = stringValueMaxLength,
                                DefaultAggregateFunction = defaultAggregateFunction,
                            };
                            col.Variations.AddRange(_variations);
                            tables.Model.AddRole(col);
                            tab.Columns.Add(col);
                            _conn.Columns.Add(col.DaxName, col);
                        }
                        else
                        {
                            colType = ADOTabularObjectType.KPI;
                            var kpiCol = new ADOTabularKpi(tab, refName, name, caption, description, isVisible, colType, contents, kpi)
                            {
                                DataType = Type.GetType($"System.{dataType}")
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
                    _variations = new List<ADOTabularVariation>();

                }
                if (!rdr.Read()) break;// quit the read loop if there is no more data
            }

            // Set Key column
            var keyCol = tab?.Columns.GetByPropertyRef(keyRef);
            if(keyCol != null) keyCol.IsKey = true;

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

        private static List<ADOTabularVariation> ProcessVariations(XmlReader rdr)
        {
            string _name;
            bool _default = false;
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
                                _default = bool.Parse( rdr.Value);
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
                    _variations.Add(new ADOTabularVariation() { NavigationPropertyRef = navigationPropertyRef, DefaultHierarchyRef = defaultHierarchyRef, IsDefault = _default });
                    _default = false;
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
                    IADOTabularFolderReference folder = new ADOTabularDisplayFolder(folderCaption, folderReference);
                    parent.FolderItems.Add(folder);

                    rdr.ReadToNextElement();

                    // recurse down to child items
                    ProcessDisplayFolder(rdr, table, folder);
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
                    IADOTabularObjectReference reference = new ADOTabularObjectReference("", objRef);
                    parent.FolderItems.Add(reference);
                    var column = table.Columns.GetByPropertyRef(objRef);
                    if (column != null) { column.IsInDisplayFolder = true; }
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
                    string structure = GetHierarchStructure(table, hierName, hierCap);
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
            if (functionGroups == null) throw new ArgumentNullException(nameof(functionGroups));
            DataRow[] drFuncs = _conn.GetSchemaDataSet("MDSCHEMA_FUNCTIONS",null,false).Tables[0].Select("ORIGIN=3 OR ORIGIN=4");
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
#pragma warning disable IDE0050 // Convert to tuple
                         select new { Keyword = (string)keyword["Keyword"] , Matched = (kword==null) };
#pragma warning restore IDE0050 // Convert to tuple

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
            AdomdDataReader result = _conn.ExecuteReader("SELECT * FROM $SYSTEM.MDSCHEMA_FUNCTIONS");
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
                if (ssasVersion.StartsWith("13.",StringComparison.InvariantCultureIgnoreCase)) {
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
            else if (serverName.StartsWith("powerbi://", StringComparison.InvariantCultureIgnoreCase)       // Power BI XMLA EndPoint (Premium, Premium per User)
                || serverName.StartsWith("pbiazure://", StringComparison.InvariantCultureIgnoreCase)        // Power BI Dataset
                || serverName.StartsWith("pbidedicated://", StringComparison.InvariantCultureIgnoreCase) )  // Power BI Premium Internal
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
                if (ssasVersion.StartsWith("15.", StringComparison.InvariantCultureIgnoreCase)) {
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
            // (csdl doesnt seem to have the DAX expression)

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

            using AdomdDataReader result = _conn.ExecuteReader(QUERY_REMAP_COLUMNS);
            while (result.Read())
            {
                string columnId = GetString(result, 0);
                string columnName = GetString(result, 1);
                daxColumnsRemap.RemapNames.Add(columnId, columnName);
            }
        }
    }

}
