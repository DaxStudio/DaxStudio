extern alias ExcelAdomdClientReference;
using System.Data;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdDataAdapter
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter _obj;
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
            if (command.Connection.Type == AdomdType.AnalysisServices)
            {
                _obj = new Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter();
                _obj.SelectCommand = (Microsoft.AnalysisServices.AdomdClient.AdomdCommand) command.UnderlyingCommand;
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _objExcel = new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter();
                    _objExcel.SelectCommand =
                        (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand) command.UnderlyingCommand;
                };
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
               ExcelAdoMdConnections.VoidDelegate f = delegate
               {
                   _objExcel.Fill(tbl);
               };
               f();
           }
       }
    }
}
