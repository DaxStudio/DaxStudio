extern alias ExcelAdomdClientReference;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class Dimension
    {
        private Microsoft.AnalysisServices.AdomdClient.Dimension _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Dimension _objExcel;

        public Dimension(Microsoft.AnalysisServices.AdomdClient.Dimension obj)
        {
            _obj = obj;
        }
        public Dimension(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Dimension obj)
        {
            _objExcel = obj;
        }

        public List<Hierarchy> Hierarchies
        {
            get
            {
                if (_obj != null)
                {
                    List<Hierarchy> list = new List<Hierarchy>();
                    foreach (Microsoft.AnalysisServices.AdomdClient.Hierarchy dim in _obj.Hierarchies)
                    {
                        list.Add(new Hierarchy(dim));
                    }
                    return list;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<List<Hierarchy>> f = delegate
                    {
                        List<Hierarchy> list = new List<Hierarchy>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Hierarchy dim in _objExcel.Hierarchies)
                        {
                            list.Add(new Hierarchy(dim));
                        }
                        return list;
                    };
                    return f();
                }
            }
        }

        public string Caption
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.Caption;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.Caption;
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

        public string UniqueName
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.UniqueName;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.UniqueName;
                    };
                    return f();
                }
            }
        }

        public string Description
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.Description;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.Description;
                    };
                    return f();
                }
            }
        }

        public PropertyCollection Properties
        {
            get
            {
                if (_obj != null)
                {
                    PropertyCollection coll = new PropertyCollection();
                    foreach (Microsoft.AnalysisServices.AdomdClient.Property prop in _obj.Properties)
                    {
                        coll.Add(prop.Name, new Property(prop.Name, prop.Value, prop.Type));
                    }
                    return coll;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<PropertyCollection> f = delegate
                    {
                        PropertyCollection coll = new PropertyCollection();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Property prop in _objExcel.Properties)
                        {
                            coll.Add(prop.Name, new Property(prop.Name, prop.Value, prop.Type));
                        }
                        return coll;
                    };
                    return f();
                }
            }
        }

    }

    public class DimensionCollection : List<Dimension>
    {
        public Dimension Find(string index)
        {
            foreach (Dimension d in this)
            {
                if (string.Compare(d.Name, index, true) == 0)
                {
                    return d;
                }
            }
            return null;
        }
    }

}
