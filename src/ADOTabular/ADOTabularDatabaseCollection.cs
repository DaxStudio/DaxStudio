using System;
using System.Collections.Generic;
using System.Data;
using System.Collections;
using System.Xml;
using System.IO;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Interfaces;

namespace ADOTabular
{
    public class DatabaseDetails: IDatabaseReference
    {
        public DatabaseDetails(string name, string id, string caption, string lastUpdate, string compatLevel, string roles, string description)
        {
            Name = name;
            Id = id;
            Caption = caption;
            _ = DateTime.TryParse(lastUpdate, out DateTime lastUpdatedDate);
            LastUpdate = lastUpdatedDate;
            CompatibilityLevel = compatLevel;
            Roles = roles;
            Description = description;
        }

        public DatabaseDetails(DataRow row, string caption)
        {
            Name = row["CATALOG_NAME"].ToString();
            Id = row.Table.Columns.Contains("DATABASE_ID") ? row["DATABASE_ID"].ToString() : string.Empty;
            Caption = caption?.Length > 0 ? caption : row["CATALOG_NAME"].ToString();
            _ = DateTime.TryParse(row["DATE_MODIFIED"].ToString(), out DateTime lastUpdatedDate);
            LastUpdate = lastUpdatedDate;
            CompatibilityLevel = row.Table.Columns.Contains("COMPATIBILITY_LEVEL") ? row["COMPATIBILITY_LEVEL"].ToString() : string.Empty;
            Roles = row.Table.Columns.Contains("ROLES") ? row["ROLES"].ToString() : string.Empty;
            Description = row.Table.Columns.Contains("DESCRIPTION") ? row["DESCRIPTION"].ToString() : string.Empty;
        }

        public string Name { get; set; }
        public string Id {get; }
        public DateTime LastUpdate {get; set;}
        public string CompatibilityLevel { get; internal set; }
        public string Roles { get; internal set; }
        public string Caption { get; set; }

        public string Description { get; set; }
    }
    public class ADOTabularDatabaseCollection:IEnumerable<DatabaseDetails>
    {
        private DataSet _dsDatabases;
        private readonly ADOTabularConnection _adoTabConn;
        public ADOTabularDatabaseCollection(ADOTabularConnection adoTabConn)
        {
            _adoTabConn = adoTabConn;
            
        }

        private DataTable GetDatabaseTable()
        {
            if (_dsDatabases == null )
            {
                if (_adoTabConn.ServerType == Enums.ServerType.Offline)
                {
                    var dt = new DataTable();
                    dt.Columns.Add("CATALOG_NAME", typeof(string));
                    dt.Columns.Add("DATE_MODIFIED", typeof(string));
                    dt.Rows.Add((_adoTabConn.Database.Name));
                    _dsDatabases = _dsDatabases ??new DataSet();
                    _dsDatabases.Tables.Add(dt);
                }
                else
                {
                    _dsDatabases = _adoTabConn.GetSchemaDataSet("DBSCHEMA_CATALOGS");
                    
                }
                _dsDatabases.Tables[0].PrimaryKey = new[] {
                    _dsDatabases.Tables[0].Columns["CATALOG_NAME"]
                };
            }

            return _dsDatabases.Tables[0];
        }

        private IDictionary<string, DatabaseDetails> _databaseDictionary;

        public IDictionary<string, DatabaseDetails> GetDatabaseDictionary(int spid)
        {
            return GetDatabaseDictionary(spid, false);
        }

        object _getDatabaseLock = new object();
        public IDictionary<string, DatabaseDetails> GetDatabaseDictionary(int spid, bool refresh)
        {

            if (_databaseDictionary == null && _adoTabConn.ServerType == Enums.ServerType.Offline) 
            {
                _databaseDictionary = new Dictionary<string, DatabaseDetails>();
                var db = _adoTabConn.Database;
                DatabaseDetails tmpDD = new DatabaseDetails(db.Name, db.Id, db.Caption, string.Empty, db.CompatibilityLevel, string.Empty, db.Description);
                _databaseDictionary.Add(db.Name, tmpDD);
                return _databaseDictionary;
            }

            lock (_getDatabaseLock)
            {

                if (_databaseDictionary != null && !refresh) return _databaseDictionary;

                IDictionary<string, DatabaseDetails> tmpDatabaseDict;
                if (spid != -1)
                {
                    tmpDatabaseDict = GetDatabaseDictionaryFromXml();
                    var tmpDatabaseDmvDict = GetDatabaseDictionaryFromDMV();
                    foreach (var db in tmpDatabaseDict.Values)
                    {
                        db.CompatibilityLevel = tmpDatabaseDmvDict[db.Name].CompatibilityLevel;
                        db.Roles = tmpDatabaseDmvDict[db.Name].Roles;
                        if (_adoTabConn.FileName.Length > 0) db.Caption = _adoTabConn.ShortFileName;

                    }
                }
                else
                    tmpDatabaseDict = GetDatabaseDictionaryFromDMV();

                if (_databaseDictionary == null) _databaseDictionary = tmpDatabaseDict;
                else MergeDatabaseDictionaries(tmpDatabaseDict);

                return _databaseDictionary;
            }
        }

