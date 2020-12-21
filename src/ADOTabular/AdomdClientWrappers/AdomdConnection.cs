using ADOTabular.Enums;
using System;
using System.Data;
using System.Threading;

namespace ADOTabular.AdomdClientWrappers
{
    public sealed class AdomdConnection:IDisposable
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdConnection _conn;
        
        private readonly object rowsetLock = new object();
        public AdomdConnection(Microsoft.AnalysisServices.AdomdClient.AdomdConnection connection)
        {
            Type = AdomdType.AnalysisServices;
            _conn = connection;
        }
        
        public AdomdConnection(string connectionString, AdomdType type)
        {
            Type = type;
            _conn = new Microsoft.AnalysisServices.AdomdClient.AdomdConnection(connectionString);
 
        }

        internal AdomdType Type { get; }

        internal object UnderlyingConnection => _conn;


        public void Open() => _conn.Open();

        public void Open(string connectionString) => _conn.Open(connectionString);

        public void Close() => _conn.Close();

        public void Close(bool endSession) => _conn.Close(endSession);

        public void ChangeDatabase(string database)
        {
            if (database == null) return; 
            if (database.Trim().Length == 0) return;
            
                _conn.ChangeDatabase(database);
            
        }

        public string ConnectionString => _conn.ConnectionString;

        public string ClientVersion => _conn.ClientVersion;

        public AdomdCommand CreateCommand()
        {

            var cmd = new AdomdCommand
            {
                Connection = this
            };
            return cmd;

        }

        //public CubeCollection Cubes
        //{
        //    get
        //    {
                
        //            return new CubeCollection(_conn.Cubes);
                
        //    }
        //}

        public ConnectionState State
        {
            get
            {
                
                    if (_conn != null) return _conn.State;
                    return ConnectionState.Closed;
                
            }
        }

        public string SessionID
        {
            get => _conn.SessionID;
            set => _conn.SessionID = value;
        }

        public string Database => _conn.Database;

        public string ServerVersion
        {
            get
            {
                if (_conn.State != ConnectionState.Open)
                    _conn.Open();
                return _conn.ServerVersion;
            }
        }

        public DataSet GetSchemaDataSet(string schemaName, AdomdRestrictionCollection restrictions, bool throwOnInlineErrors)
        {
            
                global::Microsoft.AnalysisServices.AdomdClient.AdomdRestrictionCollection coll = new global::Microsoft.AnalysisServices.AdomdClient.AdomdRestrictionCollection();
                if (restrictions != null)
                {
                
                    foreach (AdomdClientWrappers.AdomdRestriction res in restrictions)
                    {
                        coll.Add(new global::Microsoft.AnalysisServices.AdomdClient.AdomdRestriction( res.Name, res.Value));
                    }
                }
                if (_conn.State != ConnectionState.Open)
                {
                    _conn.Open();
                }

                // wait 10 seconds before timing out
                if (Monitor.TryEnter(rowsetLock, new TimeSpan(0,0,10 )))
                {
                    try
                    {
                        return _conn.GetSchemaDataSet(schemaName, coll, throwOnInlineErrors);
                    }
                    finally
                    {
                        Monitor.Exit(rowsetLock);
                    }
                } else
                {
                    throw new InvalidOperationException("Timeout exceeded attempting to establish internal lock for GetSchemaDataSet");
                }
            
        }

        public void RefreshMetadata()
        {
            _conn?.RefreshMetadata();
        }

        public void Dispose()
        {
            if (_conn != null)
            {
                _conn.Dispose();
                _conn = null;
            }
        }
    }


}
