using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DaxStudio.UI.Utils
{
    public static class FormatDebugMode
    {
        public static string MoveCommasToDebugMode(string test)
        {
            StringBuilder sb = new StringBuilder();
            var list = test.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            bool addInitialComma = false;
            for (int line = 0; line < list.Length; line++)
            {
                string result = addInitialComma ? "," : string.Empty;
                string trimmedLine = list[line].TrimEnd();
                bool commaLast = trimmedLine.EndsWith(",");
                var nextLineAvailable = ((line + 1) < list.Length) ? !list[line + 1].TrimStart().StartsWith(",") : false;
                if (commaLast && nextLineAvailable)
                {
                    result += list[line].TrimEnd().Substring(0, Math.Max(trimmedLine.Length - 1, 0));
                    addInitialComma = true;
                }
                else
                {
                    result += list[line];
                    addInitialComma = false;
                }
                sb.AppendLine(result);
            }
            return sb.ToString();
        }
    }
}
