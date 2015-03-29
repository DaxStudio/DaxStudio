extern alias ExcelAdomdClientReference;
using System;

namespace ADOTabular.AdomdClientWrappers
{
    public class AdomdDataReader
    {
        private Microsoft.AnalysisServices.AdomdClient.AdomdDataReader _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataReader _objExcel;

        public AdomdDataReader() { }
        public AdomdDataReader(Microsoft.AnalysisServices.AdomdClient.AdomdDataReader obj)
        {
            _obj = obj;
        }
        public AdomdDataReader(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdDataReader obj)
        {
            _objExcel = obj;
        }

    }
}
