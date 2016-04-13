using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.DelimiterTranslator
{
    class CharStateOther : IDelimiterStateMachine
    {
        public IDelimiterStateMachine Process(string input, int pos, DelimiterState targetRegion, StringBuilder output)
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
                    if (targetRegion == DelimiterState.Unknown) { targetRegion = DelimiterState.Comma; }
                    if (targetRegion == DelimiterState.Comma) { output.Append(','); }
                    else { output.Append(input[pos]); }
                    return this;
                case '.':
                    if (targetRegion == DelimiterState.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        { 
                            targetRegion = DelimiterState.SemiColon;
                        }
                    }
                    if (targetRegion == DelimiterState.SemiColon) { output.Append(','); }
                    if (targetRegion == DelimiterState.Comma) { output.Append(input[pos]); }
                    return this;
                case ',':
                    if (targetRegion == DelimiterState.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        {
                            targetRegion = DelimiterState.Comma;
                        }
                        else
                        {
                            targetRegion = DelimiterState.SemiColon;
                        }
                    }
                    switch (targetRegion)
                    {
                        case DelimiterState.SemiColon:
                            output.Append(';');
                            break;
                        case DelimiterState.Comma:
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
