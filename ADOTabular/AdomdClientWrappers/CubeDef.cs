extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class CubeDef
    {
        private AsAdomdClient.CubeDef _obj;
        private ExcelAdomdClient.CubeDef _objExcel;

        public CubeDef(AsAdomdClient.CubeDef obj)
        {
            _obj = obj;
        }
        public CubeDef(ExcelAdomdClient.CubeDef obj)
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
                    foreach (AsAdomdClient.Dimension dim in _obj.Dimensions)
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
                        foreach (ExcelAdomdClient.Dimension dim in _objExcel.Dimensions)
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
                    foreach (AsAdomdClient.Measure dim in _obj.Measures)
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
                        foreach (ExcelAdomdClient.Measure dim in _objExcel.Measures)
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
                    foreach (AsAdomdClient.Kpi dim in _obj.Kpis)
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
                        foreach (ExcelAdomdClient.Kpi dim in _objExcel.Kpis)
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
                    foreach (AsAdomdClient.NamedSet dim in _obj.NamedSets)
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
                        foreach (ExcelAdomdClient.NamedSet dim in _objExcel.NamedSets)
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
        private AsAdomdClient.CubeCollection _obj;
        private ExcelAdomdClient.CubeCollection _objExcel;

        public CubeCollection(AsAdomdClient.CubeCollection obj)
        {
            _obj = obj;
        }
        public CubeCollection(ExcelAdomdClient.CubeCollection obj)
        {
            _objExcel = obj;
        }
        
        public CubeDef Find(string index)
        {
            if (_obj != null)
            {
                AsAdomdClient.CubeDef obj = _obj.Find(index);
                return obj == null ? null : new CubeDef(obj);
            }
            else
            {
                ExcelAdoMdConnections.ReturnDelegate<CubeDef> f = delegate
                {
                    ExcelAdomdClient.CubeDef obj = _objExcel.Find(index);
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
                    catch (AsAdomdClient.AdomdConnectionException)
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
                        catch (ExcelAdomdClient.AdomdConnectionException)
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
