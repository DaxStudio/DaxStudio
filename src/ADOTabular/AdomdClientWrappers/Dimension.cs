extern alias ExcelAdomdClientReference;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class Dimension
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.Dimension _obj;
        private readonly ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Dimension _objExcel;

        public Dimension(Microsoft.AnalysisServices.AdomdClient.Dimension dimension)
        {
            _obj = dimension;
        }
        public Dimension(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Dimension dimension)
        {
            _objExcel = dimension;
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
                    List<Hierarchy> f()
                    {
                        List<Hierarchy> list = new List<Hierarchy>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Hierarchy dim in _objExcel.Hierarchies)
                        {
                            list.Add(new Hierarchy(dim));
                        }
                        return list;
                    }
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
                    string f()
                    {
                        return _objExcel.Caption;
                    }
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
                    string f()
                    {
                        return _objExcel.Name;
                    }
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
                    string f()
                    {
                        return _objExcel.UniqueName;
                    }
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
                    string f()
                    {
                        return _objExcel.Description;
                    }
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
                    PropertyCollection f()
                    {
                        PropertyCollection coll = new PropertyCollection();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Property prop in _objExcel.Properties)
                        {
                            coll.Add(prop.Name, new Property(prop.Name, prop.Value, prop.Type));
                        }
                        return coll;
                    }
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
                if (string.Compare(d.Name, index, true, System.Globalization.CultureInfo.InvariantCulture) == 0)
                {
                    return d;
                }
            }
            return null;
        }
    }

}
