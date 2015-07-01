using System;
using System.IO;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using System.Xml;
using ADOTabular.AdomdClientWrappers;
using System.Collections.Generic;

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
    
    public class ADOTabularConnection:IDisposable
    {
        private AdomdCommand _runningCommand;

        public event EventHandler ConnectionChanged;
        private AdomdConnection _adomdConn; 
        private readonly AdomdType _connectionType;
        public ADOTabularConnection(string connectionString, AdomdType connectionType) 
            : this(connectionString,connectionType, ADOTabularMetadataDiscovery.Csdl)
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
            _adomdConn = new AdomdConnection(ConnectionString,connectionType);
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
                if (_adomdConn.State != ConnectionState.Open)
                {
                    _adomdConn.Open();
                }
                var dd = Databases.GetDatabaseDictionary(this.SPID);
                if (!dd.ContainsKey(_adomdConn.Database))
                {
                    dd = Databases.GetDatabaseDictionary(this.SPID, true);
                }
                var db = dd[_adomdConn.Database];
                if (_db == null || db.Name != _db.Name )
                {
                    _db = new ADOTabularDatabase(this, _adomdConn.Database, db.Id, db.LastUpdate);
                }
                return _db;
            }
        }

        public void Open()
        {
            _adomdConn.Open();
        }

        public void Open(string connectionString)
        {
            _adomdConn.Open(connectionString);
            if (ConnectionChanged!=null)
                ConnectionChanged(this,new EventArgs());
        }

        public void ChangeDatabase(string database)
        {
            _adomdConn.ChangeDatabase(database);
            if (ConnectionChanged != null)
                ConnectionChanged(this, new EventArgs());
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

        private string _connectionString="";
        private Dictionary<string, string> _connectionProps;
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
                if (!connstr.Contains("Show Hidden Cubes") && ShowHiddenObjects)
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
            }
        }

        private Dictionary<string, string> SplitConnectionString(string connectionString)
        {
            var props = new Dictionary<string, string>();
            foreach (var prop in connectionString.Split(';'))
            {
                if (prop.Trim().Length == 0) continue;
                var p = prop.Split('=');

                props.Add(p[0], p[1]);
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
            return Task.Factory.StartNew(() =>
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
            
            return Task.Factory.StartNew(() =>
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
            get { return _adomdConn.State; }
        }

        public void EndExecuteDaxReader(IAsyncResult result)
        {
            _execReader.EndInvoke(result);
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
            
            FixColumnNaming(dt);
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

            FixColumnNaming(dt);
            return dt;
        }


        public int ExecuteCommand(string command) {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = command;
            if (_adomdConn.State != ConnectionState.Open) _adomdConn.Open();
            return cmd.ExecuteNonQuery();
        }

        public void Close()
        {
            if (_adomdConn.State != ConnectionState.Closed && _adomdConn.State != ConnectionState.Broken)
            {
                _adomdConn.Close();
                _spid = 0;
            }
        }

        private ADOTabularFunctionGroupCollection _functionGroups;
        public ADOTabularFunctionGroupCollection FunctionGroups
        {
            get { return _functionGroups ?? (_functionGroups = new ADOTabularFunctionGroupCollection(this)); }
        }

        private ADOTabularKeywordCollection _keywords;
        public ADOTabularKeywordCollection Keywords
        {
            get {
                if (_keywords == null) { _keywords = new ADOTabularKeywordCollection(this); }
                return _keywords; }
        }

        /*
        private ADOTabularFunctionCollection _adoTabFuncColl;
        public ADOTabularFunctionCollection Functions
        {
            get
            {
                if (_adoTabFuncColl == null)
                {
                    if (_adomdConn != null)
                    {
                        _adoTabFuncColl = new ADOTabularFunctionCollection(this);
                    }
                    else
                    {
                        throw new NullReferenceException("Unable to populate Function collection - a valid connection has not been established");
                    }
                }
                return _adoTabFuncColl;
            }
        }
        */
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


        public bool IsPowerPivot {get; set;}

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

        private static void FixColumnNaming(DataTable dataTable)
        {
            const string MEASURES_MDX = "[Measures].";

            // If at least one column has the Mdx syntax, identify the result as an MDX query (hoping the assumption is always true...)
            bool isMdxResult = (from DataColumn col in dataTable.Columns
                                where col.ColumnName.IndexOf("].[") > 0
                                select col).Count() > 0;
            foreach (DataColumn col in dataTable.Columns)
            {
                bool removeCaption = false;
                string name = col.ColumnName;
                bool removeSquareBrackets = !isMdxResult;
                if (isMdxResult) {
                    if (name.IndexOf(MEASURES_MDX) >= 0) {
                        name = name.Replace(MEASURES_MDX, "");
                        removeSquareBrackets = true;
                    }
                    else {
                        removeCaption = true;
                    }
                }

                if (removeSquareBrackets) {
                    // Format column naming for DAX result or if it is a measure name
                    int firstBracket = name.IndexOf('[') + 1;
                    name = firstBracket == 0 ? name : name.Substring(firstBracket, name.Length - firstBracket - 1);
                }
                col.Caption = (removeCaption) ? "" : name;
                col.ColumnName = name.Replace(' ', '_');
            }

        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public void SetCube(string cubeName)
        {
            _adomdConn.Close();
            _adomdConn = new AdomdConnection(string.Format("{0};Cube={1}", ConnectionString, cubeName ), _connectionType);
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
            _adoTabDatabaseColl = null;
            _db = null;
            _adomdConn.RefreshMetadata();
        }
    }

}
