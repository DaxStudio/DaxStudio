using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.DelimiterTranslator
{
    class CharStateTable : IDelimiterStateMachine
    {
        public IDelimiterStateMachine Process(string input, int pos, DelimiterState targetRegion, StringBuilder output)
        {
            output.Append(input[pos]);
            switch (input[pos])
            {
                case '\'': return new CharStateOther();
                default:   return this;
            }
        }
    }
}
