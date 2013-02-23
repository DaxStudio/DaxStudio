extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class Kpi
    {
        private AsAdomdClient.Kpi _obj;
        private ExcelAdomdClient.Kpi _objExcel;

        public Kpi(AsAdomdClient.Kpi obj)
        {
            _obj = obj;
        }
        public Kpi(ExcelAdomdClient.Kpi obj)
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

        public string StatusGraphic
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.StatusGraphic;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.StatusGraphic;
                    };
                    return f();
                }
            }
        }

        public string TrendGraphic
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.TrendGraphic;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.TrendGraphic;
                    };
                    return f();
                }
            }
        }

        public Kpi ParentKpi
        {
            get
            {
                if (_obj != null)
                {
                    return new Kpi(_obj.ParentKpi);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<Kpi> f = delegate
                    {
                        return new Kpi(_objExcel.ParentKpi);
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
