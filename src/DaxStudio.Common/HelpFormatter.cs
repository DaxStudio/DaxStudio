using Fclp;
using Fclp.Internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.Common
{
    public static class HelpFormatter
    {
        
        public static string Format(FluentCommandLineParser parser)
        {

            var sb = new StringBuilder();
            sb.AppendLine("Syntax: dscmd <Command> <Options>");
            sb.AppendLine();
            foreach (var cmd in parser.Commands)
            {
                sb.AppendLine("COMMAND: "+ cmd.Name);
                var options = cmd.Options;

                sb.Append(Format(options));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static string Format(IEnumerable<ICommandLineOption> options)
        {

            var sb = new StringBuilder();
            
            

                var maxArgLen = options.Max(o => (o.HasShortName ? 2 + o.ShortName.Length : 0) + (o.HasLongName ? 3 + o.LongName.Length : 0) + (o.SetupType == typeof(bool) ? 0 : o.SetupType.Name.Length + 2)) + 2;
                foreach (var option in options)
                {
                    if (string.IsNullOrWhiteSpace(option.Description)) continue;

                    sb.Append("  ");

                    var currLen = 0;
                    if (option.HasShortName)
                    {
                        sb.Append('-');
                        sb.Append(option.ShortName);
                        sb.Append(' ');
                        currLen += option.ShortName.Length + 2;
                    }
                    if (option.HasLongName)
                    {
                        sb.Append($"--");
                        sb.Append(option.LongName);
                        sb.Append(' ');
                        currLen += option.LongName.Length + 3;
                    }
                    if (!(option.SetupType == typeof(bool)))
                    {
                        sb.Append('<');
                        sb.Append(option.SetupType.Name.ToLower());
                        sb.Append('>');
                        currLen += option.SetupType.Name.Length + 2;
                    }

                    sb.Append(new String(' ', maxArgLen - currLen));
                    sb.AppendLine(option.Description);

                }
            
            return sb.ToString();
        }


    }
}
