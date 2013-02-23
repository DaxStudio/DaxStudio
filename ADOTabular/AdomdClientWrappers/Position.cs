extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class Position
    {
        private AsAdomdClient.Position _obj;
        private ExcelAdomdClient.Position _objExcel;

        public Position(AsAdomdClient.Position obj)
        {
            _obj = obj;
        }
        public Position(ExcelAdomdClient.Position obj)
        {
            _objExcel = obj;
        }

        public MemberCollection Members
        {
            get
            {
                if (_obj != null)
                {
                    MemberCollection coll = new MemberCollection();
                    foreach (AsAdomdClient.Member member in _obj.Members)
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
                        foreach (ExcelAdomdClient.Member member in _objExcel.Members)
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
}
