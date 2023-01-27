using ADOTabular.Enums;
using System;
using System.Data;
using System.Threading;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdCommand : IDisposable
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdCommand _command;

        public AdomdCommand() { }
        public AdomdCommand(Microsoft.AnalysisServices.AdomdClient.AdomdCommand command)
        {
            _command = command;
        }

        public AdomdConnection Connection
        {
            get
            {
                return new AdomdConnection(_command.Connection);
            }

            set
            {
                if (value == null) { 
                    Dispose(); 
                    return; 
                }

                if (_command == null)
                    _command = new Microsoft.AnalysisServices.AdomdClient.AdomdCommand();
                _command.Connection = (Microsoft.AnalysisServices.AdomdClient.AdomdConnection)value.UnderlyingConnection;
            }
        }

        public string CommandText
        {
            get
            {
                return _command.CommandText;
            }

            set
            {
                _command.CommandText = value;
            }
        }

        public CommandType CommandType
        {
            get
            {
                return _command.CommandType;
            }

            set
            {
                _command.CommandType = value;
            }
        }

        public void Cancel()
        {
            _command.Cancel();
        }

        public Microsoft.AnalysisServices.AdomdClient.AdomdParameterCollection Parameters => _command.Parameters;


        public AdomdDataReader ExecuteReader()
        {

            return new AdomdDataReader(_command.ExecuteReader( ));
        }


        public int ExecuteNonQuery()
        {

            _command.Parameters.Clear();
            foreach (AdomdParameter param in Parameters)
            {
                _command.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
            }
            return _command.ExecuteNonQuery();
        }

        public void Execute()
        {
            _command.Parameters.Clear();
            foreach (AdomdParameter param in Parameters)
            {
                _command.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
            }
            _command.Execute();
        }

        internal object UnderlyingCommand
        {
            get
            {
                return _command;
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
                    _command.Dispose();
                    _command = null;
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
