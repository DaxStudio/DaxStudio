using System;
using System.Collections.Generic;
using System.Data;
using System.Collections;
using ADOTabular.AdomdClientWrappers;
using System.Xml;
using System.IO;
using System.Collections.ObjectModel;

namespace ADOTabular
{
    public class DatabaseDetails
    {
        public DatabaseDetails(string name, string id, string lastUpdate, string compatLevel, string roles)
        {
            Name = name;
            Id = id;
            DateTime lastUpdatedDate = new DateTime(1900,1,1);
            DateTime.TryParse(lastUpdate, out lastUpdatedDate);
            LastUpdate = lastUpdatedDate;
            CompatibilityLevel = compatLevel;
            Roles = roles;
        }

        public DatabaseDetails(string name, string lastUpdate, string compatLevel, string roles) : this(name, string.Empty, lastUpdate, compatLevel, roles) { }
        
        public string Name {get;private set;}
        public string Id {get;private set;}
        public DateTime LastUpdate {get; set;}
        public string CompatibilityLevel { get; internal set; }
        public string Roles { get; internal set; }
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
                _dsDatabases.Tables[0].PrimaryKey = new DataColumn[] {
                    _dsDatabases.Tables[0].Columns["CATALOG_NAME"]
                };
            }

            return _dsDatabases.Tables[0];
        }

        private Dictionary<string, DatabaseDetails> _databaseDictionary;

        public Dictionary<string, DatabaseDetails> GetDatabaseDictionary(int spid)
        {
            return GetDatabaseDictionary(spid, false);
        }
        public Dictionary<string, DatabaseDetails> GetDatabaseDictionary(int spid, bool refresh)
        {
            //if (refresh) _databaseDictionary = null;
            if (_databaseDictionary != null && !refresh) return _databaseDictionary;

            Dictionary<string,DatabaseDetails> tmpDatabaseDict;
            if (spid != -1)
            {
                tmpDatabaseDict = GetDatabaseDictionaryFromXML();
                var tmpDatabaseDmvDict = GetDatabaseDictionaryFromDMV();
                foreach (var db in tmpDatabaseDict.Values)
                {
                    db.CompatibilityLevel = tmpDatabaseDmvDict[db.Name].CompatibilityLevel;
                    db.Roles = tmpDatabaseDmvDict[db.Name].Roles;
                }
            }
            else
                tmpDatabaseDict = GetDatabaseDictionaryFromDMV();

            if (_databaseDictionary == null) _databaseDictionary = tmpDatabaseDict;
            else MergeDatabaseDictionaries(tmpDatabaseDict);

            return _databaseDictionary;
        }

        private void MergeDatabaseDictionaries(Dictionary<string, DatabaseDetails> tmpDatabaseDict)
        {
            // Update the lastUpdated datetime
            foreach (var dbName in tmpDatabaseDict.Keys)
            {
                if (_databaseDictionary.ContainsKey(dbName)) {
                    _databaseDictionary[dbName].LastUpdate = tmpDatabaseDict[dbName].LastUpdate;
                } else
                {
                    _databaseDictionary.Add(dbName, tmpDatabaseDict[dbName]);
                }
            }

            //Delete databases no longer in the list
            List<string> keysToRemove = new List<string>();
            foreach(var dbName in  _databaseDictionary.Keys)
            {
                if (!tmpDatabaseDict.ContainsKey(dbName ))
                {
                   keysToRemove.Add(dbName);
                }
            }

            foreach (var key in keysToRemove)
            {
                _databaseDictionary.Remove(key);
            }
        }

        private Dictionary<string, DatabaseDetails> GetDatabaseDictionaryFromDMV()
        {
            var databaseDictionary = new Dictionary<string, DatabaseDetails>();
            var ds = _adoTabConn.GetSchemaDataSet("DBSCHEMA_CATALOGS", null);
            foreach( DataRow row in ds.Tables[0].Rows)
            {
                databaseDictionary.Add(row["CATALOG_NAME"].ToString(), new DatabaseDetails(
                    row["CATALOG_NAME"].ToString(),
                    row["DATE_MODIFIED"].ToString(),
                    row.Table.Columns.Contains("COMPATIBILITY_LEVEL")? row["COMPATIBILITY_LEVEL"].ToString():"",
                    row.Table.Columns.Contains("ROLES") ? row["ROLES"].ToString() : ""));
            }
            return databaseDictionary;
        }

        private Dictionary<string, DatabaseDetails> GetDatabaseDictionaryFromXML()
        {
            
            var databaseDictionary = new Dictionary<string, DatabaseDetails>();

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
                            string compatLevel = "";
                            while (!rdr.EOF)
                            {
                                if (rdr.NodeType == XmlNodeType.Element)
                                {
                                    switch (rdr.LocalName)
                                    {
                                        case "Name":
                                            name = rdr.ReadElementContentAsString();
                                            break;
                                        case "ID":
                                            id = rdr.ReadElementContentAsString();
                                            break;
                                        case "LastUpdate":
                                            lastUpdate = rdr.ReadElementContentAsString();
                                            break;
                                        case "CompatibilityLevel":
                                            compatLevel = rdr.ReadElementContentAsString();
                                            break;
                                        default:
                                            rdr.Read();
                                            break;
                                    }
                                    continue;
                                }                                 
                                    
                                    
                                
                                
                                if (rdr.NodeType == XmlNodeType.EndElement
                                    && rdr.LocalName == eDatabase)
                                {
                                    databaseDictionary.Add(name, new DatabaseDetails( name,  id, lastUpdate,compatLevel,""));
                                    
                                }

                                rdr.Read();
                            }
                        }

                    }
                }
            }
            return databaseDictionary;
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
                //if (_adoTabConn.PowerBIFileName != string.Empty) {
                //    yield return _adoTabConn.PowerBIFileName;
                //}
                //else {
                    //yield return new ADOTabularDatabase(_adoTabConn, dr["CATALOG_NAME"].ToString());//, dr);
                    yield return dr["CATALOG_NAME"].ToString();//, dr);
                //}
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
        /*
        public SortedSet<string> ToSortedSet()
        {
            var ss = new SortedSet<string>();
            foreach (var dbname in this)
            { ss.Add(dbname); }
            return ss;
        }*/
        

    }
}
