using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils.DelimiterTranslator
{

    public enum DelimiterType
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
    
}

