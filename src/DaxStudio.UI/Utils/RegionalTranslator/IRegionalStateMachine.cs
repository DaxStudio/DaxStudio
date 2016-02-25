using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.RegionalTranslator
{
    public interface IRegionalStateMachine
    {
        IRegionalStateMachine Process(string input, int pos, RegionalState targetRegion, StringBuilder output);
    };
}
