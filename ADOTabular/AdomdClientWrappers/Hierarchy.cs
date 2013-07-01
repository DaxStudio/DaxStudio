extern alias ExcelAdomdClientReference;
using System.Collections.Generic;
using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular.AdomdClientWrappers
{
    public class Hierarchy
    {
        private Microsoft.AnalysisServices.AdomdClient.Hierarchy _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Hierarchy _objExcel;

        public Hierarchy(Microsoft.AnalysisServices.AdomdClient.Hierarchy obj)
        {
            _obj = obj;
        }
        public Hierarchy(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Hierarchy obj)
        {
            _objExcel = obj;
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

        public Dimension ParentDimension
        {
            get
            {
                if (_obj != null)
                {
                    return new Dimension(_obj.ParentDimension);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<Dimension> f = delegate
                    {
                        return new Dimension(_objExcel.ParentDimension);
                    };
                    return f();
                }
            }
        }
        public Microsoft.AnalysisServices.AdomdClient.HierarchyOrigin HierarchyOrigin
        {
            get
            {
                if (_obj != null)
                {
                    return (Microsoft.AnalysisServices.AdomdClient.HierarchyOrigin)_obj.HierarchyOrigin;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<HierarchyOrigin> f = delegate
                    {
                        return (Microsoft.AnalysisServices.AdomdClient.HierarchyOrigin)_objExcel.HierarchyOrigin;
                    };
                    return f();
                }
            }
        }

        public List<Level> Levels
        {
            get
            {
                if (_obj != null)
                {
                    List<Level> list = new List<Level>();
                    foreach (Microsoft.AnalysisServices.AdomdClient.Level level in _obj.Levels)
                    {
                        list.Add(new Level(level));
                    }
                    return list;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<List<Level>> f = delegate
                    {
                        List<Level> list = new List<Level>();
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Level level in _objExcel.Levels)
                        {
                            list.Add(new Level(level));
                        }
                        return list;
                    };
                    return f();
                }
            }
        }
    }
}
