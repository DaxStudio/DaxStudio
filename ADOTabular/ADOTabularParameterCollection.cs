using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularParameterCollection:IEnumerable<ADOTabularParameter>,IEnumerable
    {
        private DataRow[] _paramInfo;

        public ADOTabularParameterCollection(DataRow[] paramInfo)
        {
            _paramInfo = paramInfo;
            

        }

        public IEnumerator<ADOTabularParameter> GetEnumerator()
        {
            foreach (DataRow dr in _paramInfo)
            {
                yield return new ADOTabularParameter(dr);
            }
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // format the paramters as
        // <<param>> - required
        // [<<param>>] optional
        // <<param,...>>, repeating?? 
        public override string ToString()
        {
            List<string> arrParam = new List<string>();
            StringBuilder sbParam = new StringBuilder();
            foreach (DataRow dr in _paramInfo)
            {
                if (dr["OPTIONAL"].ToString() == "TRUE" && dr["REPEATING"].ToString() == "TRUE")
                {
                    arrParam.Add(string.Format("[«{0}»,...]", dr["NAME"]));
                }
                else if (dr["OPTIONAL"].ToString() == "TRUE")
                {
                    arrParam.Add(string.Format("[«{0}»]", dr["NAME"]));
                }
                else if (dr["REPEATABLE"].ToString() == "TRUE")
                {
                    arrParam.Add(string.Format("«{0}»,...", dr["NAME"]));
                }
                else
                {
                    arrParam.Add(string.Format("«{0}»", dr["NAME"]));
                }
            }
            return string.Join(",", arrParam.ToArray());
        }
    }
}
