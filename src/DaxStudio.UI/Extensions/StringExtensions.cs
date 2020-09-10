using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Extensions
{
    public static class StringExtensions
    {
        public static string StripLineBreaks(this string input)
        {
            var sb = new StringBuilder(input.Length);
            char c;
            bool prevCharIsWhitespace = false;
            for (int i=0;i<input.Length;i++)
            {
                c = input[i];
                switch (c)
                {
                    case '\r':
                    case '\n':
                    case '\t':
                    case ' ':
                        if (!prevCharIsWhitespace) sb.Append(' ');
                        prevCharIsWhitespace = true;
                        break;
                    default:
                        sb.Append(c);
                        prevCharIsWhitespace = false;
                        break;
                }
            }
            return sb.ToString();
        }
        
        public static bool IsNumeric(this string input)
        {
            return input.All(c => Char.IsDigit(c));
        } 


        public static bool Contains(this string input, string searchFor, StringComparison comparison)
        {
            return input.IndexOf(searchFor, comparison) >= 0;
        }

        public static string Format(this string input, params object[] args)
        {
            return string.Format(input, args);
        }
    }
}
