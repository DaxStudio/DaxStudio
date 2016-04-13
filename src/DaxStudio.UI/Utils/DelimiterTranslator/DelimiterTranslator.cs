using System;
using System.Text;

namespace DaxStudio.UI.Utils.DelimiterTranslator
{

    public enum DelimiterState
    {
        SemiColon,
        Comma,
        Unknown
    };

    public enum CharState
    {
        String,
        Column,
        Table,
        Other
    }

    public static class DelimiterTranslator
    {

        public static string Translate(string input)
        {
            return Translate(input, DelimiterState.Unknown);
        }


        public static string Translate(string input, DelimiterState targetDelimiter)
        {
            // from European
            // ; -> ,
            // , -> .
            // from English
            // , -> ;
            // . -> ,
            // TopPercent( 10.0, 

            StringBuilder output = new StringBuilder( input.Length);

            IDelimiterStateMachine currentState = new CharStateOther();

            for (int i = 0; i < input.Length;i++)
            {
                currentState = currentState.Process(input, i, targetDelimiter, output);
            }
            return output.ToString();
        }

    }
}
