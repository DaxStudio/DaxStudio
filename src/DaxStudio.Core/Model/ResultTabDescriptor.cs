using System.Collections.Generic;
using System.Data;
using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.Core.Model
{
    /// <summary>
    /// A WPF-free description of a single tab to display in the query Results pane. A tab is either
    /// a query-result data grid (<see cref="Table"/>) or the tree-grid produced by a Comment Script
    /// <c>--&gt; SHOW</c> command (<see cref="ShowTreeRoots"/> + <see cref="ShowType"/>). The results
    /// pipeline builds an ordered list of these (interspersed in batch execution order) and hands it
    /// to the runner so the UI can materialise the heterogeneous tab collection.
    /// </summary>
    public class ResultTabDescriptor
    {
        /// <summary>True when this tab is a SHOW tree-grid; false when it is a query-result data grid.</summary>
        public bool IsShowTree { get; set; }

        /// <summary>The query-result table for a data-grid tab (null for a SHOW tab).</summary>
        public DataTable Table { get; set; }

        /// <summary>The root nodes for a SHOW tree-grid tab (null for a data-grid tab).</summary>
        public IList<ShowTreeNode> ShowTreeRoots { get; set; }

        /// <summary>The SHOW command variant for a SHOW tab (ignored for a data-grid tab).</summary>
        public ShowType ShowType { get; set; }

        /// <summary>Creates a descriptor for a query-result data-grid tab.</summary>
        public static ResultTabDescriptor ForTable(DataTable table)
            => new ResultTabDescriptor { IsShowTree = false, Table = table };

        /// <summary>Creates a descriptor for a Comment Script <c>--&gt; SHOW</c> tree-grid tab.</summary>
        public static ResultTabDescriptor ForShowTree(IList<ShowTreeNode> roots, ShowType showType)
            => new ResultTabDescriptor { IsShowTree = true, ShowTreeRoots = roots, ShowType = showType };
    }
}
