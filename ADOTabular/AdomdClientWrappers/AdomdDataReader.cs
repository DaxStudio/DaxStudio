extern alias ExcelAdomdClientReference;

using System;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class AdomdDataReader
    {
        private AsAdomdClient.AdomdDataReader _obj;
        private ExcelAdomdClient.AdomdDataReader _objExcel;

        public AdomdDataReader() { }
        public AdomdDataReader(AsAdomdClient.AdomdDataReader obj)
        {
            _obj = obj;
        }
        public AdomdDataReader(ExcelAdomdClient.AdomdDataReader obj)
        {
            _objExcel = obj;
        }
    }
}
