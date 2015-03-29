extern alias ExcelAdomdClientReference;
using System.Collections.Generic;

namespace ADOTabular.AdomdClientWrappers
{
    public class MemberProperty
    {
        private Microsoft.AnalysisServices.AdomdClient.MemberProperty _obj;
        private ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient.MemberProperty _objExcel;

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
                    ExcelAdoMdConnections.ReturnDelegate<string> f = delegate
                    {
                        return _objExcel.Name;
                    };
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
                    ExcelAdoMdConnections.ReturnDelegate<object> f = delegate
                    {
                        return _objExcel.Value;
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
