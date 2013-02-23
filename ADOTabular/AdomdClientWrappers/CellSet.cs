extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class CellSet
    {
        private AsAdomdClient.CellSet _obj;
        private ExcelAdomdClient.CellSet _objExcel;

        public CellSet(AsAdomdClient.CellSet obj)
        {
            _obj = obj;
        }
        public CellSet(ExcelAdomdClient.CellSet obj)
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
                    foreach (AsAdomdClient.Axis level in _obj.Axes)
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
                        foreach (ExcelAdomdClient.Axis level in _objExcel.Axes)
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
