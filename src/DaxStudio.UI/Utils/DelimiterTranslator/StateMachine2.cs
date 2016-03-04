using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.DelimiterTranslator
{

    public enum DelimiterMode
    {
        Comma,
        SemiColon,
        Unknown
    }

    public abstract class StringStateMachine<T> where T : StringStateMachine<T>
    {
        
        public State CurrentState { get; set; }

        public StringStateMachine(State initial)
        {
            this.CurrentState = initial;
        }

        public void EventHappens(string input, int pos)
        {
            this.CurrentState = this.CurrentState.OnEvent((T)this, input, pos);
        }

        public string ProcessString(string input)
        {
            StringBuilder sb = new StringBuilder(input.Length);
            for (int pos = 0; pos < input.Length; pos++)
            {
                sb.Append(this.CurrentState.ProcessCharacter((T)this, input, pos));
                this.CurrentState.OnEvent((T)this, input, pos);
            }
            return sb.ToString();
        }

        public class State
        {
            /// <summary>
            /// The Name of this state
            /// </summary>
            public string Name { get; private set; }

            private readonly IDictionary<char, Func<T, State, string, int, State>> transitions = new Dictionary<char, Func<T, State, string, int, State>>();

            /// <summary>
            /// Create a new State with a name and an optional entry and exit action
            /// </summary>
            public State(string name, Func<StringStateMachine<T>, string, int, char> processCharacter)
            {
                this.Name = name;
                this.ProcessCharacter = processCharacter;
            }

            public char currentChar;
            public State When(char @event, Func<T, State, string, int, State> action)
            {
                transitions.Add(@event, action);
                currentChar = @event;
                return this;
            }

            public Func<StringStateMachine<T>, string, int, char> ProcessCharacter;

            public State OnEvent(T parent, string input, int pos)
            {
                Func<T, State, string, int, State> transition = null;
                char c = input[pos];
                if (transitions.TryGetValue(c, out transition))
                {
                    State newState = transition(parent, this, input, pos);
                    return newState;
                }
                else
                    return this;        // did not change state
            }

            public override string ToString()
            {
                return "*" + this.Name + "*";
            }
        }
    }
    public class DelimiterStateMachine : StringStateMachine<DelimiterStateMachine>
    {
        // states
        private static State OtherText = new State("Other", (sm, s, pos) => { return s[pos]; });
        private static State TableName = new State("Table", (sm, s, pos) => { return s[pos]; });
        private static State ColumnName = new State("Column", (sm, s, pos) => { return s[pos]; });
        private static State StringConstant = new State("String", (sm, s,pos) => { return s[pos]; });
        
        // significant characters
        private const char SingleQuote = '\'';
        private const char DoubleQuote = '"';
        private const char OpenSquareBracket = '[';
        private const char CloseSquareBracket = ']';

        public DelimiterMode CurrentMode = DelimiterMode.Unknown;

        #region Constructors
        static DelimiterStateMachine()
        {
            OtherText.When(SingleQuote, (sm, s, str, pos) => { return TableName; })
                .When(DoubleQuote, (sm, s, str, pos) => { return StringConstant; })
                .When(OpenSquareBracket, (sm, s, str, pos) => { return ColumnName; });
            OtherText.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return ProcessOtherText((DelimiterStateMachine)sm, str, pos); };

            TableName.When(SingleQuote, (sm, s, str, pos) => { return OtherText; });
            TableName.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return str[pos]; };

            ColumnName.When(CloseSquareBracket, (sm, s, str, pos) => { return OtherText; });
            ColumnName.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return str[pos]; };

            StringConstant.When(DoubleQuote, (sm, s, str, pos) => { return OtherText; });
            StringConstant.ProcessCharacter = (sm, str, pos) => {
                sm.EventHappens(str, pos);
                return str[pos]; };

        }

        public DelimiterStateMachine():base(OtherText) { CurrentMode = DelimiterMode.Unknown; }
        public DelimiterStateMachine(DelimiterMode mode) : base(OtherText) { CurrentMode = mode; }
        public DelimiterStateMachine(State initial, DelimiterMode mode) : base(initial) { CurrentMode = mode; }
        #endregion

        public static char ProcessOtherText(DelimiterStateMachine sm, string input, int pos)
        {
            switch (input[pos])
            {
                case ';':
                    if (sm.CurrentMode == DelimiterMode.Unknown ) { sm.CurrentMode = DelimiterMode.Comma; }
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

