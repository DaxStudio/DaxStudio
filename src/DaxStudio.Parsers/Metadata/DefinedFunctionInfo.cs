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

        /// <summary>The function definition text from the parameter list onwards (everything after the
        /// first <c>=</c>), e.g. <c>(param1) =&gt; param1 + 10</c>, preserving the original source text, or
        /// an empty string when it could not be captured (e.g. during error recovery).</summary>
        public string Expression { get; }

        /// <summary>The column / measure / (non-built-in) function references found in the function body,
        /// so the dependency tree can be extended with the objects the function depends on.</summary>
        public IReadOnlyList<DaxObjectReference> References { get; }

        public DefinedFunctionInfo(string name, IReadOnlyList<DefinedFunctionParameter> parameters, string expression = null, IReadOnlyList<DaxObjectReference> references = null)
        {
            Name = name;
            Parameters = parameters ?? new List<DefinedFunctionParameter>();
            Expression = expression ?? string.Empty;
            References = references ?? new List<DaxObjectReference>();
        }
    }
}
