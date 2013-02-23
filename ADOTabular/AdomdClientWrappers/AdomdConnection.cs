extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class AdomdConnection
    {
        private AdomdType _type;
        private AsAdomdClient.AdomdConnection _conn;
        private ExcelAdomdClient.AdomdConnection _connExcel;

        public AdomdConnection(AsAdomdClient.AdomdConnection obj)
        {
            _type = AdomdType.AnalysisServices;
            _conn = obj;
        }
        public AdomdConnection(ExcelAdomdClient.AdomdConnection obj)
        {
            _type = AdomdType.Excel;
            _connExcel = obj;
        }
        
        public AdomdConnection(string connectionString, AdomdType type)
        {
            _type = type;
            if (_type == AdomdType.AnalysisServices)
            {
                _conn = new AsAdomdClient.AdomdConnection(connectionString);
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _connExcel = new ExcelAdomdClient.AdomdConnection(connectionString);
                };
                f();
            }
        }

        internal AdomdType Type
        {
            get { return _type; }
        }

        internal object UnderlyingConnection
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return _conn;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<object> f = delegate
                    {
                        return _connExcel;
                    };
                    return f();
                }
            }
        }

        public void Open()
        {
            if (_type == AdomdType.AnalysisServices)
            {
                _conn.Open();
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _connExcel.Open();
                };
                f();
            }
        }

        public void Open(string connectionString)
        {
            if (_type == AdomdType.AnalysisServices)
            {
                _conn.Open(connectionString);
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _connExcel.Open(connectionString);
                };
                f();
            }
        }
        public void Close()
        {
            if (_type == AdomdType.AnalysisServices)
            {
                _conn.Close();
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _connExcel.Close();
                };
                f();
            }
        }

        public void ChangeDatabase(string database)
        {
            if (_type == AdomdType.AnalysisServices)
            {
                _conn.ChangeDatabase(database);
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _connExcel.ChangeDatabase(database);
                };
                f();
            }
        }

        public string ConnectionString
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return _conn.ConnectionString;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _connExcel.ConnectionString;
                    };
                    return f();
                }
            }
        }

        public string ClientVersion
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return _conn.ClientVersion;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _connExcel.ClientVersion;
                    };
                    return f();
                }
            }
        }

        public AdomdCommand CreateCommand()
        {
            //if (_type == AdomdType.AnalysisServices)
            //{
                var cmd = new AdomdCommand();
                cmd.Connection = this;
                return cmd;
            /*}
            else
            {
                ExcelAdoMdConnections.ReturnDelegate<ExcelAdomdClientReference.Microsoft.AnalysisServices.AdomdClient.AdomdCommand> f = delegate
                {
                    return _connExcel.CreateCommand();
                };
                f();
            }
             */ 
        }

        public CubeCollection Cubes
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return new CubeCollection(_conn.Cubes);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<CubeCollection> f = delegate
                    {
                        return new CubeCollection(_connExcel.Cubes);
                    };
                    return f();
                }
            }
        }

        public ConnectionState State
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return _conn.State;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<ConnectionState> f = delegate
                    {
                        return _connExcel.State;
                    };
                    return f();
                }
            }
        }

        public string SessionID
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return _conn.SessionID;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _connExcel.SessionID;
                    };
                    return f();
                }
            }
            set
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    _conn.SessionID = value;
                }
                else
                {
                    ExcelAdoMdConnections.VoidDelegate f = delegate
                    {
                        _connExcel.SessionID = value;
                    };
                    f();
                }
            }
        }

        public string Database
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return _conn.Database;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _connExcel.Database;
                    };
                    return f();
                }
            }
        }

        public string ServerVersion
        {
            get
            {
                if (_type == AdomdType.AnalysisServices)
                {
                    return _conn.ServerVersion;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _connExcel.ServerVersion;
                    };
                    return f();
                }
            }
        }

        public DataSet GetSchemaDataSet(string schemaName, AdomdRestrictionCollection restrictions)
        {
            if (_type == AdomdType.AnalysisServices)
            {
                AsAdomdClient.AdomdRestrictionCollection coll = new AsAdomdClient.AdomdRestrictionCollection();
                if (restrictions != null)
                {
                
                    foreach (AdomdRestriction res in restrictions)
                    {
                        coll.Add(new AsAdomdClient.AdomdRestriction(res.Name, res.Value));
                    }
                }
                return _conn.GetSchemaDataSet(schemaName, coll);
            }
            else
            {
                ExcelAdoMdConnections.ReturnDelegate<DataSet> f = delegate
                {
                    ExcelAdomdClient.AdomdRestrictionCollection coll = new ExcelAdomdClient.AdomdRestrictionCollection();
                    if (restrictions != null)
                    {
                        foreach (AdomdRestriction res in restrictions)
                        {
                            coll.Add(new ExcelAdomdClient.AdomdRestriction(res.Name, res.Value));
                        }
                    }
                    return _connExcel.GetSchemaDataSet(schemaName, coll);
                };
                return f();
            }
        }
    }

    public enum AdomdType {
        AnalysisServices = 1,
        Excel = 2
    }
}
