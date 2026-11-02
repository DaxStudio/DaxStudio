using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    public class ParameterCommand : ScriptCommand
    {
        public ParameterCommand(string name, object value, string typename)
        {
            ParameterName = name;
            Value = value;
            TypeName = typename;
        }
        public string ParameterName { get;  }
        public object Value { get;  }
        public string TypeName { get; }
    }
}
