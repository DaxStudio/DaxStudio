using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
{
    /// <summary>
    /// Options for what statistic to display on columns in the Model Diagram
    /// after VPA (Vertipaq Analyzer) data becomes available.
    /// </summary>
    public enum DiagramColumnStatDisplay
    {
        [Description("None")]
        None,
        
        [Description("Cardinality")]
        Cardinality,
        
        [Description("Size")]
        Size
    }
}
