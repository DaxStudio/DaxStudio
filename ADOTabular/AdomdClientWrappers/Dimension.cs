extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class Dimension
    {
        private AsAdomdClient.Dimension _obj;
        private ExcelAdomdClient.Dimension _objExcel;

        public Dimension(AsAdomdClient.Dimension obj)
        {
            _obj = obj;
        }
        public Dimension(ExcelAdomdClient.Dimension obj)
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
                    foreach (AsAdomdClient.Hierarchy dim in _obj.Hierarchies)
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
                        foreach (ExcelAdomdClient.Hierarchy dim in _objExcel.Hierarchies)
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
                    foreach (AsAdomdClient.Property prop in _obj.Properties)
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
                        foreach (ExcelAdomdClient.Property prop in _objExcel.Properties)
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
