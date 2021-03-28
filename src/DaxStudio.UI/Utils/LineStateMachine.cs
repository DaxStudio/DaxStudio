using System;
using System.Collections.Generic;
using System.Text;

namespace DaxStudio.UI.Utils
{
    public abstract class LineStateMachine<T> where T : LineStateMachine<T>
    {
        private const char NewLine = '\n';
        private const string SqlQueryComment = "// SQL Query";
        private const string SqlDirectQueryComment = "// Direct Query";
        public State CurrentState { get; set; }
        private StringBuilder _sbMain;
        private StringBuilder _sbCurrentLine;
        protected LineStateMachine(State initial)
        {
            CurrentState = initial;
        }

        public void EventHappens(string input, int pos)
        {
            CurrentState = CurrentState.OnEvent((T)this, input, pos);
        }

        private int _linePosition;
        public int LinePosition
        {
            get => _linePosition;
            private set
            {
                _linePosition = value;
                if (value == 0)
                {
                    if (CurrentLineIsSqlQueryComment())
                    {
                        SqlQueryCommentFound = true;
                    }

                    _sbCurrentLine.Clear();
                }
            }
        }

        public bool CurrentLineIsSqlQueryComment()
        {
            return _sbCurrentLine.ToString() == SqlQueryComment || _sbCurrentLine.ToString() == SqlDirectQueryComment;
        }

        public bool SqlQueryCommentFound { get; private set; }
        public int SqlQueryCommentPosition { get; private set; }

        public void InsertNewLine()
        {
            _sbMain?.Append(NewLine);
            _sbCurrentLine.Clear();
            LinePosition = 0;
        }

        public string ProcessString(string input)
        {
            _sbMain = new StringBuilder(input.Length);
            _sbCurrentLine = new StringBuilder();
            for (int pos = 0; pos < input.Length; pos++)
            {
                foreach (char ch in CurrentState.ProcessCharacter((T)this, input, pos))
                {
                    _sbMain.Append(ch);
                    if (ch != '\r' && ch != '\n') _sbCurrentLine.Append(ch);
                }

                if (input[pos] == NewLine)
                {
                    if (CurrentLineIsSqlQueryComment() && SqlQueryCommentPosition == 0) SqlQueryCommentPosition = pos - LinePosition;
                    LinePosition = 0;
                }
                else LinePosition++;
                CurrentState.OnEvent((T)this, input, pos);
            }
            return _sbMain.ToString();
        }

        public class State
        {
            /// <summary>
            /// The Name of this state
            /// </summary>
            public string Name { get; }

            private readonly IDictionary<char, Func<T, State, string, int, State>> _transitions = new Dictionary<char, Func<T, State, string, int, State>>();

            /// <summary>
            /// Create a new State with a name and an optional entry and exit action
            /// </summary>
            public State(string name, Func<LineStateMachine<T>, string, int, IEnumerable<char>> processCharacter)
            {
                Name = name;
                ProcessCharacter = processCharacter;
            }

            public State(string name)
            {
                Name = name;
                ProcessCharacter = ProcessCharacterDefault;
            }

            private IEnumerable<char> ProcessCharacterDefault(LineStateMachine<T> sm, string str, int pos)
            {
                sm.EventHappens(str, pos);
                yield return str[pos];
            }

            public char CurrentChar;
            public State When(char @event, Func<T, State, string, int, State> action)
            {
                _transitions.Add(@event, action);
                CurrentChar = @event;
                return this;
            }

            public Func<LineStateMachine<T>, string, int, IEnumerable<char>> ProcessCharacter;

            public State OnEvent(T parent, string input, int pos)
            {
                Func<T, State, string, int, State> transition = null;
                char c = input[pos];
                if (_transitions.TryGetValue(c, out transition))
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
}
