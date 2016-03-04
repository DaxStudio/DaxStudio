
namespace DaxStudio.UI.Utils.DelimiterTranslator
{
    public class DelimiterStateMachine : StringStateMachine<DelimiterStateMachine>
    {
        // states
        private static State OtherText = new State("Other", (sm, s, pos) => { return s[pos]; });
        private static State TableName = new State("Table", (sm, s, pos) => { return s[pos]; });
        private static State ColumnName = new State("Column", (sm, s, pos) => { return s[pos]; });
        private static State StringConstant = new State("String", (sm, s, pos) => { return s[pos]; });

        // significant characters
        private const char SingleQuote = '\'';
        private const char DoubleQuote = '"';
        private const char OpenSquareBracket = '[';
        private const char CloseSquareBracket = ']';

        public DelimiterMode CurrentMode = DelimiterMode.Unknown;

        #region Constructors
        static DelimiterStateMachine()
        {
            // setup all the states and transitions
            OtherText.When(SingleQuote, (sm, s, str, pos) => { return TableName; })
                .When(DoubleQuote, (sm, s, str, pos) => { return StringConstant; })
                .When(OpenSquareBracket, (sm, s, str, pos) => { return ColumnName; });
            OtherText.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return ProcessOtherText((DelimiterStateMachine)sm, str, pos);
            };

            TableName.When(SingleQuote, (sm, s, str, pos) => { return OtherText; });
            TableName.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return str[pos];
            };

            ColumnName.When(CloseSquareBracket, (sm, s, str, pos) => { return OtherText; });
            ColumnName.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return str[pos];
            };

            StringConstant.When(DoubleQuote, (sm, s, str, pos) => { return OtherText; });
            StringConstant.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return str[pos];
            };

        }

        public DelimiterStateMachine() : base(OtherText) { CurrentMode = DelimiterMode.Unknown; }
        public DelimiterStateMachine(DelimiterMode mode) : base(OtherText) { CurrentMode = mode; }
        public DelimiterStateMachine(State initial, DelimiterMode mode) : base(initial) { CurrentMode = mode; }
        #endregion

        public static char ProcessOtherText(DelimiterStateMachine sm, string input, int pos)
        {
            switch (input[pos])
            {
                case ';':
                    if (sm.CurrentMode == DelimiterMode.Unknown) { sm.CurrentMode = DelimiterMode.Comma; }
                    if (sm.CurrentMode == DelimiterMode.Comma) { return ','; }
                    return input[pos];
                case '.':
                    if (sm.CurrentMode == DelimiterMode.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        {
                            sm.CurrentMode = DelimiterMode.SemiColon;
                        }
                    }
                    if (sm.CurrentMode == DelimiterMode.SemiColon) { return ','; }
                    return input[pos];
                case ',':
                    if (sm.CurrentMode == DelimiterMode.Unknown)
                    {
                        if (pos > 0 && pos < input.Length - 1 && IsNumeric(input[pos - 1]) && IsNumeric(input[pos + 1]))
                        {
                            sm.CurrentMode = DelimiterMode.Comma;
                        }
                        else
                        {
                            sm.CurrentMode = DelimiterMode.SemiColon;
                        }
                    }
                    switch (sm.CurrentMode)
                    {
                        case DelimiterMode.SemiColon:
                            return ';';
                        case DelimiterMode.Comma:
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
