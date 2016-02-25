using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.RegionalTranslator
{
    class CharStateTable : IRegionalStateMachine
    {
        public IRegionalStateMachine Process(string input, int pos, RegionalState targetRegion, StringBuilder output)
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
