extern alias ExcelAdomdClientReference;

using System;
using System.Collections.Generic;
using System.Text;
using AsAdomdClient = Microsoft.AnalysisServices.AdomdClient;
using ExcelAdomdClient = ExcelAdomdClientReference::Microsoft.AnalysisServices.AdomdClient;

namespace DaxStudio.AdomdClientWrappers
{
    public class MemberProperty
    {
        private AsAdomdClient.MemberProperty _obj;
        private ExcelAdomdClient.MemberProperty _objExcel;

        public MemberProperty(AsAdomdClient.MemberProperty obj)
        {
            _obj = obj;
        }
        public MemberProperty(ExcelAdomdClient.MemberProperty obj)
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
