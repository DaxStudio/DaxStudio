using System;
using System.Collections.Generic;
using System.Data;
using System.Collections;
using ADOTabular.AdomdClientWrappers;
using System.Xml;
using System.IO;

namespace ADOTabular
{
    public class DatabaseDetails
    {
        public string Name {get;set;}
        public string Id {get;set;}
        public DateTime LastUpdate {get;set;}
    }
    public class ADOTabularDatabaseCollection:IEnumerable<string>
    {
        private DataSet _dsDatabases;
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularDatabaseCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;
            
        }

        private DataTable GetDatabaseTable()
        {
            if (_dsDatabases == null)
            {
                _dsDatabases = _adoTabConn.GetSchemaDataSet("DBSCHEMA_CATALOGS");
            }
            _dsDatabases.Tables[0].PrimaryKey = new DataColumn[] {
                _dsDatabases.Tables[0].Columns["CATALOG_NAME"]
                };
            return _dsDatabases.Tables[0];
        }

        private Dictionary<string, DatabaseDetails> _databaseDictionary;

        public Dictionary<string, DatabaseDetails> GetDatabaseDictionary()
        {
            return GetDatabaseDictionary(false);
        }
        public Dictionary<string, DatabaseDetails> GetDatabaseDictionary(bool refresh)
        {
            if (refresh) _databaseDictionary = null;
            if (_databaseDictionary != null) return _databaseDictionary;
            _databaseDictionary = new Dictionary<string, DatabaseDetails>();

            var ds = _adoTabConn.GetSchemaDataSet("DISCOVER_XML_METADATA",
                                                 new AdomdRestrictionCollection
                                                     {
                                                         new AdomdRestriction("ObjectExpansion", "ExpandObject")
                                                     });
            string metadata = ds.Tables[0].Rows[0]["METADATA"].ToString();
            
            using (XmlReader rdr = new XmlTextReader(new StringReader(metadata)))
            {
                if (rdr.NameTable != null)
                {
                    var eDatabase = rdr.NameTable.Add("Database");
                    var eName = rdr.NameTable.Add("Name");
                    var eId = rdr.NameTable.Add("ID");
                    var eLastUpdate = rdr.NameTable.Add("LastUpdate");
                    while (rdr.Read())
                    {
                        if (rdr.NodeType == XmlNodeType.Element
                            && rdr.LocalName == eDatabase)
                        {
                            string name = "";
                            string id = "";
                            string lastUpdate = "";
                            while (rdr.Read())
                            {
                                if (rdr.NodeType == XmlNodeType.Element
                                    && rdr.LocalName == eName)
                                {
                                    name = rdr.ReadElementContentAsString();
                                }
                                if (rdr.NodeType == XmlNodeType.Element
                                    && rdr.LocalName == eId)
                                {
                                    id = rdr.ReadElementContentAsString();
                                }
                                if (rdr.NodeType == XmlNodeType.Element
                                    && rdr.LocalName == eLastUpdate)
                                {
                                    lastUpdate = rdr.ReadElementContentAsString();
                                }
                                if (rdr.NodeType == XmlNodeType.EndElement
                                    && rdr.LocalName == eDatabase)
                                {
                                    _databaseDictionary.Add(name, new DatabaseDetails { Name = name, Id = id, LastUpdate = DateTime.Parse(lastUpdate) });
                                    break;
                                }

                            }
                        }

                    }
                }
            }
            return _databaseDictionary;
        }

        public string this[int index]
        {
            get
            {
                int i = 0;
                foreach (DataRow dr in GetDatabaseTable().Rows)
                {
                    if (i == index)
                    {
                        return dr["CATALOG_NAME"].ToString();
                    }
                    i++;
                }

                throw new IndexOutOfRangeException();
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (DataRow dr in GetDatabaseTable().Rows)
            {
                //yield return new ADOTabularDatabase(_adoTabConn, dr["CATALOG_NAME"].ToString());//, dr);
                yield return dr["CATALOG_NAME"].ToString();//, dr);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Contains(string databaseName)
        {
            return GetDatabaseTable().Rows.Contains(databaseName);
        }

        public int Count { get { return GetDatabaseTable().Rows.Count; } }

        public SortedSet<string> ToSortedSet()
        {
            var ss = new SortedSet<string>();
            foreach (var dbname in this)
            { ss.Add(dbname); }
            return ss;
        }
    }
}
