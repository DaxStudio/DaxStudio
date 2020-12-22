using ADOTabular.Enums;

//using DaxStudio.Common.Enums;
using System;
using System.Data;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdCommand : IDisposable
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdCommand _command;
        //private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand _objExcel;

        public AdomdCommand() { }
        public AdomdCommand(Microsoft.AnalysisServices.AdomdClient.AdomdCommand command)
        {
            _command = command;
        }


        public ADOTabular.AdomdClientWrappers.AdomdConnection Connection
        {
            get => new AdomdConnection(_command.Connection);

            set
            {
                if (value == null) { 
                    Dispose(); 
                    return; 
                }


                _command = new Microsoft.AnalysisServices.AdomdClient.AdomdCommand();
                _command.Connection = (Microsoft.AnalysisServices.AdomdClient.AdomdConnection)value.UnderlyingConnection;
                
            }
        }

        public string CommandText
        {
            get => _command.CommandText;

            set => _command.CommandText = value;
        }

        public CommandType CommandType
        {
            get => _command.CommandType;

            set => _command.CommandType = value;
        }

        public void Cancel() => _command.Cancel();

        public AdomdParameterCollection Parameters { get; } = new AdomdParameterCollection();

        

        public AdomdDataReader ExecuteReader()
        {
            
            _command.Parameters.Clear();
            foreach (AdomdParameter param in Parameters)
            {
                _command.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
            }
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
            if (_command != null)
            {
                _command.Parameters.Clear();
                foreach (AdomdParameter param in Parameters)
                {
                    _command.Parameters.Add(new Microsoft.AnalysisServices.AdomdClient.AdomdParameter(param.Name, param.Value));
                }
                _command.Execute();
            }
            
        }

        internal object UnderlyingCommand => _command;

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