        private void MergeDatabaseDictionaries(IDictionary<string, DatabaseDetails> tmpDatabaseDict)
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

        private IDictionary<string, DatabaseDetails> GetDatabaseDictionaryFromDMV()
        {
            var databaseDictionary = new SortedDictionary<string, DatabaseDetails>(StringComparer.OrdinalIgnoreCase);
            var ds = _adoTabConn.GetSchemaDataSet("DBSCHEMA_CATALOGS", null);
            foreach( DataRow row in ds.Tables[0].Rows)
            {
                databaseDictionary.Add(row["CATALOG_NAME"].ToString(), new DatabaseDetails(
                    row["CATALOG_NAME"].ToString(),
                    row.Table.Columns.Contains("DATABASE_ID") ? row["DATABASE_ID"].ToString() : string.Empty,
                    _adoTabConn.ShortFileName?.Length > 0 ? _adoTabConn.ShortFileName: row["CATALOG_NAME"].ToString(),
                    row["DATE_MODIFIED"].ToString(),
                    row.Table.Columns.Contains("COMPATIBILITY_LEVEL")? row["COMPATIBILITY_LEVEL"].ToString(): string.Empty,
                    row.Table.Columns.Contains("ROLES") ? row["ROLES"].ToString() : string.Empty,
                    row.Table.Columns.Contains("DESCRIPTION") ? row["DESCRIPTION"].ToString() : string.Empty
                    )
                    );
                // TODO - add support for loading Database Description
            }
            return databaseDictionary;
        }

        private IDictionary<string, DatabaseDetails> GetDatabaseDictionaryFromXml()
        {
            
            var databaseDictionary = new SortedDictionary<string, DatabaseDetails>(StringComparer.OrdinalIgnoreCase);

            var ds = _adoTabConn.GetSchemaDataSet("DISCOVER_XML_METADATA",
                                                 new AdomdRestrictionCollection
                                                     {
                                                         new AdomdRestriction("ObjectExpansion", "ExpandObject")
                                                     });
            string metadata = ds.Tables[0].Rows[0]["METADATA"].ToString();

            using XmlReader rdr = new XmlTextReader(new StringReader(metadata)) { DtdProcessing = DtdProcessing.Ignore};
            if (rdr.NameTable == null) return databaseDictionary;
            var eDatabase = rdr.NameTable.Add("Database");

            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Element
                    && rdr.LocalName == eDatabase)
                {
                    string name = string.Empty;
                    string id = string.Empty;
                    string lastUpdate = string.Empty;
                    string compatLevel = string.Empty;
                    string description = string.Empty;
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
                                case "Description":
                                    description = rdr.ReadElementContentAsString();
                                    break;
                                default:
                                    rdr.Read();
                                    break;
                            }
                            continue;
                        }

                        var caption = _adoTabConn.ShortFileName?.Length > 0 ? _adoTabConn.ShortFileName : name;         
                                
                        if (rdr.NodeType == XmlNodeType.EndElement
                            && rdr.LocalName == eDatabase)
                        {
                            databaseDictionary.Add(name, new DatabaseDetails( name,  id, caption, lastUpdate,compatLevel,string.Empty, description));       
                        }

                        rdr.Read();
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

                throw new InvalidOperationException();
            }
        }


        public IEnumerator<DatabaseDetails> GetEnumerator()
        {
            foreach (DataRow dr in GetDatabaseTable().Rows)
            {
                //if (_adoTabConn.PowerBIFileName != string.Empty) {
                //    yield return _adoTabConn.PowerBIFileName;
                //}
                //else {
                    //yield return new ADOTabularDatabase(_adoTabConn, dr["CATALOG_NAME"].ToString());//, dr);
                    //yield return dr["CATALOG_NAME"].ToString();//, dr);
                yield return new DatabaseDetails(dr, _adoTabConn.ShortFileName);
                    
                    
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

        public void Add(ADOTabularDatabase db)
        {
            DatabaseDetails dd = new DatabaseDetails(db.Name, db.Id, db.Caption, db.LastUpdate.ToLongDateString(), db.CompatibilityLevel, db.Roles, db.Description);
            _databaseDictionary = _databaseDictionary ?? new Dictionary<string, DatabaseDetails>();
            _databaseDictionary.Add(dd.Name, dd);
        }

        public int Count => GetDatabaseTable().Rows.Count;

    }
}
