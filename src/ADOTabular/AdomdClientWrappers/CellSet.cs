extern alias ExcelAdomdClientReference;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class CellSet
    {
        private Microsoft.AnalysisServices.AdomdClient.CellSet _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CellSet _objExcel;

        public CellSet(Microsoft.AnalysisServices.AdomdClient.CellSet obj)
        {
            _obj = obj;
        }
        public CellSet(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CellSet obj)
        {
            _objExcel = obj;
        }

        public List<Axis> Axes
        {
            get
            {
                if (_obj != null)
                {
                    List<Axis> list = new List<Axis>();
                    foreach (Microsoft.AnalysisServices.AdomdClient.Axis level in _obj.Axes)
                    {
                        list.Add(new Axis(level));
                    }
                    return list;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<List<Axis>> f = delegate
                    {
                        List<Axis> list = new List<Axis>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Axis level in _objExcel.Axes)
                        {
                            list.Add(new Axis(level));
                        }
                        return list;
                    };
                    return f();
                }
            }
        }

    }
}
