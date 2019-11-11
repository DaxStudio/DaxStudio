using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Xml;

namespace ADOTabular
{
    public static class DiscoverXmlParser
    {
        public static Dictionary<string,string> Databases(XmlReader rdr)
        {
            Contract.Requires(rdr != null, "The rdr parameter must not be null");

            var dbs = new Dictionary<string, string>();

            if (rdr.NameTable == null) return dbs;

            var eDatabases = rdr.NameTable.Add("Databases");
            var eDatabase = rdr.NameTable.Add("Database");
            var eName = rdr.NameTable.Add("Name");
            var eID = rdr.NameTable.Add("ID");
            string name;
            string id;
            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == eDatabases)
                {
                    while (rdr.Read())
                    {
                        if (rdr.NodeType == XmlNodeType.Element
                            && rdr.LocalName == eDatabase)
                        {
                            name = "";
                            id = "";
                            while (rdr.Read())
                            {
                                if (rdr.NodeType == XmlNodeType.Element
                                && rdr.LocalName == eName)
                                {
                                    name = rdr.ReadElementContentAsString();    
                                }
                                if (rdr.NodeType == XmlNodeType.Element
                                && rdr.LocalName == eID)
                                {
                                    id = rdr.ReadElementContentAsString();
                                }

                                if (rdr.NodeType == XmlNodeType.EndElement
                                    && rdr.LocalName == eDatabase) break;
                            }
                            dbs.Add(name, id);
                        }
                        if (rdr.NodeType == XmlNodeType.EndElement
                            && rdr.LocalName == eDatabases) break;
                    }
                    if (rdr.NodeType == XmlNodeType.EndElement
                            && rdr.LocalName == eDatabases) break;
                }
            }
            return dbs;
        }

        public static Dictionary<string, string> ServerProperties(XmlReader rdr)
        {
            Contract.Requires(rdr != null, "The rdr parameter must not be null");

            var props = new Dictionary<string, string>();

            if (rdr.NameTable == null) return props;
            
            var eServerMode = rdr.NameTable.Add("ServerMode");
            
            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == eServerMode)
                {
                    props.Add("ServerMode", rdr.ReadElementContentAsString());
                }
            }
            return props;
        }
    }
}
