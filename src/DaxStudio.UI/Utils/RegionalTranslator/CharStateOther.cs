using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.RegionalTranslator
{
    class CharStateOther : IRegionalStateMachine
    {
        public IRegionalStateMachine Process(string input, int pos, RegionalState targetRegion, StringBuilder output)
        {
            switch (input[pos])
            {
                case '"':
                    output.Append(input[pos]);
                    return new CharStateString();
                case '\'':
                    output.Append(input[pos]);
                    return new CharStateTable();
                case '[':
                    output.Append(input[pos]);
                    return new CharStateColumn();
                case ';':
                    if (targetRegion == RegionalState.Unknown) { targetRegion = RegionalState.English; }
                    if (targetRegion == RegionalState.English) { output.Append(','); }
                    else { output.Append(input[pos]); }
                    return this;
                case '.':
                    if (targetRegion == RegionalState.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        { 
                            targetRegion = RegionalState.European;
                        }
                    }
                    if (targetRegion == RegionalState.European) { output.Append(','); }
                    if (targetRegion == RegionalState.English) { output.Append(input[pos]); }
                    return this;
                case ',':
                    if (targetRegion == RegionalState.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        {
                            targetRegion = RegionalState.English;
                        }
                        else
                        {
                            targetRegion = RegionalState.European;
                        }
                    }
                    switch (targetRegion)
                    {
                        case RegionalState.European:
                            output.Append(';');
                            break;
                        case RegionalState.English:
                            output.Append('.');
                            break;
                        default:
                            output.Append(input[pos]);
                            break;
                    }

                    return this;
                default:
                    output.Append(input[pos]);
                    return this;
            }
        }

        bool IsNumeric(char c)
        {
            int tmp = 0;
            return int.TryParse(c.ToString(), out tmp);
        }
    }
}
