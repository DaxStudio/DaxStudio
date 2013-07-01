extern alias ExcelAdomdClientReference;
using System;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class CubeDef
    {
        private Microsoft.AnalysisServices.AdomdClient.CubeDef _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeDef _objExcel;

        public CubeDef(Microsoft.AnalysisServices.AdomdClient.CubeDef obj)
        {
            _obj = obj;
        }
        public CubeDef(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeDef obj)
        {
            _objExcel = obj;
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
                    ExcelAdoMdConnections.ReturnDelegate<AdomdConnection> f = delegate
                    {
                        return new AdomdConnection(_objExcel.ParentConnection);
                    };
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
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.Name;
                    };
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
                    ExcelAdoMdConnections.ReturnDelegate<DimensionCollection> f = delegate
                    {
                        DimensionCollection list = new DimensionCollection();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Dimension dim in _objExcel.Dimensions)
                        {
                            list.Add(new Dimension(dim));
                        }
                        return list;
                    };
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
                    ExcelAdoMdConnections.ReturnDelegate<List<Measure>> f = delegate
                    {
                        List<Measure> list = new List<Measure>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Measure dim in _objExcel.Measures)
                        {
                            list.Add(new Measure(dim));
                        }
                        return list;
                    };
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
                    ExcelAdoMdConnections.ReturnDelegate<List<Kpi>> f = delegate
                    {
                        List<Kpi> list = new List<Kpi>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Kpi dim in _objExcel.Kpis)
                        {
                            list.Add(new Kpi(dim));
                        }
                        return list;
                    };
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
                    ExcelAdoMdConnections.ReturnDelegate<NamedSetCollection> f = delegate
                    {
                        NamedSetCollection list = new NamedSetCollection();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.NamedSet dim in _objExcel.NamedSets)
                        {
                            list.Add(new NamedSet(dim));
                        }
                        return list;
                    };
                    return f();
                }
            }
        }
    }

    public class CubeCollection
    {
        private Microsoft.AnalysisServices.AdomdClient.CubeCollection _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeCollection _objExcel;

        public CubeCollection(Microsoft.AnalysisServices.AdomdClient.CubeCollection obj)
        {
            _obj = obj;
        }
        public CubeCollection(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeCollection obj)
        {
            _objExcel = obj;
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
                ExcelAdoMdConnections.ReturnDelegate<CubeDef> f = delegate
                {
                    ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.CubeDef obj = _objExcel.Find(index);
                    return obj == null ? null : new CubeDef(obj);
                };
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
                        throw new AdomdConnectionException(); //just to communicate what type of exception
                    }
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<int> f = delegate
                    {
                        try
                        {
                            return _objExcel.Count;
                        }
                        catch (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.AdomdConnectionException)
                        {
                            throw new AdomdConnectionException(); //just to communicate what type of exception
                        }
                    };
                    return f();
                }
            }
        }

    }

    public class AdomdConnectionException : Exception
    {
    }
}
