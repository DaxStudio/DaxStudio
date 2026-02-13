using System.ComponentModel;

namespace DaxStudio.Interfaces.Enums
{
    /// <summary>
    /// Options for sorting columns in the Model Diagram tables.
    /// Sort by stat options require VPA (Vertipaq Analyzer) data.
    /// </summary>
    public enum DiagramColumnSortOrder
    {
        [Description("Name")]
        Name,
        
        [Description("Cardinality ↓")]
        CardinalityDesc,
        
        [Description("Size ↓")]
        SizeDesc
    }
}
