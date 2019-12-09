extern alias ExcelAdomdClientReference;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class NamedSet
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.NamedSet _obj;
        private readonly ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.NamedSet _objExcel;

        public NamedSet(Microsoft.AnalysisServices.AdomdClient.NamedSet obj)
        {
            _obj = obj;
        }
        public NamedSet(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.NamedSet obj)
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
                    string f() => _objExcel.Name;
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
                    string f() => _objExcel.Description;
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
                    CubeDef f() => new CubeDef(_objExcel.ParentCube);
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
