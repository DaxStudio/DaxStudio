extern alias ExcelAdomdClientReference;
using Microsoft.AnalysisServices.AdomdClient;

namespace ADOTabular.AdomdClientWrappers
{
    public class Level
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.Level _obj;
        private readonly ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Level _objExcel;

        public Level(Microsoft.AnalysisServices.AdomdClient.Level obj)
        {
            _obj = obj;
        }
        public Level(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Level obj)
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

        public Hierarchy ParentHierarchy
        {
            get
            {
                if (_obj != null)
                {
                    return new Hierarchy(_obj.ParentHierarchy);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<Hierarchy> f = delegate
                    {
                        return new Hierarchy(_objExcel.ParentHierarchy);
                    };
                    return f();
                }
            }
        }

        public LevelTypeEnum LevelType
        {
            get
            {
                if (_obj != null)
                {
                    return (LevelTypeEnum)_obj.LevelType;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<LevelTypeEnum> f = delegate
                    {
                        return (LevelTypeEnum)_objExcel.LevelType;
                    };
                    return f();
                }
            }
        }

        public MemberCollection GetMembers(long start, long count, string[] properties, params MemberFilter[] filters)
        {
            if (_obj != null)
            {
                MemberCollection coll = new MemberCollection();
                foreach (Microsoft.AnalysisServices.AdomdClient.Member member in _obj.GetMembers(start, count, properties, new Microsoft.AnalysisServices.AdomdClient.MemberFilter[] { }))
                {
                    coll.Add(new Member(member));
                }
                return coll;
            }
            else
            {
                ExcelAdoMdConnections.ReturnDelegate<MemberCollection> f = delegate
                {
                    MemberCollection coll = new MemberCollection();
                    foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Member member in _objExcel.GetMembers(start, count, properties, new ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.MemberFilter[] { }))
                    {
                        coll.Add(new Member(member));
                    }
                    return coll;
                };
                return f();
            }
        }

    }

}
