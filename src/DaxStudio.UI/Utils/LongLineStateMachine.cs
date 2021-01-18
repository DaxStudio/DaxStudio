using System.Collections.Generic;

namespace DaxStudio.UI.Utils
{
    /// <summary>
    /// This class takes care of swapping delimiters from US to NonUS styles
    /// </summary>
    public class LongLineStateMachine : LineStateMachine<LongLineStateMachine>
    {
        // states
        private static readonly State OtherText = new State("Other", (sm, str, pos) => {
            sm.EventHappens(str, pos);
            return ProcessOtherText((LongLineStateMachine)sm, str, pos);
        });
        private static readonly State TableName = new State("Table");
        private static readonly State ColumnName = new State("Column");
        private static readonly State StringConstant = new State("String");
        private static readonly State LineCommentState = new State("LineComment");
        private static readonly State BlockCommentState = new State("BlockComment"); 


        // significant characters
        private const char SingleQuote = '\'';
        private const char DoubleQuote = '"';
        private const char OpenSquareBracket = '[';
        private const char CloseSquareBracket = ']';
        private const char Dash = '-';
        private const char ForwardSlash = '/';
        private const char Star = '*';
        private const char NewLine = '\n';

        //public DelimiterType TargetDelimiterType = DelimiterType.Unknown;
        public int MaxLineLength = 5000;


        #region Constructors
        static LongLineStateMachine()
        {
            // setup all the state transitions
            OtherText.When(SingleQuote, (sm, s, str, pos) => TableName)
                .When(DoubleQuote, (sm, s, str, pos) => StringConstant)
                .When(OpenSquareBracket, (sm, s, str, pos) => ColumnName)
                .When(Dash, (sm, s, str, pos) => {
                    if (pos == str.Length - 1) return OtherText;
                    if (str[pos + 1] == Dash) return LineCommentState;
                    return OtherText;
                 })
                .When(ForwardSlash, (sm, s, str, pos) => {
                    if (pos == str.Length - 1) return OtherText;
                    if (str[pos + 1] == ForwardSlash) return LineCommentState;
                    if (str[pos + 1] == Star) return BlockCommentState;
                    return OtherText;
                });

            TableName.When(SingleQuote, (sm, s, str, pos) => OtherText);
            
            ColumnName.When(CloseSquareBracket, (sm, s, str, pos) => OtherText);
            
            StringConstant.When(DoubleQuote, (sm, s, str, pos) => OtherText);
            
            LineCommentState.When(NewLine, (sm, s, str, pos) => OtherText);
            
            BlockCommentState.When(ForwardSlash, (sm, s, str, pos) => {
                if (pos == 0) return BlockCommentState; // if block comment is at the start of the string
                if (str[pos - 1] == Star) return OtherText;
                return BlockCommentState; 
            });

        }

        public LongLineStateMachine() : base(OtherText) {  }
        public LongLineStateMachine(int maxLineLength) : base(OtherText) { MaxLineLength = maxLineLength; }

        public LongLineStateMachine(State initial, int maxLineLength) : base(initial) { MaxLineLength = maxLineLength; }
        #endregion

        public static IEnumerable<char> ProcessOtherText(LongLineStateMachine sm, string input, int pos)
        {
            switch (input[pos])
            {
                case ')':
                case '}':
                case ' ':
                    if (sm.LinePosition > sm.MaxLineLength)
                    {
                        char nextChar = (pos + 1) < input.Length ? input[pos + 1] : char.MinValue;
                        yield return input[pos];
                        if (nextChar != '\r' && nextChar != '\n') sm.InsertNewLine();
                        yield break;
                    }

                    yield return input[pos];
                    yield break;
                default:
                    yield return input[pos];
                    yield break;
            }
        }

    }
}
