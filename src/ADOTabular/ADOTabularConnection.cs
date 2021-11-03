using System;
using System.IO;
using System.Linq;
using System.Data;
using System.Xml;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Data.OleDb;
using System.Globalization;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Enums;
using ADOTabular.Extensions;
using ADOTabular.Utils;
using ADOTabular.Interfaces;

namespace ADOTabular
{
    public class ADOTabularConnection : IDisposable, IADOTabularConnection
    {
        private AdomdCommand _runningCommand;

        public event EventHandler ConnectionChanged;
        private AdomdConnection _adomdConn;
        private string _currentDatabase;
        private readonly Regex _LocaleIdRegex = new Regex("Locale Identifier\\s*=\\s*(\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ADOTabularConnection(string connectionString, AdomdType connectionType)
            : this(connectionString, connectionType, ADOTabularMetadataDiscovery.Csdl)
        { }

        public ADOTabularConnection(string connectionString, AdomdType connectionType, ADOTabularMetadataDiscovery visitorType)
            : this(connectionString, connectionType, true, visitorType)
        { }

        public ADOTabularConnection(string connectionString, AdomdType connectionType, bool showHidden)
            : this(connectionString, connectionType, showHidden, ADOTabularMetadataDiscovery.Csdl)
        { }


        public ADOTabularConnection(string connectionString, AdomdType connectionType, bool showHiddenObjects, ADOTabularMetadataDiscovery visitorType)
        {
            
            ShowHiddenObjects = showHiddenObjects;
            ConnectionString = connectionString;
            _adomdConn = new ADOTabular.AdomdClientWrappers.AdomdConnection(ConnectionString, connectionType);

            Type = connectionType;
            //   _adomdConn.ConnectionString = connectionString;

            //_adomdConn.Open();
            if (visitorType == ADOTabularMetadataDiscovery.Adomd)
            {
                Visitor = new MetaDataVisitorADOMD(this);
            }
            else
            {
                Visitor = new MetaDataVisitorCSDL(this);
            }
            ConnectionChanged?.Invoke(this, new EventArgs());
        }

        private ADOTabularDatabase _db;

        // returns the current database for the connection
        public ADOTabularDatabase Database
        {

            get
            {
                //_adomdConn.UnderlyingConnection.Databases
                if (_adomdConn == null) return null;
                try
                {
                    if (_adomdConn.State != ConnectionState.Open)
                    {
                        this.Open();
                    }
                    var dd = Databases.GetDatabaseDictionary(this.SPID);
                    
                    if (string.IsNullOrWhiteSpace(_currentDatabase) && _adomdConn.State == ConnectionState.Open) _currentDatabase = _adomdConn.Database;

                    if (!dd.ContainsKey(_currentDatabase))
                    {
                        dd = Databases.GetDatabaseDictionary(this.SPID, true);
                    }
                    //var db = dd[_adomdConn.Database];
                    if (string.IsNullOrEmpty(_currentDatabase) && dd.Count == 0)
                    {
                        // return an empty database object if there is no current database or no databases on the server
                        return new ADOTabularDatabase(this, "", "", DateTime.MinValue, "","");
                    }
                    // The Power BI XMLA endpoint does not set a default database, so we have a collection of database, but no current database
                    // in this case we just set the current database to the first in the list
                    if (string.IsNullOrEmpty(_currentDatabase) && dd.Count > 0)
                    {
                        var details = dd.First().Value;
                        _db = new ADOTabularDatabase(this, details.Name, details.Id, details.LastUpdate, details.CompatibilityLevel, details.Roles);
                        ChangeDatabase(details.Name);

                    }

                    // todo - somehow users are getting here, but the current database is not in the dictionary
                    var db = dd[_currentDatabase];
                    if (_db == null || db.Id != _db.Id) // && db.Name != FileName)
                    {
                        _db = new ADOTabularDatabase(this, db.Name, db.Id, db.LastUpdate, db.CompatibilityLevel, db.Roles);
                        _db.Caption = db.Caption;
                    } 

                    return _db;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in Database property: {ex.Message}");
                    throw;
                    //return null;
                }
            }
        }

        public void Open()
        {
            _adomdConn.Open();
            ChangeDatabase(_adomdConn.Database);
            CacheKeywords();
            CacheFunctionGroups();
            UpdateServerProperties();
            // We do not cache DaxMetadata intentionally - it is saved manually, there is no need to read them every time
        }

        private void CacheFunctionGroups()
        {
            _functionGroups ??= new ADOTabularFunctionGroupCollection(this);
        }

        private void CacheKeywords()
        {
            _keywords ??= new ADOTabularKeywordCollection(this);
        }

        /*       public void Open(string connectionString)
               {
                   _adomdConn.Open(connectionString);
                   if (ConnectionChanged!=null)
                       ConnectionChanged(this,new EventArgs());
               }
               */

        public void ChangeDatabase(string database)
        {
            _currentDatabase = database;
            if (_adomdConn.State != ConnectionState.Open)
            {
                _adomdConn.Open();
            }
            //if (PowerBIFileName != string.Empty)
            //{
            //    _currentDatabase = PowerBIFileName;
            //    ADOTabularDatabase db = Database;
            //    _adomdConn.ChangeDatabase(db.Id);
            //}
            //else
            //{
            if (_adomdConn.Database != database)
            {
                _adomdConn.ChangeDatabase(database);
            }

            //}
            ConnectionChanged?.Invoke(this, new EventArgs());

            _spid = 0; // reset the spid to 0 so that it will get re-evaluated
                       // the PowerBI xmla endpoint sets the permissions to call DISCOVER_SESSIONS on a per data set basis
                       // depending on whether the user has admin access to the given data set

        }

        private bool _showHiddenObjects;
        public bool ShowHiddenObjects
        {
            get => _showHiddenObjects;
            set
            {
                if (_adomdConn != null)
                {
                    if (_adomdConn.State == ConnectionState.Open)
                        throw new Exception("Cannot set the ShowHiddenObjects setting while the connection is open");
                }
                _showHiddenObjects = value;
            }
        }

        public ADOTabularConnectionType ConnectionType { get; private set; }


        public AdomdType Type
        {
            get;
            set;
        }


        public bool SupportsQueryTable => Type == AdomdType.AnalysisServices;

        public override string ToString()
        {
            return _adomdConn.ConnectionString;
        }

        private string _connectionString = "";

        public Dictionary<string, string> Properties { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string ConnectionString
        {
            get
            {
                var connstr = _connectionString;
                // TODO - do we need to set the initial catalog here??
                /* 
                if (!connstr.Contains("Initial Catalog") && Database != null)
                {
                    connstr = 
                        string.Format(
                            connstr.EndsWith(";")
                                ? "{0}Initial Catalog={1}"
                                : "{0};Initial Catalog={1}", connstr, Database.Name);
                }
                 */ 
                if (connstr.IndexOf("Show Hidden Cubes", StringComparison.OrdinalIgnoreCase) == -1 && ShowHiddenObjects)
                {
                    connstr =
                        string.Format(CultureInfo.InvariantCulture
                            , connstr.EndsWith(";", StringComparison.InvariantCulture)
                                ? "{0}Show Hidden Cubes=true"
                                : "{0};Show Hidden Cubes=true", connstr);
                }
                return connstr;
            }
            set { _connectionString = value;
                if (_connectionString != null)
                {
                    Properties = SplitConnectionString(_connectionString);
                    ConnectionType = GetConnectionType(ServerName);
                    //_connectionProps = ConnectionStringParser.Parse(_connectionString);
                }
            }
        }

        private static ADOTabularConnectionType GetConnectionType(string serverName)
        {
            var lowerServerName = serverName.Trim();
            if (lowerServerName.StartsWith("localhost",StringComparison.OrdinalIgnoreCase)) return ADOTabularConnectionType.LocalMachine;
            if (lowerServerName.StartsWith("asazure:", StringComparison.OrdinalIgnoreCase) 
             || lowerServerName.StartsWith("powerbi:", StringComparison.OrdinalIgnoreCase)) return ADOTabularConnectionType.Cloud;
            return ADOTabularConnectionType.LocalNetwork;
        }

        private static Dictionary<string, string> SplitConnectionString(string connectionString)
        {
            var props = ConnectionStringParser.Parse(connectionString);

            return props;
        }


        // In ADO we set the current DB in the connection string
        // so having a collection of database objects may not be 
        // appropriate
        //
        // currently just returning a collection of available database names (not database objects)
        private ADOTabularDatabaseCollection _adoTabDatabaseColl;
        public ADOTabularDatabaseCollection Databases
        {
            get { 
                if (_adoTabDatabaseColl == null)
                {
                    if (_adomdConn != null)
                    {
                    _adoTabDatabaseColl = new ADOTabularDatabaseCollection(this);
                    }
                    else
                    {
                        throw new InvalidOperationException("Unable to populate Databases collection - a valid connection has not been established");
                    }
                }
                return _adoTabDatabaseColl;
            }
        }
        
        public int Count
        {
            get
            {
                return _adoTabDatabaseColl.Count;
            }
        }
#pragma warning disable CA1725 // Parameter names should match base declaration
        public DataSet GetSchemaDataSet(string schemaName)
        {
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            return _adomdConn.GetSchemaDataSet(schemaName, null,true);
        }


        public DataSet GetSchemaDataSet(string schemaName, AdomdRestrictionCollection restrictionCollection)
        {
            if (_adomdConn.State != ConnectionState.Open)
            {
                _adomdConn.Open();
            }
            return _adomdConn.GetSchemaDataSet(schemaName, restrictionCollection,true);
        }

        public DataSet GetSchemaDataSet(string schemaName, AdomdRestrictionCollection restrictionCollection, bool throwOnInlineErrors)
        {
            if (_adomdConn.State != ConnectionState.Open)
            {
                _adomdConn.Open();
            }
            return _adomdConn.GetSchemaDataSet(schemaName, restrictionCollection, throwOnInlineErrors);
            
        }
#pragma warning restore CA1725 // Parameter names should match base declaration


        public void ExecuteNonQuery(string command)
        {
            var cmd = _adomdConn.CreateCommand();
            try
            {
                cmd.CommandText = command;
                cmd.CommandType = CommandType.Text;

                _ = cmd.ExecuteNonQuery();
            }
            finally
            {
                cmd.Dispose();
            }

        }

        public void CancelSPID(int spid)
        {
            var cmd = $"<Cancel xmlns='http://schemas.microsoft.com/analysisservices/2003/engine'><SPID>{spid}</SPID></Cancel>";
            ExecuteNonQuery(cmd);
        }

        public void CancelQuery()
        {
            const string cmd = "<Cancel xmlns='http://schemas.microsoft.com/analysisservices/2003/engine'/>";
            ExecuteNonQuery(cmd);
        }

        public void Ping()
        {
            const string cmd = "<Batch xmlns='http://schemas.microsoft.com/analysisservices/2003/engine'/>";
            ExecuteNonQuery(cmd);
        }

        public void PingTrace()
        {

            // Ping the server by sending a discover request for the current catalog name
            //var restrictionCollection = new AdomdRestrictionCollection();
            //var restriction = new AdomdRestriction("PropertyName", "Catalog");
            //restrictionCollection.Add(restriction);
            _ = GetSchemaDataSet("MDSCHEMA_CUBES");

            //ExecuteNonQuery(cmd);
        }


        public ConnectionState State
        {
            get {
                if (_adomdConn == null) return ConnectionState.Closed;
                return _adomdConn.State;
            }
        }

        public ADOTabular.AdomdClientWrappers.AdomdDataReader ExecuteReader(string command, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList)
        {
            if (_runningCommand != null)
            {
                _runningCommand.Dispose();
                _runningCommand = null;
            }

            _runningCommand = _adomdConn.CreateCommand();
            _runningCommand.CommandType = CommandType.Text;
            _runningCommand.CommandText = command;

            if (paramList != null)
            {
                foreach (var p in paramList)
                {
                    _runningCommand.Parameters.Add(p);
                }
            }

            // TOOO - add parameters to connection

            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            AdomdDataReader rdr = _runningCommand.ExecuteReader();
            rdr.Connection = this;
            rdr.CommandText = command;
     
            return rdr;

        }

        public DataTable ExecuteDaxQueryDataTable(string query)
        {
            _runningCommand = _adomdConn.CreateCommand();
            _runningCommand.CommandType = CommandType.Text;
            _runningCommand.CommandText = query;
            var dt = new DataTable("DAXResult");
            using (var da = new AdomdDataAdapter(_runningCommand))
            {
                if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
                da.Fill(dt);
            }
            _runningCommand.Dispose();
            _runningCommand = null;
            return dt;
        }

        public int ExecuteCommand(string command) {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            try
            {
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = command;
                if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
                return cmd.ExecuteNonQuery();
            }
            finally
            {
                cmd.Dispose();
            }
        }     


        public void Close(bool endSession)
        {
            if (_adomdConn.State != ConnectionState.Closed && _adomdConn.State != ConnectionState.Broken)
            {
                _adomdConn.Close(endSession);
                _spid = 0;
            }
        }

        public void Close()
        {
            if (_adomdConn.State != ConnectionState.Closed && _adomdConn.State != ConnectionState.Broken)
            {
                _adomdConn.Close();
                _spid = 0;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _adomdConn?.Dispose();
                _runningCommand?.Dispose();
                _runningCommand = null;
                _spid = 0;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private ADOTabularFunctionGroupCollection _functionGroups;
        public ADOTabularFunctionGroupCollection FunctionGroups
        {
            get {
                CacheFunctionGroups();
                return _functionGroups;
            }
        }

        public IEnumerable<string> AllFunctions
        {
            get
            {
                foreach(var fg in FunctionGroups)
                {
                    foreach(var f in fg.Functions )
                    {
                        yield return f.Caption;
                    }
                }
                
            }
        }

        private MetadataInfo.DaxMetadata _daxMetadataInfo;
        public MetadataInfo.DaxMetadata DaxMetadataInfo {
            get {
                CacheDaxMetadataInfo();
                return _daxMetadataInfo;
            }
        }

        public void CacheDaxMetadataInfo()
        {
            if (_daxMetadataInfo == null) _daxMetadataInfo = new MetadataInfo.DaxMetadata(this);
        }

        private MetadataInfo.DaxColumnsRemap _daxColumnsRemapInfo;
        public MetadataInfo.DaxColumnsRemap DaxColumnsRemapInfo
        {
            get
            {
                CacheColumnRemapInfo();
                return _daxColumnsRemapInfo;
            }
        }

        public void CacheColumnRemapInfo()
        {
            if (_daxColumnsRemapInfo == null) _daxColumnsRemapInfo = new MetadataInfo.DaxColumnsRemap(this);
        }

        private MetadataInfo.DaxTablesRemap _daxTablesRemapInfo; 
        public MetadataInfo.DaxTablesRemap DaxTablesRemapInfo
        {
            get
            {
                CacheTableRemapInfo();
                return _daxTablesRemapInfo;
            }
        }

        public void CacheTableRemapInfo() {
            if (_daxTablesRemapInfo == null) _daxTablesRemapInfo = new MetadataInfo.DaxTablesRemap(this);
        }

        private ADOTabularKeywordCollection _keywords;
        public ADOTabularKeywordCollection Keywords
        {
            get {
                CacheKeywords(); 
                return _keywords;
            }
        }
        
        private ADOTabularDynamicManagementViewCollection _dmvCollection;
        public ADOTabularDynamicManagementViewCollection DynamicManagementViews
        {
            get {
                if (_dmvCollection == null)
                {
                    _dmvCollection = new ADOTabularDynamicManagementViewCollection(this);
                }

                return _dmvCollection ;
            }
        }

        public string ServerName
        {
            get
            {
                foreach(var prop in ConnectionString.Split(';'))
                {
                    if (prop.Trim().Length ==0) continue;
                    var p = prop.Split('=');
                    if (p[0] == "Data Source") return p[1].TrimStart('"').TrimEnd('"');
                }
                return "Not Connected";
            }
        }

        private string _svrVersion;
        public string ServerVersion
        {
            get
            {
                if (_svrVersion == null)
                {
                    _svrVersion = _adomdConn.ServerVersion;

                }
                return _svrVersion;
            }
            set => _svrVersion = value;
        }
        public string SessionId
        { 
            get { return _adomdConn.SessionID; }
        }

        private string _serverMode;
        public string ServerMode
        {
            get
            {
                if (_serverMode == null)
                {
                    _serverMode = GetServerMode();
                }
                return _serverMode;
            }
        }

        private string GetServerMode()
        {
            
            var ds = _adomdConn.GetSchemaDataSet("DISCOVER_XML_METADATA",
                                                 new AdomdRestrictionCollection
                                                     {
                                                         new AdomdRestriction("ObjectExpansion", "ReferenceOnly")
                                                     },true);
            string metadata = ds.Tables[0].Rows[0]["METADATA"].ToString();
            
            using (XmlReader rdr = new XmlTextReader(new StringReader(metadata)) { DtdProcessing = DtdProcessing.Prohibit })
            {
                if (rdr.NameTable != null)
                {
                    var eSvrMode = rdr.NameTable.Add("ServerMode");

                    while (rdr.Read())
                    {
                        if (rdr.NodeType == XmlNodeType.Element
                            && rdr.LocalName == eSvrMode)
                        {
                            return rdr.ReadElementContentAsString();
                        }

                    }
                }
            }
            return "Unknown";
        }


        private string _serverId;
        public string ServerId {
            get {
                if (_serverId == null) {
                    _serverId = GetServerId();
                }
                return _serverId;
            }
        }

        private string GetServerId() {

            var ds = _adomdConn.GetSchemaDataSet("DISCOVER_XML_METADATA",
                                                 new AdomdRestrictionCollection
                                                     {
                                                         new AdomdRestriction("ObjectExpansion", "ReferenceOnly")
                                                     }, true);
            string metadata = ds.Tables[0].Rows[0]["METADATA"].ToString();

            using (XmlReader rdr = new XmlTextReader(new StringReader(metadata)) { DtdProcessing = DtdProcessing.Prohibit }) {
                if (rdr.NameTable != null) {
                    var eSvrMode = rdr.NameTable.Add("ID");

                    while (rdr.Read()) {
                        if (rdr.NodeType == XmlNodeType.Element
                            && rdr.LocalName == eSvrMode) {
                            return rdr.ReadElementContentAsString();
                        }

                    }
                }
            }
            return "Unknown";
        }

        private int _spid;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>")]
        public int SPID
        {
            get
            {
                if (_spid == 0)
                {
                    try
                    {
                        //var resColl = new AdomdRestrictionCollection {{"SESSION_ID", SessionID}};
                        //var ds = GetSchemaDataSet("DISCOVER_SESSIONS", resColl);
                        var ds = GetSchemaDataSet("DISCOVER_SESSIONS");
                        foreach (var dr in ds.Tables[0].Rows.Cast<DataRow>().Where(dr => dr["SESSION_ID"].ToString() == SessionId))
                        {
                            _spid = int.Parse(dr["SESSION_SPID"].ToString(),CultureInfo.InvariantCulture);
                        }
                    }
                    catch (Exception )
                    {
                        _spid = -1;  // non-adminstrators cannot run DISCOVER_SESSIONS so we will return -1
                    }
                }
                return _spid;
            }
        }

        public IMetaDataVisitor Visitor { get; set; }

        public void Cancel()
        {
            
            if (_runningCommand == null)
            {
                return;
            }
            _runningCommand.Cancel();

        }


        public bool IsMultiDimensional => ServerMode == "Multidimensional";

        public bool IsPowerPivot {get; set;}
        public bool IsPowerBIorSSDT => ServerType == ServerType.PowerBIDesktop || ServerType == ServerType.SSDT;

        // BeginQueryAsync
        /*
        public void BeginQueryAsync(string query)
        {
            Task<TResult> t = new Task(ExecuteDaxQueryDataTableTask, query);
            t.Start();
        }

        public void ExecuteDaxQueryDataTableTask(string query)
        {
            ExecuteDaxQueryDataTable(query)
        }
        */
        // QueryComplete

        private string _powerBIFileName = string.Empty;
        private string _currentCube = string.Empty;

        public string FileName
        {
            get => _powerBIFileName;
            set
            {
                _powerBIFileName = value ?? throw new ArgumentNullException(nameof(FileName));
                if (string.IsNullOrEmpty(_powerBIFileName)) return;
                if (_powerBIFileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                  || _powerBIFileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    ShortFileName = _powerBIFileName.Split('/').Last();
                }
                else
                {
                    try
                    {
                        ShortFileName = new FileInfo(_powerBIFileName).Name;
                    }
                    catch
                    {
                        // if there are any errors fallback to using the filename as the ShortName
                        ShortFileName = _powerBIFileName;
                    }
                }
            }
        }


        public void SetCube(string cubeName)
        {
            _currentCube = cubeName;
            _adomdConn.Close();
            _adomdConn = new ADOTabular.AdomdClientWrappers.AdomdConnection($"{ConnectionString};Cube={cubeName};Initial Catalog={Database.Name}", AdomdType.AnalysisServices);
        }

        public bool Is2012SP1OrLater
        {
            get
            {
                return Version.Parse(ServerVersion) >= new Version(11, 0, 3368, 0);
            }
        }

        public string ApplicationName
        {
            get { 
                if (Properties == null) return "";
                if (!Properties.ContainsKey("Application Name")) return "";
                return Properties["Application Name"];
            }
        }

        public void Refresh()
        {
            Columns.Clear();
            _adoTabDatabaseColl = null;
            _db = null;
            _adomdConn.RefreshMetadata();
        }

        // This method forces in the Initial Catalog and Cube settings to the connection string 
        public string ConnectionStringWithInitialCatalog {
            get {
                var builder = new OleDbConnectionStringBuilder(ConnectionString)
                {
                    ["Initial Catalog"] = _currentDatabase
                };
                if (!string.IsNullOrEmpty(_currentCube)) builder["Cube"] = _currentCube;
                return builder.ToString();
                
                //return string.Format("{0};Initial Catalog={1}{2}", this.ConnectionString , _currentDatabase, CurrentCubeInternal);
            }
        }

        internal object CurrentCubeInternal => string.IsNullOrEmpty(_currentCube) ? string.Empty : $";Cube={_currentCube}";

        public Dictionary<string, ADOTabularColumn> Columns { get; } = new Dictionary<string, ADOTabularColumn>();

        public ServerType ServerType { get; set; }
        public string ServerLocation { get; private set; }
        public string ServerEdition { get; private set; }

        public int LocaleIdentifier { get {
                if (_adomdConn == null) return 0;
                if (_adomdConn.ConnectionString == null) return 0;
                if (_adomdConn.ConnectionString.Trim().Length == 0) return 0;
                var m = _LocaleIdRegex.Match(_adomdConn.ConnectionString);
                if (!m.Success) return 0;
                return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        public bool IsPowerBIXmla { get => this.Properties["Data Source"].IsPowerBIService(); }
        public string ShortFileName { get; private set; }

        public bool IsAdminConnection => SPID != -1 || HasRlsParameters() || IsPowerBIXmla;

        public bool IsTestingRls => HasRlsParameters();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "These properties are not critical so we just set them to empty strings on any exception")]
        private void UpdateServerProperties()
        {
            var res = new AdomdRestrictionCollection
            {
                new AdomdRestriction("ObjectExpansion", "ReferenceOnly")
            };
            var props = _adomdConn.GetSchemaDataSet("DISCOVER_XML_METADATA", res, true);
            var xmla = props.Tables[0].Rows[0][0].ToString();
            var xdoc = new XmlDocument() { XmlResolver = null  };
            var oMgr = new XmlNamespaceManager(xdoc.NameTable);
            oMgr.AddNamespace("d", "http://schemas.microsoft.com/analysisservices/2003/engine");
            oMgr.AddNamespace("ddl400", "http://schemas.microsoft.com/analysisservices/2012/engine/400");
            System.IO.StringReader sreader = new System.IO.StringReader(xmla);
            using (XmlReader reader = XmlReader.Create(sreader, new XmlReaderSettings() { XmlResolver = null }))
            {
                xdoc.Load(reader);
            }

            try
            {
                ServerLocation = xdoc.SelectSingleNode("//ddl400:ServerLocation", oMgr).InnerText ?? "";
            }
            catch
            {
                ServerLocation = "";
            }
            try
            {
                ServerEdition = xdoc.SelectSingleNode("//d:Edition", oMgr).InnerText ?? "";
            }
            catch
            {
                ServerEdition = "";
            }
        }

        public ADOTabularConnection Clone(bool sameSession)
        {
            return CloneInternal(this.ConnectionStringWithInitialCatalog, sameSession);
        }

        public ADOTabularConnection Clone()
        {
            return CloneInternal(this.ConnectionStringWithInitialCatalog,false);
        }

        /// <summary>
        /// Checks if the connection string contains any of the RLS testing parameters
        /// </summary>
        /// <returns>bool</returns>
        public bool HasRlsParameters()
        {
            var builder = new OleDbConnectionStringBuilder(ConnectionStringWithInitialCatalog);
            foreach (var param in rlsParameters)
            {
                if (builder.ContainsKey(param)) return true;
            }
            return false;
        }

        private static string[] rlsParameters = { "Roles", "EffectiveUserName","Authentication Scheme","Ext Auth Info" };
        public ADOTabularConnection CloneWithoutRLS()
        {
            var builder = new OleDbConnectionStringBuilder(ConnectionStringWithInitialCatalog);
            foreach (var param in rlsParameters)
            {
                builder.Remove(param);
            }   
            var newConnStr = builder.ToString();
            return CloneInternal(newConnStr,false, false);
        }

        private ADOTabularConnection CloneInternal(string connectionString, bool sameSession)
        {
            return CloneInternal(connectionString, sameSession, true);
        }
        private ADOTabularConnection CloneInternal(string connectionString, bool sameSession, bool copyDatabaseReference)
        {
            var connStrBuilder = new System.Data.OleDb.OleDbConnectionStringBuilder(connectionString);
            if(sameSession) connStrBuilder["SessionId"] = _adomdConn.SessionID;
            var newConnStr = connStrBuilder.ToString();
            var cnn = new ADOTabularConnection(newConnStr, this.Type)
            {
                // copy keywords, functiongroups, DMV's
                _functionGroups = this._functionGroups,
                _keywords = this._keywords,
                _serverMode = this._serverMode,
                _dmvCollection = this._dmvCollection,
                ServerType = this.ServerType
            };

            if (copyDatabaseReference)
            {
                cnn._db = this._db;
                cnn._adoTabDatabaseColl = this._adoTabDatabaseColl;
            }
            return cnn;
        }
    }

}
