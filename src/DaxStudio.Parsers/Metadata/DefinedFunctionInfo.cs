using System.Collections.Generic;

namespace DaxStudio.Parsers.Metadata
{
    /// <summary>
    /// A single parameter of a function defined in the query itself via DEFINE FUNCTION.
    /// </summary>
    public class DefinedFunctionParameter
    {
        public string Name { get; }

        /// <summary>The (optional) type annotation text, or an empty string when none was specified.</summary>
        public string TypeAnnotation { get; }

        public DefinedFunctionParameter(string name, string typeAnnotation)
        {
            Name = name;
            TypeAnnotation = typeAnnotation ?? string.Empty;
        }
    }

    /// <summary>
    /// A user-defined function declared in the current query via DEFINE FUNCTION, including its
    /// parameter names. Used to provide signature/insight help for functions that are not part of
    /// the connected model's metadata.
    /// </summary>
    public class DefinedFunctionInfo
    {
        public string Name { get; }
        public IReadOnlyList<DefinedFunctionParameter> Parameters { get; }

        public DefinedFunctionInfo(string name, IReadOnlyList<DefinedFunctionParameter> parameters)
        {
            Name = name;
            Parameters = parameters ?? new List<DefinedFunctionParameter>();
        }
    }
}
