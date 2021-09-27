using System;
using System.Text;
using System.Text.RegularExpressions;
namespace DaxStudio.UI.Utils
{
    public static class FormatDebugMode
    {
        public static string MoveCommasToDebugMode(string daxExpression)
        {
            StringBuilder sb = new StringBuilder();
            var list = daxExpression.Split(new[] { "\n" }, StringSplitOptions.None);
            bool addInitialComma = false;
            for (int line = 0; line < list.Length; line++)
            {
                
                string trimmedLine = list[line].TrimEnd();
                string trailingComment;
                (trimmedLine, trailingComment) = TrimTrailingComment(trimmedLine);
                string result = addInitialComma & (trimmedLine.Length > 0 ) ? "," : string.Empty;
                bool commaLast = trimmedLine.EndsWith(",");
                var nextLineAvailable = ((line + 1) < list.Length) ? !list[line + 1].TrimStart().StartsWith(",") : false;
                if (commaLast && nextLineAvailable && trimmedLine.Length > 0)
                {
                    result += trimmedLine.Substring(0, Math.Max(trimmedLine.Length - 1, 0)) + trailingComment;
                    addInitialComma = true;
                }
                else
                {
                    result += trimmedLine + trailingComment;
                    if (trimmedLine.Length > 0)
                        addInitialComma = false;
                }
                sb.Append(result);
                sb.Append("\n");

            }
            return sb.ToString().TrimEnd();
        }


        public static string MoveCommasFromDebugMode(string daxExpression)
        {
            StringBuilder sb = new StringBuilder();
            var list = daxExpression.Split(new[] { "\n" }, StringSplitOptions.None);
            bool addTrailingComma = false;
            string nextTrimmedLine = string.Empty;
            string nextTrailingComment = string.Empty;
            
            for (int line = list.Length-1; line >= 0 ; line--)
            {

                string trimmedLine = list[line].TrimEnd();
                string trailingComment;

                string result = string.Empty;
                
                if (line == list.Length-1)
                    (trimmedLine, trailingComment) = TrimTrailingComment(trimmedLine);
                else
                {
                    trimmedLine = nextTrimmedLine;
                    trailingComment = nextTrailingComment;
                }
                if ( line != 0)
                    (nextTrimmedLine, nextTrailingComment ) = TrimTrailingComment(list[line-1].TrimEnd());

                result += addTrailingComma & (trimmedLine.Length > 0) ? "," : string.Empty;
                bool commaFirst = trimmedLine.StartsWith(",");
                var nextLineAvailable = ((line - 1) >= 0) ? !nextTrimmedLine.EndsWith(",") : false;
                if (commaFirst && nextLineAvailable && trimmedLine.Length > 0)
                {
                    result =  trimmedLine.Substring(1) + result + trailingComment ;
                    addTrailingComma = true;
                }
                else
                {
                    result = trimmedLine + result + trailingComment;
                    if (trimmedLine.Length > 0)
                        addTrailingComma = false;
                }
                
                sb.Insert(0, result + "\n");
            }
            return sb.ToString().TrimEnd();
        }

        private static (string trimmedLine, string trailingComment) TrimTrailingComment(string trimmedLine)
        {
            var commentMatch = comment.Match(trimmedLine);
            
            if (commentMatch.Success) 
                return (trimmedLine.Substring(0, commentMatch.Index), trimmedLine.Substring(commentMatch.Index));
            else
                return (trimmedLine, string.Empty);

        }

        private static Regex leadingComma = new Regex(@"\n\,", RegexOptions.Compiled);
        private static Regex comment = new Regex(@"(\s*(?://|--).*)$|(\s*/\*.*?\*/(?!w*|\,))", RegexOptions.Compiled);

        public static string ToggleDebugCommas(string test)
        {
            if (leadingComma.IsMatch(test)) return MoveCommasFromDebugMode(test);
            return MoveCommasToDebugMode(test);
        }
    }
}
