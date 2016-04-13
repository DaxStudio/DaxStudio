
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

        public DelimiterType TargetDelimiterType = DelimiterType.Unknown;

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
