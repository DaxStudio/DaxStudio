using System.Collections.Generic;

namespace DaxStudio.UI.Events
{
    /// <summary>
    /// Event raised to request the Model Diagram to show only the specified tables.
    /// Used by Server Timings and Storage Engine Dependencies views to highlight 
    /// query-dependent tables in the model diagram context.
    /// </summary>
    public class ShowTablesInModelDiagramEvent
    {
        public ShowTablesInModelDiagramEvent(IEnumerable<string> tableNames, bool includeRelated = true)
        {
            TableNames = tableNames;
            IncludeRelated = includeRelated;
        }

        /// <summary>
        /// The table names to show in the model diagram.
        /// </summary>
        public IEnumerable<string> TableNames { get; }

        /// <summary>
        /// Whether to also show tables connected via relationships to form a complete subgraph.
        /// </summary>
        public bool IncludeRelated { get; }
    }
}
