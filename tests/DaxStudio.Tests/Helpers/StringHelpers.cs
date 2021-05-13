using System.Text.RegularExpressions;


namespace DaxStudio.Tests.Helpers
{

    public static class StringHelpers
    {
        private const char _cr = '\u000D';
        private const char _lf = '\u000A';
        private static string _crlf = new string(new[] { _cr, _lf });
        private static Regex _crlfRegex = new Regex(_cr + '|' + _lf + '|' + _crlf);

        public static string NormalizeNewline(this string str)
        {
            var result = _crlfRegex.Replace(str, "\n");
            return result.Replace("\r\n","\n");
        }
    }
}
