extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class Axis
    {
        private AsAdomdClient.Axis _obj;
        private ExcelAdomdClient.Axis _objExcel;

        public Axis(AsAdomdClient.Axis obj)
        {
            _obj = obj;
        }
        public Axis(ExcelAdomdClient.Axis obj)
        {
            _objExcel = obj;
        }

        public List<Position> Positions
        {
            get
            {
                if (_obj != null)
                {
                    List<Position> list = new List<Position>();
                    foreach (AsAdomdClient.Position level in _obj.Positions)
                    {
                        list.Add(new Position(level));
                    }
                    return list;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<List<Position>> f = delegate
                    {
                        List<Position> list = new List<Position>();
                        foreach (ExcelAdomdClient.Position level in _objExcel.Positions)
                        {
                            list.Add(new Position(level));
                        }
                        return list;
                    };
                    return f();
                }
            }
        }

    }
}
