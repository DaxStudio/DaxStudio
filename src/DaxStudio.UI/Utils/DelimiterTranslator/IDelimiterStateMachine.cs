using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.DelimiterTranslator
{
    public interface IDelimiterStateMachine
    {
        IDelimiterStateMachine Process(string input, int pos, DelimiterState targetRegion, StringBuilder output);
    };
}
