using ADOTabular.Enums;
using System;
using System.Data;
using System.Diagnostics.Contracts;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdDataAdapter : IDisposable
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter _obj;

        public AdomdDataAdapter() { }
        public AdomdDataAdapter(Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter adapter)
        {
            _obj = adapter;
        }

        public AdomdDataAdapter(AdomdCommand command)
        {
            Contract.Requires(command != null, "The command parameter must not be null");

            
                _obj = new Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter
                {
                    SelectCommand = (Microsoft.AnalysisServices.AdomdClient.AdomdCommand)command.UnderlyingCommand
                };
            
            
        }
        

       public void Fill(DataTable tbl)
       {
           
               _obj.Fill(tbl);
           
       }


        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                        _obj.Dispose();
                    
                }

                disposedValue = true;
            }
        }


        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
