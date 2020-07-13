using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular
{
    public class ADOTabularModelCapabilities
    {
        /*
         * <bi:ModelCapabilities>
                <bi:EncourageIsEmptyDAXFunctionUsage>true</bi:EncourageIsEmptyDAXFunctionUsage>
                <bi:QueryBatching>1</bi:QueryBatching>
                <bi:Variables>1</bi:Variables>
                <bi:DAXFunctions>
                  <bi:SummarizeColumns>1</bi:SummarizeColumns>
                  <bi:SubstituteWithIndex>1</bi:SubstituteWithIndex>
                  <bi:LeftOuterJoin>1</bi:LeftOuterJoin>
                  <bi:BinaryMinMax>1</bi:BinaryMinMax>
                </bi:DAXFunctions>
              </bi:ModelCapabilities>
         */

        public DaxFunctions DAXFunctions { get; set; } = new DaxFunctions();
    }
    public class DaxFunctions
    {
        public bool SummarizeColumns { get; set; } = false;
        public bool SubstituteWithIndex { get; set; } = false;
        public bool LeftOuterJoin { get; set; } = false;
        public bool BinaryMinMax { get; set; } = false;
    }
}
