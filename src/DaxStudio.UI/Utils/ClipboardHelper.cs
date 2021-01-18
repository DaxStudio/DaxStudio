using System.Text;

namespace DaxStudio.UI.Utils
{
    public static class ClipboardHelper
    {
        const int MaxLineLen = 500;

        public static string FixupString(string input)
        {
            var sb = new StringBuilder();
            var lineLen = 0;
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                lineLen++;
                // this check strips out unicode non-breaking spaces and replaces them
                // with a "normal" space. This is helpful when pasting code from other 
                // sources like web pages or word docs which may have non-breaking
                // which would normally cause the tabular engine to throw an error
                if (c == '\u00A0') c = ' ';
                sb.Append(c);

                // reset the current line length if we hit a newline character
                if (c == '\n') lineLen = 0;

                // if the current line is greater than the specified max length then
                // insert a newline character after the next char we find in the switch list below. 
                // This prevents massive lines being pasted in which can cause the syntax 
                // highligher to hang. 
                if (lineLen > MaxLineLen)
                {
                    switch (c)
                    {
                        case ';':
                        case ',':
                        case ')':
                        case '}':
                            sb.Append('\n');
                            lineLen = 0;
                            break;
                    }
                }
            }

            return sb.ToString();
        }

        
    }
}
