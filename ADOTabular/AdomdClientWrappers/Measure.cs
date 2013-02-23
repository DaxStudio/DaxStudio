extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class Measure
    {
        private AsAdomdClient.Measure _obj;
        private ExcelAdomdClient.Measure _objExcel;

        public Measure(AsAdomdClient.Measure obj)
        {
            _obj = obj;
        }
        public Measure(ExcelAdomdClient.Measure obj)
        {
            _objExcel = obj;
        }

        public string Expression
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.Expression;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.Expression;
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

        public string DisplayFolder
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.DisplayFolder;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.DisplayFolder;
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

}
