extern alias ExcelAdomdClientReference;

namespace ADOTabular.AdomdClientWrappers
{
    public class Position
    {
        private Microsoft.AnalysisServices.AdomdClient.Position _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Position _objExcel;

        public Position(Microsoft.AnalysisServices.AdomdClient.Position obj)
        {
            _obj = obj;
        }
        public Position(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Position obj)
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
                    foreach (Microsoft.AnalysisServices.AdomdClient.Member member in _obj.Members)
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
                        foreach (ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.Member member in _objExcel.Members)
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
