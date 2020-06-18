using DaxStudio.Interfaces.Enums;
/// <summary>
/// This class takes care of swapping delimiters from US to NonUS styles
/// </summary>
namespace DaxStudio.UI.Utils.DelimiterTranslator
{
    public class DelimiterStateMachine : StringStateMachine<DelimiterStateMachine>
    {
        // states
        private static State OtherText = new State("Other", (sm, str, pos) => {
            sm.EventHappens(str, pos);
            return ProcessOtherText((DelimiterStateMachine)sm, str, pos);
        });
        private static State TableName = new State("Table");
        private static State ColumnName = new State("Column");
        private static State StringConstant = new State("String");
        private static State LineCommentState = new State("LineComment");
        private static State BlockCommentState = new State("BlockComment"); 


        // significant characters
        private const char SingleQuote = '\'';
        private const char DoubleQuote = '"';
        private const char OpenSquareBracket = '[';
        private const char CloseSquareBracket = ']';
        private const char Dash = '-';
        private const char ForwardSlash = '/';
        private const char Star = '*';
        private const char NewLine = '\n';

        public DelimiterType TargetDelimiterType = DelimiterType.Unknown;

        #region Constructors
        static DelimiterStateMachine()
        {
            // setup all the state transitions
            OtherText.When(SingleQuote, (sm, s, str, pos) => { return TableName; })
                .When(DoubleQuote, (sm, s, str, pos) => { return StringConstant; })
                .When(OpenSquareBracket, (sm, s, str, pos) => { return ColumnName; })
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

            TableName.When(SingleQuote, (sm, s, str, pos) => { return OtherText; });
            
            ColumnName.When(CloseSquareBracket, (sm, s, str, pos) => { return OtherText; });
            
            StringConstant.When(DoubleQuote, (sm, s, str, pos) => { return OtherText; });
            
            LineCommentState.When(NewLine, (sm, s, str, pos) => { return OtherText; });
            
            BlockCommentState.When(ForwardSlash, (sm, s, str, pos) => {
                if (str[pos - 1] == Star) return OtherText;
                return BlockCommentState; 
            });

        }

        public DelimiterStateMachine() : base(OtherText) { TargetDelimiterType = DelimiterType.Unknown; }
        public DelimiterStateMachine(DelimiterType targetDelimiterType) : base(OtherText) { TargetDelimiterType = targetDelimiterType; }
        public DelimiterStateMachine(State initial, DelimiterType targetDelimiterType) : base(initial) { TargetDelimiterType = targetDelimiterType; }
        #endregion

        public static char ProcessOtherText(DelimiterStateMachine sm, string input, int pos)
        {
            switch (input[pos])
            {
                case ';':
                    if (sm.TargetDelimiterType == DelimiterType.Unknown) { sm.TargetDelimiterType = DelimiterType.Comma; }
                    if (sm.TargetDelimiterType == DelimiterType.Comma) { return ','; }
                    return input[pos];
                case '.':
                    if (sm.TargetDelimiterType == DelimiterType.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        {
                            sm.TargetDelimiterType = DelimiterType.SemiColon;
                        }
                    }
                    if (sm.TargetDelimiterType == DelimiterType.SemiColon) { return ','; }
                    return input[pos];
                case ',':
                    if (sm.TargetDelimiterType == DelimiterType.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        {
                            sm.TargetDelimiterType = DelimiterType.Comma;
                        }
                        else
                        {
                            sm.TargetDelimiterType = DelimiterType.SemiColon;
                        }
                    }
                    switch (sm.TargetDelimiterType)
                    {
                        case DelimiterType.SemiColon:
                            return ';';
                        case DelimiterType.Comma:
                            return '.';
                        default:
                            return input[pos];
                    }
                default:
                    return input[pos];
            }
        }

        static bool IsNumeric(char c)
        {
            int tmp = 0;
            return int.TryParse(c.ToString(), out tmp);
        }

    }
}
