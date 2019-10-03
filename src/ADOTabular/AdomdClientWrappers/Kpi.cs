extern alias ExcelAdomdClientReference;

namespace ADOTabular.AdomdClientWrappers
{
    public class Kpi
    {
        private Microsoft.AnalysisServices.AdomdClient.Kpi _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Kpi _objExcel;

        public Kpi(Microsoft.AnalysisServices.AdomdClient.Kpi kpi)
        {
            _obj = kpi;
        }
        public Kpi(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Kpi kpi)
        {
            _objExcel = kpi;
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

}
