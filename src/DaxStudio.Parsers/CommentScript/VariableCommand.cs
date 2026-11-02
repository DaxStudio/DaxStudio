using DaxStudio.Parsers.Grammars.Generated;
namespace DaxStudio.Parsers.CommentScript
{
    /// <summary>
    /// A "--&gt; SET &lt;name&gt; = &lt;value&gt;" comment-script command that defines a script
    /// variable. <see cref="RawValue"/> holds the value exactly as written (it may still contain
    /// <c>$(...)</c> references); the value is expanded eagerly at run time by
    /// <see cref="ScriptVariableExpander"/> so that captured built-ins (e.g. <c>$(now:...)</c>) are
    /// frozen at the point the SET executes.
    /// </summary>
    public class VariableCommand : ScriptCommand
    {
        public VariableCommand(string name, string rawValue)
        {
            Name = name;
            RawValue = rawValue;
        }

        public string Name { get; }

        public string RawValue { get; }
    }
}
