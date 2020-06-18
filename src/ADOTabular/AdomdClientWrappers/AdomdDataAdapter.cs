extern alias ExcelAdomdClientReference;

using DaxStudio.Common.Enums;
using System;
using System.Data;
using System.Diagnostics.Contracts;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdDataAdapter : IDisposable
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter _objExcel;

        public AdomdDataAdapter() { }
        public AdomdDataAdapter(Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter adapter)
        {
            _obj = adapter;
        }
        public AdomdDataAdapter(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter adapter)
        {
            _objExcel = adapter;
        }
        public AdomdDataAdapter(AdomdCommand command)
        {
            Contract.Requires(command != null, "The command parameter must not be null");

            if (command.Connection.Type == AdomdType.AnalysisServices)
            {
                _obj = new Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter
                {
                    SelectCommand = (Microsoft.AnalysisServices.AdomdClient.AdomdCommand)command.UnderlyingCommand
                };
            }
            else
            {
                void f()
                {
                    _objExcel = new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter
                    {
                        SelectCommand =
                        (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand)command.UnderlyingCommand
                    };
                }
                f();
            }
            
        }
        

       public void Fill(DataTable tbl)
       {
           if (_obj != null)
           {
               _obj.Fill(tbl);
           }
           else
           {
                void f() => _objExcel.Fill(tbl);
                f();
           }
       }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_obj != null)
                    {
                        _obj.Dispose();
                    }
                    else
                    {
                        void f() => _objExcel.Dispose();
                        f();
                    }
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
