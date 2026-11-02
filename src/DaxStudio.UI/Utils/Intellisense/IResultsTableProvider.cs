using System.Data;

namespace DaxStudio.UI.Utils.Intellisense
{
    /// <summary>
    /// Implemented by a document that can surface the <see cref="DataTable"/> of its current query
    /// results, so the intellisense layer can build a <c>--&gt; ASSERT TABLE</c> block from the live
    /// results without taking a dependency on the full document view-model.
    /// </summary>
    public interface IResultsTableProvider
    {
        /// <summary>The table backing the currently selected result tab, or null when there are none.</summary>
        DataTable GetActiveResultsTable();
    }
}
