using System;
using System.Linq;
using Microsoft.AnalysisServices.AdomdClient;
using System.Data;

namespace ADOTabular
{
    public class ADOTabularConnection
    {
        private readonly AdomdConnection _adomdConn; 
        public ADOTabularConnection(string connectionString)
        {
            _adomdConn = new AdomdConnection(connectionString);
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

        public bool ShowHiddenObjects
        {
            get { return _adomdConn.ShowHiddenObjects; }
            set { _adomdConn.ShowHiddenObjects = value; }
        }

        public override string ToString()
        {
            return _adomdConn.ConnectionString;
        }

        public string ConnectionString
        {
            get { return _adomdConn.ConnectionString; }
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
            return cmd.ExecuteReader();
        }

        public DataTable ExecuteDaxQuery(string query)
        {
            AdomdCommand cmd = _adomdConn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = query;
            var da = new AdomdDataAdapter(cmd);
            var dt = new DataTable("DAXResult");
            da.Fill(dt);
            return dt;
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
    }
}
