extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class Hierarchy
    {
        private AsAdomdClient.Hierarchy _obj;
        private ExcelAdomdClient.Hierarchy _objExcel;

        public Hierarchy(AsAdomdClient.Hierarchy obj)
        {
            _obj = obj;
        }
        public Hierarchy(ExcelAdomdClient.Hierarchy obj)
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
        public AsAdomdClient.HierarchyOrigin HierarchyOrigin
        {
            get
            {
                if (_obj != null)
                {
                    return (AsAdomdClient.HierarchyOrigin)_obj.HierarchyOrigin;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<AsAdomdClient.HierarchyOrigin> f = delegate
                    {
                        return (AsAdomdClient.HierarchyOrigin)_objExcel.HierarchyOrigin;
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
                    foreach (AsAdomdClient.Level level in _obj.Levels)
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
                        foreach (ExcelAdomdClient.Level level in _objExcel.Levels)
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
