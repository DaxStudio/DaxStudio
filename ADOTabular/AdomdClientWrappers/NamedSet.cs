extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class NamedSet
    {
        private AsAdomdClient.NamedSet _obj;
        private ExcelAdomdClient.NamedSet _objExcel;

        public NamedSet(AsAdomdClient.NamedSet obj)
        {
            _obj = obj;
        }
        public NamedSet(ExcelAdomdClient.NamedSet obj)
        {
            _objExcel = obj;
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

        public CubeDef ParentCube
        {
            get
            {
                if (_obj != null)
                {
                    return new CubeDef(_obj.ParentCube);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<CubeDef> f = delegate
                    {
                        return new CubeDef(_objExcel.ParentCube);
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

    public class NamedSetCollection : List<NamedSet>
    {
        public NamedSet Find(string index)
        {
            foreach (NamedSet d in this)
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
