extern alias ExcelAdomdClientReference;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class MemberProperty
    {
        private readonly Microsoft.AnalysisServices.AdomdClient.MemberProperty _obj;
        private readonly ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.MemberProperty _objExcel;

        public MemberProperty(Microsoft.AnalysisServices.AdomdClient.MemberProperty obj)
        {
            _obj = obj;
        }
        public MemberProperty(ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.MemberProperty obj)
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

        public object Value
        {
            get
            {
                if (_obj != null)
                {
                    return _obj.Value;
                }
                else
                {
                    object f() => _objExcel.Value;
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
                    string f() => _objExcel.UniqueName;
                    return f();
                }
            }
        }
    }

    public class MemberPropertyCollection : List<MemberProperty>
    {
        public MemberProperty this[string index]
        {
            get
            {
                foreach (MemberProperty prop in this)
                {
                    if (string.Compare(prop.Name, index, true) == 0)
                    {
                        return prop;
                    }
                }
                return null;
            }
        }
    }

}
