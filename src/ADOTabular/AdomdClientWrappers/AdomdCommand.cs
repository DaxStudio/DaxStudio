extern alias ExcelAdomdClientReference;
using System;
using System.Data;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdCommand
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdCommand _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand _objExcel;

        public AdomdCommand() { }
        public AdomdCommand(Microsoft.AnalysisServices.AdomdClient.AdomdCommand obj)
        {
            _obj = obj;
        }
        public AdomdCommand(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand obj)
        {
            _objExcel = obj;
        }

        public AdomdConnection Connection
        {
            get
            {
                if (_obj != null)
                {
                    return new AdomdConnection(_obj.Connection);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<AdomdConnection> f = delegate
                    {
                        return new AdomdConnection(_objExcel.Connection);
                    };
                    return f();
                }
            }

            set
            {
                if (value.Type == AdomdType.AnalysisServices)
                {
                    if (_obj == null)
                        _obj = new Microsoft.AnalysisServices.AdomdClient.AdomdCommand();
                    _obj.Connection = (Microsoft.AnalysisServices.AdomdClient.AdomdConnection)value.UnderlyingConnection;
                }
                else
                {
                    ExcelAdoMdConnections.VoidDelegate f = delegate
                    {
                        if (_objExcel == null)
                            _objExcel = new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand();
                        _objExcel.Connection = (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdConnection)value.UnderlyingConnection;
                    };
                    f();
                }
            }
        }

        public string CommandText
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.CommandText;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.CommandText;
                    };
                    return f();
                }
            }

            set
            {
                if (_obj != null)
                {
                    _obj.CommandText = value;
                }
                else
                {
                    ExcelAdoMdConnections.VoidDelegate f = delegate
                    {
                        _objExcel.CommandText = value;
                    };
                    f();
                }
            }
        }

        public CommandType CommandType
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.CommandType;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<CommandType> f = delegate
                    {
                        return _objExcel.CommandType;
                    };
                    return f();
                }
            }

            set
            {
                if (_obj != null)
                {
                    _obj.CommandType = value;
                }
                else
                {
                    ExcelAdoMdConnections.VoidDelegate f = delegate
                    {
                        _objExcel.CommandType = value;
                    };
                    f();
                }
            }
        }

        public void Cancel()
        {
            if (_obj != null)
            {
                _obj.Cancel();
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _objExcel.Cancel();
                };
                f();
            }
        }

        AdomdParameterCollection _parameters = new AdomdParameterCollection();
        public AdomdParameterCollection Parameters
        {
            get
            {
                return _parameters;
            }
        }

        public CellSet ExecuteCellSet()
        {
            if (_obj != null)
            {
                _obj.Parameters.Clear();
                foreach (AdomdParameter param in _parameters)
                {
                    _obj.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                }
                return new CellSet(_obj.ExecuteCellSet());
            }
            else
            {
                ExcelAdoMdConnections.ReturnDelegate<CellSet> f = delegate
                {
                    _objExcel.Parameters.Clear();
                    foreach (AdomdParameter param in _parameters)
                    {
                        _objExcel.Parameters.Add(new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                    }
                    return new CellSet(_objExcel.ExecuteCellSet());
                };
                return f();
            }
        }


        public AdomdDataReader ExecuteReader()
        {
            if (_obj != null)
            {
                _obj.Parameters.Clear();
                foreach (AdomdParameter param in _parameters)
                {
                    _obj.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                }
                return new AdomdDataReader(_obj.ExecuteReader());
            }
            else
            {
                ExcelAdoMdConnections.ReturnDelegate<AdomdDataReader> f = delegate
                {
                    _objExcel.Parameters.Clear();
                    foreach (AdomdParameter param in _parameters)
                    {
                        _objExcel.Parameters.Add(new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                    }
                    return new AdomdDataReader(_objExcel.ExecuteReader());
                };
                return f();
            }
        }


        public int ExecuteNonQuery()
        {
            if (_obj != null)
            {
                _obj.Parameters.Clear();
                foreach (AdomdParameter param in _parameters)
                {
                    _obj.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                }
                return _obj.ExecuteNonQuery();
            }
            else
            {
                ExcelAdoMdConnections.ReturnDelegate<int> f = delegate
                {
                    _objExcel.Parameters.Clear();
                    foreach (AdomdParameter param in _parameters)
                    {
                        _objExcel.Parameters.Add(new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                    }
                    return _objExcel.ExecuteNonQuery();
                };
                return f();
            }
        }

        public void Execute()
        {
            if (_obj != null)
            {
                _obj.Parameters.Clear();
                foreach (AdomdParameter param in _parameters)
                {
                    _obj.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                }
                _obj.Execute();
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _objExcel.Parameters.Clear();
                    foreach (AdomdParameter param in _parameters)
                    {
                        _objExcel.Parameters.Add(new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                    }
                    _objExcel.Execute();
                };
                f();
            }
        }

        internal object UnderlyingCommand
        {
            get
            {
                if (_obj != null)
                {
                    return _obj;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<object> f = delegate
                    {
                        return _objExcel;
                    };
                    return f();
                }
            }
        }
    }

}
