using System;
using System.Linq;
using System.Threading.Tasks;
//using Microsoft.AnalysisServices.AdomdClient;
using System.Data;
using ADOTabular.AdomdClientWrappers;
using DaxStudio.AdomdClientWrappers;

namespace ADOTabular
{

    [Flags]
    public enum MdschemaVisibility
    {
        Visible = 0x01,
        NonVisible = 0x02
    }

    public class ADOTabularConnection
    {
        private readonly AdomdConnection _adomdConn; 
        public ADOTabularConnection(string connectionString, AdomdType connectionType)
        {
            _adomdConn = new AdomdConnection(connectionString, connectionType);  //TODO - add new connection object
            //_adomdConn.ConnectionString = connectionString;
            //_adomdConn.ShowHiddenObjects = true;
            //_adomdConn.Open();
        }

        public ADOTabularConnection(string connectionString, AdomdType connectionType, bool showHiddenObjects)
        {
            _adomdConn = new AdomdConnection(connectionString,connectionType);
         //   _adomdConn.ConnectionString = connectionString;
            ShowHiddenObjects = showHiddenObjects;
            //_adomdConn.Open();
        }

        // returns the current database for the connection
        public ADOTabularDatabase Database
        {
            get { return new ADOTabularDatabase(this, _adomdConn.Database); }
        }

        public void Open(string connectionString)
        {
            _adomdConn.Open(connectionString);
        }

        public void ChangeDatabase(string database)
        {
            _adomdConn.ChangeDatabase(database);
        }

        private bool _showHiddenObjects;
        public bool ShowHiddenObjects
        {
            get { return _showHiddenObjects; }
            set
            {
                if (_adomdConn.State == ConnectionState.Open) 
                    throw new Exception("Cannot set the ShowHiddenObjects setting while the connection is open");
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

        public string ConnectionString
        {
            get
            {
                if (!_adomdConn.ConnectionString.Contains("Initial Catalog"))
                {
                    return
                        string.Format(
                            _adomdConn.ConnectionString.EndsWith(";")
                                ? "{0}Initial Catalog={1}"
                                : "{0};Initial Catalog={1}", _adomdConn.ConnectionString, Database.Name);
                }
                return _adomdConn.ConnectionString;
            }
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
        

        public DataSet GetSchemaDataSet(string schemaName)
        {
            if (_adomdConn.State == ConnectionState.Closed) _adomdConn.Open();
            return _adomdConn.GetSchemaDataSet(schemaName, null);
        }

        public DataSet GetSchemaDataSet(string schemaName, AdomdRestrictionCollection restrictionCollection)
        {
            if (_adomdConn.State != ConnectionState.Open)
                _adomdConn.Open();
            return _adomdConn.GetSchemaDataSet(schemaName, restrictionCollection);
        }

        public AdomdDataReader ExecuteDaxReader(string query)
        {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            if (_adomdConn.State == ConnectionState.Closed) _adomdConn.Open();
            return cmd.ExecuteReader();
        }
        
        public DataTable ExecuteDaxQueryDataTable(string query)
        {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            var da = new AdomdDataAdapter(cmd);
            var dt = new DataTable("DAXResult");
            if (_adomdConn.State == ConnectionState.Closed) _adomdConn.Open();
            da.Fill(dt);
            return dt;
        }

        public CellSet ExecuteDaxQueryCellset(string query)
        {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            if (_adomdConn.State == ConnectionState.Closed) _adomdConn.Open();
            var cs = cmd.ExecuteCellSet();
            return cs;
        }

        public int ExecuteCommand(string command) {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = command;
            return cmd.ExecuteNonQuery();
        }

        public void Close()
        {
            if (_adomdConn.State != ConnectionState.Closed && _adomdConn.State != ConnectionState.Broken)
            {
                _adomdConn.Close();
            }
        }

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

        public string ServerVersion
        {
            get { return _adomdConn.ServerVersion; }
        }
        public string SessionID
        { 
            get { return _adomdConn.SessionID; }
        }

        private int _spid;
        public int SPID
        {
            get
            {
                if (_spid == 0)
                {
                    //var resColl = new AdomdRestrictionCollection {{"SESSION_ID", SessionID}};
                    //var ds = GetSchemaDataSet("DISCOVER_SESSIONS", resColl);
                    var ds = GetSchemaDataSet("DISCOVER_SESSIONS");
                    foreach (var dr in ds.Tables[0].Rows.Cast<DataRow>().Where(dr => dr["SESSION_ID"].ToString() == SessionID))
                    {
                        _spid = int.Parse(dr["SESSION_SPID"].ToString());
                    }
                }
                return _spid;
            }
        }

        public void Cancel()
        {
            var cancelConn = new AdomdConnection(_adomdConn.ConnectionString, _adomdConn.Type);
            if (_adomdConn.State == ConnectionState.Closed | _adomdConn.State == ConnectionState.Connecting) return;
            cancelConn.SessionID = _adomdConn.SessionID;
            //cancelConn.ConnectionString = _adomdConn.ConnectionString;
            cancelConn.Open();
            var cancelCmd = cancelConn.CreateCommand();
            cancelCmd.CommandType = CommandType.Text;
            cancelCmd.CommandText = "<Command><Cancel></Cancel></Command>";
            cancelCmd.Execute();
        }

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
    }
}
