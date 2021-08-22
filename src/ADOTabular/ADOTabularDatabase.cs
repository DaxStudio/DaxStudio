using ADOTabular.Interfaces;
using System;
using System.Globalization;

namespace ADOTabular
{
    public class ADOTabularDatabase
    {
        private ADOTabularModelCollection _modelColl;

        public ADOTabularDatabase(IADOTabularConnection adoTabConn, string databaseName, string databaseId, DateTime lastUpdate, string compatLevel, string roles)
        {
            Connection = adoTabConn;
            Name = databaseName;
            Id = databaseId;
            LastUpdate = lastUpdate;
            CompatibilityLevel = compatLevel;
            Roles = roles;
        }

        public bool HasSchemaChanged()
        {
            try
            {
                var ddColl = Connection.Databases.GetDatabaseDictionary(Connection.SPID, true);
                if (ddColl.Count == 0) return false; // no databases on server
                var dd = ddColl[Name];
                if (dd.LastUpdate > LastUpdate)
                {
                    LastUpdate = dd.LastUpdate;
                    return true;
                }
            }
            catch (InvalidOperationException ex)
            {
                // do nothing - probably trying to check for changes while query is still running
                System.Diagnostics.Debug.WriteLine("HasSchemaChanged Error: {0}", ex.Message);
            }

            return false;
        }

        public string Culture { get; internal set; } = string.Empty;
        public string Id { get; }
        public DateTime LastUpdate { get; internal set; } = DateTime.MinValue;

        public string Name { get;
        //get { return _adoTabConn.PowerBIFileName == string.Empty? _databaseName: _adoTabConn.PowerBIFileName; }
        }
        private string _caption = string.Empty;
        public string Caption { get => string.IsNullOrEmpty(_caption) ? Name : _caption; set { _caption = value; } }
        public ADOTabularModelCollection Models => _modelColl ??= new ADOTabularModelCollection(Connection, this);

        public IADOTabularConnection Connection { get; }

        public string CompatibilityLevel { get; }

        public string Roles { get; }

        // if the list of roles for the database contains
        public bool IsAdmin { get {
                return Roles.Contains("*"); 
            }
        }

        public void ClearCache()
        {
            Connection.ExecuteCommand(string.Format(CultureInfo.InvariantCulture, @"
                <Batch xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">
                   <ClearCache>
                     <Object>
                       <DatabaseID>{0}</DatabaseID>   
                    </Object>
                   </ClearCache>
                 </Batch>
                ", !String.IsNullOrEmpty(Connection.Database.Id) ? Connection.Database.Id : Connection.Database.Name));
                  // 2018-02-20 Hotfix by MarcoRusso - the Database.Id is an empty string, fixed with Database.Name, but it should be investigated why the Id is empty, then remove this hotfix
        }

        //private Regex daxColumnRegex = new Regex(@"'?(?<table>.*)'?\[(?<column>[^\]]*)\]", RegexOptions.Compiled);
        //public ADOTabularColumn FindColumnByName(string fullColumnName)
        //{
        //    var m = daxColumnRegex.Match(fullColumnName);
        //    var tab = m.Groups["table"].Value;
        //    var col = m.Groups["column"].Value;
        //    this.Models

        //}

        public static MetadataImages MetadataImage => MetadataImages.Database;


    }
}
