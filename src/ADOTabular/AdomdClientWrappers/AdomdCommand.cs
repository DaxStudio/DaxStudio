extern alias ExcelAdomdClientReference;

using ADOTabular.Enums;

//using DaxStudio.Common.Enums;
using System;
using System.Data;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdCommand : IDisposable
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdCommand _command;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand _objExcel;

        public AdomdCommand() { }
        public AdomdCommand(Microsoft.AnalysisServices.AdomdClient.AdomdCommand command)
        {
            _command = command;
        }
        public AdomdCommand(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand command)
        {
            _objExcel = command;
        }

        public AdomdConnection Connection
        {
            get
            {
                if (_command != null)
                {
                    return new AdomdConnection(_command.Connection);
                }
                else
                {
                    AdomdConnection f()
                    {
                        return new AdomdConnection(_objExcel.Connection);
                    }
                    return f();
                }
            }

            set
            {
                if (value == null) { 
                    Dispose(); 
                    return; 
                }

                if (value.Type == AdomdType.AnalysisServices)
                {
                    if (_command == null)
                        _command = new Microsoft.AnalysisServices.AdomdClient.AdomdCommand();
                    _command.Connection = (Microsoft.AnalysisServices.AdomdClient.AdomdConnection)value.UnderlyingConnection;
                }
                else
                {
                    void f()
                    {
                        if (_objExcel == null)
                            _objExcel = new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand();
                        _objExcel.Connection = (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdConnection)value.UnderlyingConnection;
                    }
                    f();
                }
            }
        }

        public string CommandText
        {
            get
            {
                if (_command != null)
                {
                    return _command.CommandText;
                }
                else
                {
                    string f()
                    {
                        return _objExcel.CommandText;
                    }
                    return f();
                }
            }

            set
            {
                if (_command != null)
                {
                    _command.CommandText = value;
                }
                else
                {
                    void f()
                    {
                        _objExcel.CommandText = value;
                    }
                    f();
                }
            }
        }

        public CommandType CommandType
        {
            get
            {
                if (_command != null)
                {
                    return _command.CommandType;
                }
                else
                {
                    CommandType f()
                    {
                        return _objExcel.CommandType;
                    }
                    return f();
                }
            }

            set
            {
                if (_command != null)
                {
                    _command.CommandType = value;
                }
                else
                {
                    void f()
                    {
                        _objExcel.CommandType = value;
                    }
                    f();
                }
            }
        }

        public void Cancel()
        {
            if (_command != null)
            {
                _command.Cancel();
            }
            else
            {
                void f()
                {
                    _objExcel.Cancel();
                }
                f();
            }
        }

        public Microsoft.AnalysisServices.AdomdClient.AdomdParameterCollection Parameters => _command.Parameters;


        public AdomdDataReader ExecuteReader()
        {
            if (_command != null)
            {

                return new AdomdDataReader(_command.ExecuteReader( ));
            }
            else
            {
                AdomdDataReader f()
                {
                    _objExcel.Parameters.Clear();
                    foreach (AdomdParameter param in Parameters)
                    {
                        _objExcel.Parameters.Add(new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                    }
                    return new AdomdDataReader(_objExcel.ExecuteReader());
                }
                return f();
            }
        }


        public int ExecuteNonQuery()
        {
            if (_command != null)
            {
                _command.Parameters.Clear();
                foreach (AdomdParameter param in Parameters)
                {
                    _command.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                }
                return _command.ExecuteNonQuery();
            }
            else
            {
                int f()
                {
                    _objExcel.Parameters.Clear();
                    foreach (AdomdParameter param in Parameters)
                    {
                        _objExcel.Parameters.Add(new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                    }
                    return _objExcel.ExecuteNonQuery();
                }
                return f();
            }
        }

        public void Execute()
        {
            if (_command != null)
            {
                _command.Parameters.Clear();
                foreach (AdomdParameter param in Parameters)
                {
                    _command.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                }
                _command.Execute();
            }
            else
            {
                void f()
                {
                    _objExcel.Parameters.Clear();
                    foreach (AdomdParameter param in Parameters)
                    {
                        _objExcel.Parameters.Add(new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                    }
                    _objExcel.Execute();
                }
                f();
            }
        }

        internal object UnderlyingCommand
        {
            get
            {
                if (_command != null)
                {
                    return _command;
                }
                else
                {
                    object f() => _objExcel;
                    return f();
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // dispose managed state (managed objects).
                    if (_command != null)
                    {
                        _command.Dispose();
                        _command = null;
                    }
                    else
                    {
                        void f()
                        {
                            _objExcel.Dispose();
                            _objExcel = null;
                        }
                        f();
                    }
                }

                
                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

}
