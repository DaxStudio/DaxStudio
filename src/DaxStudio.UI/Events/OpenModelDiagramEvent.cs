using System.Collections.Generic;

namespace DaxStudio.UI.Events
{
    /// <summary>
    /// Event raised to open (and optionally filter) the Model Diagram tool window.
    /// Used by the "--> SHOW DIAGRAM" comment-script command: when <see cref="TableNames"/> is
    /// non-null the diagram is filtered to just those tables (reusing the same table-subset
    /// mechanism as Server Timings); when null the full diagram is shown.
    /// </summary>
    public class OpenModelDiagramEvent
    {
        public OpenModelDiagramEvent(IEnumerable<string> tableNames = null)
        {
            TableNames = tableNames;
        }

        /// <summary>
        /// The tables to filter the diagram to, or null to show the whole model.
        /// </summary>
        public IEnumerable<string> TableNames { get; }
    }
}
