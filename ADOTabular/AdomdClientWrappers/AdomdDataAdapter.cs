extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using DaxStudio.AdomdClientWrappers;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdDataAdapter
    {
        private AsAdomdClient.AdomdDataAdapter _obj;
        private ExcelAdomdClient.AdomdDataAdapter _objExcel;

        public AdomdDataAdapter() { }
        public AdomdDataAdapter(AsAdomdClient.AdomdDataAdapter obj)
        {
            _obj = obj;
        }
        public AdomdDataAdapter(ExcelAdomdClient.AdomdDataAdapter obj)
        {
            _objExcel = obj;
        }
        public AdomdDataAdapter(AdomdCommand obj)
        {
            if (obj.Connection.Type == AdomdType.AnalysisServices)
            {
                _obj = new AsAdomdClient.AdomdDataAdapter();
                _obj.SelectCommand = (AsAdomdClient.AdomdCommand) obj.UnderlyingCommand;
            }
            else
            {
                _objExcel = new ExcelAdomdClient.AdomdDataAdapter();
                _objExcel.SelectCommand = (ExcelAdomdClient.AdomdCommand) obj.UnderlyingCommand;
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
