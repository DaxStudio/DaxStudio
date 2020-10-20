extern alias ExcelAdomdClientReference;
using System;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class CubeDef
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.CubeDef _obj;
        private readonly ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeDef _objExcel;

        public CubeDef(Microsoft.AnalysisServices.AdomdClient.CubeDef cubeDef)
        {
            _obj = cubeDef;
        }
        public CubeDef(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeDef cubeDef)
        {
            _objExcel = cubeDef;
        }

        public AdomdConnection ParentConnection
        {
            get
            {
                if (_obj != null)
                {
                    return new AdomdConnection(_obj.ParentConnection);
                }
                else
                {
                    AdomdConnection f() => new AdomdConnection(_objExcel.ParentConnection);
                    return f();
                }
            }
        }
        public string Name
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.Name;
                }
                else
                {
                    string f() => _objExcel.Name;
                    return f();
                }
            }
        }

        public DimensionCollection Dimensions
        {
            get
            {
                if (_obj != null)
                {
                    DimensionCollection list = new DimensionCollection();
                    foreach (Microsoft.AnalysisServices.AdomdClient.Dimension dim in _obj.Dimensions)
                    {
                        list.Add(new Dimension(dim));
                    }
                    return list;
                }
                else
                {
                    DimensionCollection f()
                    {
                        DimensionCollection list = new DimensionCollection();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Dimension dim in _objExcel.Dimensions)
                        {
                            list.Add(new Dimension(dim));
                        }
                        return list;
                    }
                    return f();
                }
            }
        }

        public List<Measure> Measures
        {
            get
            {
                if (_obj != null)
                {
                    List<Measure> list = new List<Measure>();
                    foreach (Microsoft.AnalysisServices.AdomdClient.Measure dim in _obj.Measures)
                    {
                        list.Add(new Measure(dim));
                    }
                    return list;
                }
                else
                {
                    List<Measure> f()
                    {
                        List<Measure> list = new List<Measure>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Measure dim in _objExcel.Measures)
                        {
                            list.Add(new Measure(dim));
                        }
                        return list;
                    }
                    return f();
                }
            }
        }

        public List<Kpi> Kpis
        {
            get
            {
                if (_obj != null)
                {
                    List<Kpi> list = new List<Kpi>();
                    foreach (Microsoft.AnalysisServices.AdomdClient.Kpi dim in _obj.Kpis)
                    {
                        list.Add(new Kpi(dim));
                    }
                    return list;
                }
                else
                {
                    List<Kpi> f()
                    {
                        List<Kpi> list = new List<Kpi>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Kpi dim in _objExcel.Kpis)
                        {
                            list.Add(new Kpi(dim));
                        }
                        return list;
                    }
                    return f();
                }
            }
        }

        public NamedSetCollection NamedSets
        {
            get
            {
                if (_obj != null)
                {
                    NamedSetCollection list = new NamedSetCollection();
                    foreach (Microsoft.AnalysisServices.AdomdClient.NamedSet dim in _obj.NamedSets)
                    {
                        list.Add(new NamedSet(dim));
                    }
                    return list;
                }
                else
                {
                    NamedSetCollection f()
                    {
                        NamedSetCollection list = new NamedSetCollection();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.NamedSet dim in _objExcel.NamedSets)
                        {
                            list.Add(new NamedSet(dim));
                        }
                        return list;
                    }
                    return f();
                }
            }
        }
    }

    public class CubeCollection
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.CubeCollection _obj;
        private readonly ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeCollection _objExcel;

        public CubeCollection(Microsoft.AnalysisServices.AdomdClient.CubeCollection cubeCollection)
        {
            _obj = cubeCollection;
        }
        public CubeCollection(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeCollection cubeCollection)
        {
            _objExcel = cubeCollection;
        }
        
        public CubeDef Find(string index)
        {
            if (_obj != null)
            {
                Microsoft.AnalysisServices.AdomdClient.CubeDef obj = _obj.Find(index);
                return obj == null ? null : new CubeDef(obj);
            }
            else
            {
                CubeDef f()
                {
                    ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeDef obj = _objExcel.Find(index);
                    return obj == null ? null : new CubeDef(obj);
                }
                return f();
            }
        }

        public int Count
        {
            get
            {
                if (_obj != null)
                {
                    try
                    {
                        return _obj.Count;
                    }
                    catch (Microsoft.AnalysisServices.AdomdClient.AdomdConnectionException)
                    {
                        throw;// new AdomdConnectionException(); //just to communicate what type of exception
                    }
                }
                else
                {
                    int f()
                    {
                        try
                        {
                            return _objExcel.Count;
                        }
                        catch (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdConnectionException)
                        {
                            throw;// new AdomdConnectionException(); //just to communicate what type of exception
                        }
                    }
                    return f();
                }
            }
        }

    }

    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2229:Implement serialization constructors", Justification = "<Pending>")]
    public class AdomdConnectionException : Exception
    {
        public AdomdConnectionException(string message) : base(message)
        {
        }

        public AdomdConnectionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public AdomdConnectionException()
        {
        }
    }
}
