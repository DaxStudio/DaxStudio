using System;
using System.IO;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using System.Xml;
using ADOTabular.AdomdClientWrappers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using ADOTabular.Utils;
using System.Data.OleDb;

namespace ADOTabular
{

    [Flags]
    public enum MdschemaVisibility
    {
        Visible = 0x01,
        NonVisible = 0x02 
    }

    public enum ADOTabularMetadataDiscovery
    {
        Adomd
        ,Csdl
    }

    public class ADOTabularConnection : IDisposable, IADOTabularConnection
    {
        private AdomdCommand _runningCommand;

        public event EventHandler ConnectionChanged;
        private AdomdConnection _adomdConn;
        private readonly AdomdType _connectionType;
        private string _currentDatabase;
        private Regex _LocaleIdRegex = new Regex("Locale Identifier\\s*=\\s*(\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ADOTabularConnection(string connectionString, AdomdType connectionType)
            : this(connectionString, connectionType, ADOTabularMetadataDiscovery.Csdl)
        { }

        public ADOTabularConnection(string connectionString, AdomdType connectionType, ADOTabularMetadataDiscovery visitorType)
            : this(connectionString, connectionType, true, visitorType)
        { }

        public ADOTabularConnection(string connectionString, AdomdType connectionType, bool showHidden)
            : this(connectionString, connectionType, showHidden, ADOTabularMetadataDiscovery.Csdl)
        { }


        public ADOTabularConnection(string connectionString, AdomdType connectionType, bool showHiddenObjects, ADOTabularMetadataDiscovery vistorType)
        {
            
            ShowHiddenObjects = showHiddenObjects;
            ConnectionString = connectionString;
            _adomdConn = new AdomdConnection(ConnectionString, connectionType);
            _connectionType = connectionType;
            //   _adomdConn.ConnectionString = connectionString;

            //_adomdConn.Open();
            if (vistorType == ADOTabularMetadataDiscovery.Adomd)
            {
                Visitor = new MetaDataVisitorADOMD(this);
            }
            else
            {
                Visitor = new MetaDataVisitorCSDL(this);
            }
            if (ConnectionChanged != null)
                ConnectionChanged(this, new EventArgs());
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
                    if (_currentDatabase == "" && dd.Count == 0)
                    {
                        // return an empty database object if there is no current database or no databases on the server
                        return new ADOTabularDatabase(this, "", "", DateTime.MinValue, "","");
                    }
                    // The Power BI XMLA endpoint does not set a default database, so we have a collection of database, but no current database
                    // in this case we just set the current database to the first in the list
                    if (_currentDatabase == "" && dd.Count > 0)
                    {
                        var details = dd.First().Value;
                        _db = new ADOTabularDatabase(this, details.Name, details.Id, details.LastUpdate, details.CompatibilityLevel, details.Roles);
                        ChangeDatabase(details.Name);

                    }

                    // todo - somehow users are getting here, but the current database is not in the dictionary
                    var db = dd[_currentDatabase];
                    if (_db == null || db.Name != _db.Name)
                    {
                        _db = new ADOTabularDatabase(this, _currentDatabase, db.Id, db.LastUpdate, db.CompatibilityLevel, db.Roles);
                    }
                    return _db;
                }
                catch
                {
                    return null;
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
            if (_functionGroups == null) _functionGroups = new ADOTabularFunctionGroupCollection(this);
        }

        private void CacheKeywords()
        {
            if (_keywords == null) _keywords = new ADOTabularKeywordCollection(this);
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
            _adomdConn.ChangeDatabase(database);
            //}
            if (ConnectionChanged != null)
                ConnectionChanged(this, new EventArgs());

            _spid = 0; // reset the spid to 0 so that it will get re-evaluated
                       // the PowerBI xmla endpoint sets the permissions to call DISCOVER_SESSIONS on a per data set basis
                       // depending on whether the user has admin access to the given data set

        }

        private bool _showHiddenObjects;
        public bool ShowHiddenObjects
        {
            get { return _showHiddenObjects; }
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
            get { return _adomdConn.Type; }
        }



        public bool SupportsQueryTable
        {
            get
            {
                return _adomdConn.Type == AdomdType.AnalysisServices;
            }
        }

        public override string ToString()
        {
            return _adomdConn.ConnectionString;
        }

        private string _connectionString = "";
        private Dictionary<string, string> _connectionProps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase );
        public Dictionary<string, string> Properties {get{ return _connectionProps; }}
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
                        string.Format(
                            connstr.EndsWith(";")
                                ? "{0}Show Hidden Cubes=true"
                                : "{0};Show Hidden Cubes=true", connstr);
                }
                return connstr;
            }
            set { _connectionString = value;
                _connectionProps = SplitConnectionString(_connectionString);
                ConnectionType = GetConnectionType(ServerName);
                //_connectionProps = ConnectionStringParser.Parse(_connectionString);
            }
        }

        private ADOTabularConnectionType GetConnectionType(string serverName)
        {
            var lowerServerName = serverName.ToLower().Trim();
            if (lowerServerName.StartsWith("localhost")) return ADOTabularConnectionType.LocalMachine;
            if (lowerServerName.StartsWith("asazure:") 
             || lowerServerName.StartsWith("powerbi:")) return ADOTabularConnectionType.Cloud;
            return ADOTabularConnectionType.LocalNetwork;
        }

        private Dictionary<string, string> SplitConnectionString(string connectionString)
        {
            var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in connectionString.Split(';'))
            {
                if (prop.Trim().Length == 0) continue;
                var p = prop.Split('=');

                props.Add(p[0].Trim(), p[1].Trim());
            }
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
                        throw new NullReferenceException("Unable to populate Databases collection - a valid connection has not been established");
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
        public DataSet GetSchemaDataSet(string schemaName)
        {
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            return _adomdConn.GetSchemaDataSet(schemaName, null,true);
        }

         public Task<DataSet> GetSchemaDataSetAsync(string schemaName)
        {
            return Task.Run(() =>
                {
                    if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
                    return _adomdConn.GetSchemaDataSet(schemaName, null,true);
                });
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

        public Task<DataSet> GetSchemaDataSetAsync(string schemaName, AdomdRestrictionCollection restrictionCollection)
        {
            
            return Task.Run(() =>
                {
                    if (_adomdConn.State != ConnectionState.Open)
                        _adomdConn.Open();
                    return _adomdConn.GetSchemaDataSet(schemaName, restrictionCollection,true);
                });
        }

        public void ExecuteNonQuery(string command)
        {
            var cmd = _adomdConn.CreateCommand();
            cmd.CommandText = command;
            cmd.CommandType = CommandType.Text;
            
            cmd.ExecuteNonQuery();
        }

        public void CancelSPID(int spid)
        {
            var cmd =
                string.Format(
                    "<Cancel xmlns='http://schemas.microsoft.com/analysisservices/2003/engine'><SPID>{0}</SPID></Cancel>",
                    spid);
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

        private Func<AdomdDataReader> _execReader; 
        public IAsyncResult BeginExecuteDaxReader(string query, AsyncCallback callback)
        {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            _execReader = cmd.ExecuteReader;
            return _execReader.BeginInvoke(callback,null);
        }

        public ConnectionState State
        {
            get {
                if (_adomdConn == null) return ConnectionState.Closed;
                return _adomdConn.State;
            }
        }

        public void EndExecuteDaxReader(IAsyncResult result)
        {
            _execReader.EndInvoke(result);
        }

        public AdomdDataReader ExecuteReader(string query)
        {
            return ExecuteReader(query, 0);
        }
        public AdomdDataReader ExecuteReader(string query, int maxRows)
        {
            _runningCommand = _adomdConn.CreateCommand();
            _runningCommand.CommandType = CommandType.Text;
            _runningCommand.CommandText = query;
            //var dt = new DataTable("DAXResult");
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            AdomdDataReader rdr = _runningCommand.ExecuteReader();
            rdr.Connection = this;
            rdr.CommandText = query;
            return rdr;
            //int iRow = 0;
            //dt.BeginLoadData();
            //if (maxRows <= 0)
            //{
            //    dt.Load(rdr);
            //}
            //else
            //{
            //    while (rdr.Read())
            //    {
            //        DataRow dr = dt.NewRow();
            //        rdr.GetValues(dr.ItemArray);
            //        dt.ImportRow(dr);
            //        //dt.LoadDataRow(rdr[iRow], LoadOption.OverwriteChanges);
            //        iRow++;
            //    }
            //    dt.EndLoadData();
            //}
            ////while (rdr)
            //FixColumnNaming(dt);
            //_runningCommand = null;
            //return dt;
        }

        public DataTable ExecuteDaxQueryDataTable(string query)
        {
            _runningCommand = _adomdConn.CreateCommand();
            _runningCommand.CommandType = CommandType.Text;
            _runningCommand.CommandText = query;
            var da = new AdomdDataAdapter(_runningCommand);
            var dt = new DataTable("DAXResult");
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            da.Fill(dt);

            _runningCommand = null;
            return dt;
        }

        public DataTable ExecuteDaxQueryAsync(string query)
        {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            var da = new AdomdDataAdapter(cmd);
            var dt = new DataTable("DAXResult");
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            da.Fill(dt);

            return dt;
        }


        public int ExecuteCommand(string command) {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = command;
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            return cmd.ExecuteNonQuery();
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

        public void Dispose()
        {
            _adomdConn.Dispose();
            _spid = 0;            
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

        public void CacheDaxMetadataInfo() {
            if (_daxMetadataInfo == null) _daxMetadataInfo = new MetadataInfo.DaxMetadata(this);
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
            get { return _dmvCollection ?? (_dmvCollection = new ADOTabularDynamicManagementViewCollection(this)); }
        }

        public string ServerName
        {
            get
            {
                foreach(var prop in ConnectionString.Split(';'))
                {
                    if (prop.Trim().Length ==0) continue;
                    var p = prop.Split('=');
                    if (p[0] == "Data Source") return p[1];
                }
                return "Not Connected";
            }
        }

        private string _svrVersion = null;
        public string ServerVersion
        {
            get {
                if (_svrVersion == null)
                {
                    _svrVersion = _adomdConn.ServerVersion;
                    
                }
                return _svrVersion;
            }
            set
            {
                _svrVersion = value;
            }
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
            
            using (XmlReader rdr = new XmlTextReader(new StringReader(metadata)))
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

            using (XmlReader rdr = new XmlTextReader(new StringReader(metadata))) {
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
                            _spid = int.Parse(dr["SESSION_SPID"].ToString());
                        }
                    }
                    catch 
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
            if (_runningCommand != null)
            {
                _runningCommand.Cancel();
            }
            
        }

        
        public bool IsMultiDimensional { get {
                return ServerMode == "Multidimensional";
            }
        }

        public bool IsPowerPivot {get; set;}
        public bool IsPowerBIorSSDT { get { return !IsPowerPivot && FileName.Length > 0; } }

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

        public string FileName { get { return _powerBIFileName; } set { _powerBIFileName = value; } }


        void IDisposable.Dispose()
        {
            Close();
        }

        public void SetCube(string cubeName)
        {
            _currentCube = cubeName;
            _adomdConn.Close();
            _adomdConn = new AdomdConnection(string.Format("{0};Cube={1};Initial Catalog={2}", ConnectionString, cubeName , Database.Name), _connectionType);
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
                if (_connectionProps == null) return "";
                if (!_connectionProps.ContainsKey("Application Name")) return "";
                return _connectionProps["Application Name"];
            }
        }

        public void Refresh()
        {
            _columns.Clear();
            _adoTabDatabaseColl = null;
            _db = null;
            _adomdConn.RefreshMetadata();
        }

        public string ConnectionStringWithInitialCatalog {
            get {
                var builder = new OleDbConnectionStringBuilder(ConnectionString);
                builder["Initial Catalog"] = _currentDatabase;
                if (!string.IsNullOrEmpty(_currentCube)) builder["Cube"] = _currentCube;
                return builder.ToString();
                
                //return string.Format("{0};Initial Catalog={1}{2}", this.ConnectionString , _currentDatabase, CurrentCubeInternal);
            }
        }

        internal object CurrentCubeInternal { get { return  (_currentCube == string.Empty)? string.Empty:string.Format(";Cube={0}", _currentCube); } }

        private Dictionary<string, ADOTabularColumn> _columns = new Dictionary<string, ADOTabularColumn>();
        public Dictionary<string,ADOTabularColumn> Columns { get { return _columns; } }

        public string ServerType { get; set; }

        private string _serverLocation = null;
        public string ServerLocation
        {
            get
            {
                return _serverLocation;
            }
        }

        private string _serverEdition = null;
        public string ServerEdition
        {
            get
            {
                return _serverEdition;
            }
        }

        public int LocaleIdentifier { get {
                if (_adomdConn == null) return 0;
                if (_adomdConn.ConnectionString == null) return 0;
                if (_adomdConn.ConnectionString.Trim().Length == 0) return 0;
                var m = _LocaleIdRegex.Match(_adomdConn.ConnectionString);
                if (!m.Success) return 0;
                return int.Parse(m.Groups[1].Value);
            }
        }

        public bool IsPowerBIXmla { get => this.Properties["Data Source"].StartsWith("powerbi://", StringComparison.OrdinalIgnoreCase); }

        private void UpdateServerProperties()
        {
            var res = new AdomdRestrictionCollection();
            res.Add(new AdomdRestriction("ObjectExpansion", "ReferenceOnly"));
            var props = _adomdConn.GetSchemaDataSet("DISCOVER_XML_METADATA", res, true);
            var xmla = props.Tables[0].Rows[0][0].ToString();
            var xdoc = new XmlDocument();
            var oMgr = new XmlNamespaceManager(xdoc.NameTable);
            oMgr.AddNamespace("d", "http://schemas.microsoft.com/analysisservices/2003/engine");
            oMgr.AddNamespace("ddl400", "http://schemas.microsoft.com/analysisservices/2012/engine/400");
            xdoc.LoadXml(xmla);
            try
            {
                _serverLocation = xdoc.SelectSingleNode("//ddl400:ServerLocation", oMgr).InnerText ?? "";
            } catch
            {
                _serverLocation = "";
            }
            try
            {
                _serverEdition = xdoc.SelectSingleNode("//d:Edition", oMgr).InnerText ?? "";
            } catch
            {
                _serverEdition = "";
            }
        }


        public ADOTabularConnection Clone()
        {
            var cnn = new ADOTabularConnection(this.ConnectionStringWithInitialCatalog, this.Type);
            // copy keywords, functiongroups, DMV's
            cnn._functionGroups = _functionGroups;
            cnn._keywords = _keywords;
            cnn._serverMode = _serverMode;
            cnn._dmvCollection = _dmvCollection;
            cnn.ServerType = ServerType;
            return cnn;
        }
    }

}
