using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
{
    /// <summary>
    /// Algorithm options for laying out tables in the Model Diagram.
    /// </summary>
    public enum DiagramLayoutAlgorithm
    {
        [Description("Auto")]
        Auto,
        
        [Description("Hierarchy")]
        Hierarchy,
        
        [Description("Grid")]
        Grid,
        
        [Description("Clustered")]
        Clustered,

        [Description("Force Directed")]
        ForceDirected
    }
}
