extern alias ExcelAdomdClientReference;
using System.Data;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdDataAdapter
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter _objExcel;

        public AdomdDataAdapter() { }
        public AdomdDataAdapter(Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter obj)
        {
            _obj = obj;
        }
        public AdomdDataAdapter(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter obj)
        {
            _objExcel = obj;
        }
        public AdomdDataAdapter(AdomdCommand obj)
        {
            if (obj.Connection.Type == AdomdType.AnalysisServices)
            {
                _obj = new Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter();
                _obj.SelectCommand = (Microsoft.AnalysisServices.AdomdClient.AdomdCommand) obj.UnderlyingCommand;
            }
            else
            {
                ExcelAdoMdConnections.VoidDelegate f = delegate
                {
                    _objExcel = new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataAdapter();
                    _objExcel.SelectCommand =
                        (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdCommand) obj.UnderlyingCommand;
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
