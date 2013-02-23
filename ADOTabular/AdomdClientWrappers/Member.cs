extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class Member
    {
        private AsAdomdClient.Member _obj;
        private ExcelAdomdClient.Member _objExcel;

        public Member(AsAdomdClient.Member obj)
        {
            _obj = obj;
        }
        public Member(ExcelAdomdClient.Member obj)
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

        public Level ParentLevel
        {
            get
            {
                if (_obj != null)
                {
                    return new Level(_obj.ParentLevel);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<Level> f = delegate
                    {
                        return new Level(_objExcel.ParentLevel);
                    };
                    return f();
                }
            }
        }

        public Member Parent
        {
            get
            {
                if (_obj != null)
                {
                    return new Member(_obj.Parent);
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<Member> f = delegate
                    {
                        return new Member(_objExcel.Parent);
                    };
                    return f();
                }
            }
        }

        public MemberPropertyCollection MemberProperties
        {
            get
            {
                if (_obj != null)
                {
                    MemberPropertyCollection coll = new MemberPropertyCollection();
                    foreach (AsAdomdClient.MemberProperty member in _obj.MemberProperties)
                    {
                        coll.Add(new MemberProperty(member));
                    }
                    return coll;
                }
                else
                {
                    ExcelAdoMdConnections.ReturnDelegate<MemberPropertyCollection> f = delegate
                    {
                        MemberPropertyCollection coll = new MemberPropertyCollection();
                        foreach (ExcelAdomdClient.MemberProperty member in _objExcel.MemberProperties)
                        {
                            coll.Add(new MemberProperty(member));
                        }
                        return coll;
                    };
                    return f();
                }
            }
        }
    }

    public class MemberCollection : List<Member>
    {
    }

    public class MemberFilter
    {
    }
}
