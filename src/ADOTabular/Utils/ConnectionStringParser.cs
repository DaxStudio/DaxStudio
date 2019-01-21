using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADOTabular.Utils
{
    public static class ConnectionStringParser
    {
        public enum ParsingState
        {
            InKey,
            InValue,
            InSingleQuotedValue,
            InDoubleQuotedValue
        }

        public static Dictionary<string,string> Parse(string connectionString)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            ParsingState state = ParsingState.InKey;
            StringBuilder key = new StringBuilder();
            StringBuilder value = new StringBuilder();
            int charIndex = 0;
            foreach (char c in connectionString)
            {
                switch (c)
                {
                    case ';':
                        switch (state)
                        {
                            case ParsingState.InValue:
                                state = ParsingState.InKey;
                                results.Add(key.ToString(), value.ToString());
                                key.Clear();
                                value.Clear();
                                break;
                            case ParsingState.InDoubleQuotedValue:
                            case ParsingState.InSingleQuotedValue:
                                value.Append(c);
                                break;
                            default:
                                throw new ArgumentException($"Unexpected character '{c}' at position {charIndex} while parsing connection string");
                                
                        }
                        break;
                    case '=':
                        switch (state)
                        {
                            case ParsingState.InKey:
                                state = ParsingState.InValue;
                                break;
                            case ParsingState.InDoubleQuotedValue:
                            case ParsingState.InSingleQuotedValue:
                                value.Append(c);
                                break;
                            default:
                                throw new ArgumentException($"Unexpected character '{c}' at position {charIndex} while parsing connection string");
                        }
                        break;
                    case '\'':
                        switch (state)
                        {
                            case ParsingState.InValue:
                                state = ParsingState.InSingleQuotedValue;
                                value.Append(c);
                                break;
                            case ParsingState.InDoubleQuotedValue:
                                value.Append(c);
                                break;
                            case ParsingState.InSingleQuotedValue:
                                state = ParsingState.InValue; // quote has been closed
                                value.Append(c);
                                break;
                            default:
                                throw new ArgumentException($"Unexpected character '{c}' at position {charIndex} while parsing connection string");
                        }
                        break;
                    case '\"':
                        switch (state)
                        {
                            case ParsingState.InValue:
                                state = ParsingState.InDoubleQuotedValue;
                                value.Append(c);
                                break;
                            case ParsingState.InDoubleQuotedValue:
                                value.Append(c);
                                state = ParsingState.InValue; // quotes have been closed
                                break;
                            case ParsingState.InSingleQuotedValue:
                                value.Append(c);
                                break;
                            default:
                                throw new ArgumentException($"Unexpected character '{c}' at position {charIndex} while parsing connection string");
                        }
                        break;
                    default:
                        switch (state)
                        {
                            case ParsingState.InKey:
                                key.Append(c);
                                break;
                            case ParsingState.InValue:
                            case ParsingState.InSingleQuotedValue:
                            case ParsingState.InDoubleQuotedValue:
                                value.Append(c);
                                break;
                        }
                        break;
                }
                charIndex++;
            }

            // if the connection string does not have a trailing ; add the last key and value
            if (key.Length > 0 && value.Length > 0)
                results.Add(key.ToString(), value.ToString());

            return results;
        }
    }

    
}
