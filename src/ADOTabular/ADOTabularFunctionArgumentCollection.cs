using System.Collections.Generic;
using System.Data;
using System.Collections;

namespace ADOTabular
{
    public class ADOTabularFunctionArgumentCollection:IEnumerable<ADOTabularFunctionArgument>
    {
        private readonly DataRow[] _paramInfo;

        public ADOTabularFunctionArgumentCollection(DataRow[] paramInfo)
        {
            _paramInfo = paramInfo;
        }

        public IEnumerator<ADOTabularFunctionArgument> GetEnumerator()
        {
            foreach (DataRow dr in _paramInfo)
            {
                yield return new ADOTabularFunctionArgument(dr);
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
            var arrParam = new List<string>();
            
            foreach (DataRow dr in _paramInfo)
            {
                if (dr["OPTIONAL"].ToString() == "TRUE" && dr["REPEATING"].ToString() == "TRUE")
                {
                    arrParam.Add($"[«{dr["NAME"]}»,...]");
                }
                else if (dr["OPTIONAL"].ToString() == "TRUE")
                {
                    arrParam.Add($"[«{dr["NAME"]}»]");
                }
                else if (dr["REPEATABLE"].ToString() == "TRUE")
                {
                    arrParam.Add($"«{dr["NAME"]}»,...");
                }
                else
                {
                    arrParam.Add($"«{dr["NAME"]}»");
                }
            }
            return string.Join(",", arrParam.ToArray());
        }
    }
}
