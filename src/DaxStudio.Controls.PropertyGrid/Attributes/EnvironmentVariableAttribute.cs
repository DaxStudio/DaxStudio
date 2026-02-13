using System;

namespace DaxStudio.Controls.PropertyGrid
{
    /// <summary>
    /// Specifies that a property should only be displayed if the specified environment variable exists and has a non-zero value.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EnvironmentVariableAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the EnvironmentVariableAttribute class with the specified environment variable name.
        /// </summary>
        /// <param name="variableName">The name of the environment variable to check</param>
        public EnvironmentVariableAttribute(string variableName)
        {
            VariableName = variableName;
        }

        /// <summary>
        /// Gets the name of the environment variable to check
        /// </summary>
        public string VariableName { get; }
    }
}
