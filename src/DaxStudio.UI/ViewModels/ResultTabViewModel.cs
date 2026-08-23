using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Controls;
using Caliburn.Micro;
using DaxStudio.Core.Model;
using DaxStudio.Parsers.CommentScript;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// Base type for a single tab in the query Results <c>TabControl</c>. The collection is
    /// heterogeneous: a tab is either a query-result data grid (<see cref="DataTableResultTab"/>)
    /// or a Comment Script <c>--&gt; SHOW</c> tree-grid (<see cref="ShowTreeResultTab"/>). The view
    /// selects the content template from the <see cref="IsShowTree"/> discriminator.
    /// </summary>
    public abstract class ResultTabViewModel : PropertyChangedBase
    {
        /// <summary>The text shown on the tab header.</summary>
        public abstract string Title { get; }

        /// <summary>True when this tab hosts a SHOW tree-grid; false for a query-result data grid.</summary>
        public abstract bool IsShowTree { get; }
    }

    /// <summary>
    /// A tab wrapping a single query-result <see cref="System.Data.DataTable"/>. Exposes the same
    /// <c>DefaultView</c> / <c>TableName</c> members the results <c>DataGrid</c> previously bound to
    /// when the tab's DataContext was the raw <see cref="System.Data.DataTable"/>.
    /// </summary>
    public class DataTableResultTab : ResultTabViewModel
    {
        public DataTableResultTab(DataTable table)
        {
            Table = table;
        }

        /// <summary>The underlying result table.</summary>
        public DataTable Table { get; }

        /// <summary>The default view the results <c>DataGrid</c> binds its ItemsSource / columns to.</summary>
        public DataView DefaultView => Table.DefaultView;

        /// <summary>The result table name (used as the tab header and for column generation).</summary>
        public string TableName => Table.TableName;

        public override string Title => Table.TableName;

        public override bool IsShowTree => false;

        /// <summary>Row count of the underlying table (used to update the document row-count status).</summary>
        public int RowCount => Table.Rows.Count;
    }

    /// <summary>
    /// A tab wrapping the tree-grid produced by a Comment Script <c>--&gt; SHOW</c> command. Carries
    /// the root nodes plus the display metadata (title and which optional columns are relevant) that
    /// used to live on the results pane view-model when SHOW was rendered as a full-pane overlay.
    /// </summary>
    public class ShowTreeResultTab : ResultTabViewModel
    {
        public ShowTreeResultTab(IList<ShowTreeNode> roots, ShowType showType)
        {
            ShowType = showType;
            if (roots != null) ShowTreeRoots.AddRange(roots);

            switch (showType)
            {
                case ShowType.LastUpdated:
                    _title = "Last Updated";
                    ShowTreeTimestampColumn = true;
                    ShowTreeExtraColumns = true;
                    ShowTreeExpressionColumn = false;
                    break;
                case ShowType.MaxUpdated:
                    _title = "Most Recently Updated";
                    ShowTreeTimestampColumn = true;
                    ShowTreeExtraColumns = false;
                    ShowTreeExpressionColumn = false;
                    break;
                case ShowType.Dependencies:
                default:
                    _title = "Dependencies";
                    ShowTreeTimestampColumn = false;
                    ShowTreeExtraColumns = false;
                    ShowTreeExpressionColumn = true;
                    break;
            }
        }

        /// <summary>Root nodes bound as the tree-grid RootItems.</summary>
        public BindableCollection<ShowTreeNode> ShowTreeRoots { get; } = new BindableCollection<ShowTreeNode>();

        /// <summary>The SHOW command variant this tab was produced from.</summary>
        public ShowType ShowType { get; }

        private readonly string _title;
        public override string Title => _title;

        public override bool IsShowTree => true;

        /// <summary>The Last Modified column is only relevant for the LAST_UPDATED / MAX_UPDATED variants.</summary>
        public bool ShowTreeTimestampColumn { get; }

        /// <summary>The Max Update / Days Since Change columns are only shown for the LAST_UPDATED variant.</summary>
        public bool ShowTreeExtraColumns { get; }

        /// <summary>The Expression column (measure / function body) is only shown for the DEPENDENCIES variant.</summary>
        public bool ShowTreeExpressionColumn { get; }

        private bool _showTreeLines = true;
        /// <summary>
        /// Whether the tree connector lines (and node expanders) are drawn. They are only meaningful while the
        /// grid preserves the hierarchy - i.e. when sorted by the Object column - so they are switched off when
        /// the user sorts by any other column (which produces a flat list).
        /// </summary>
        public bool ShowTreeLines
        {
            get => _showTreeLines;
            set { _showTreeLines = value; NotifyOfPropertyChange(() => ShowTreeLines); }
        }

        /// <summary>
        /// Column-header sort handler for the SHOW tree-grid. Sorting by the Object (tree) column re-orders the
        /// nodes recursively so the hierarchy is preserved and the tree lines stay meaningful; sorting by any
        /// other column falls back to the grid's default flat sort with the tree lines switched off.
        /// </summary>
        public void OnSorting(object source, DataGridSortingEventArgs e)
        {
            var isObjectColumn = e.Column is DaxStudio.Controls.TreeColumn;

            if (!isObjectColumn)
            {
                // Let the base DataGrid perform its flat sort; the hierarchy is no longer represented so hide
                // the connector lines / expanders.
                ShowTreeLines = false;
                return;
            }

            // Recursive, hierarchy-preserving sort of the tree nodes - suppress the flat sort the grid would do.
            e.Handled = true;

            var descending = e.Column.SortDirection == System.ComponentModel.ListSortDirection.Ascending;
            if (source is DataGrid grid)
            {
                // Remove any flat sort left over from a previous non-Object column click. The TreeGrid is a
                // plain DataGrid that flat-sorts its flattened rows via Items.SortDescriptions; if that stale
                // sort is not cleared it is re-applied when we rebuild the roots below, re-flattening the
                // hierarchy back into the previous column's order (so the tree never returns to its shape).
                grid.Items.SortDescriptions.Clear();
                foreach (var column in grid.Columns)
                {
                    if (column != e.Column) column.SortDirection = null;
                }
            }
            e.Column.SortDirection = descending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;

            SortNodesRecursive(ShowTreeRoots, descending);
            ShowTreeLines = true;
            RefreshRoots();
        }

        /// <summary>Stable recursive sort of the tree nodes by <see cref="ShowTreeNode.Name"/> at every level.</summary>
        internal static void SortNodesRecursive(IList<ShowTreeNode> nodes, bool descending)
        {
            if (nodes == null || nodes.Count == 0) return;

            var ordered = descending
                ? nodes.OrderByDescending(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList()
                : nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();

            if (nodes is List<ShowTreeNode> list)
            {
                list.Clear();
                list.AddRange(ordered);
            }
            else
            {
                nodes.Clear();
                foreach (var n in ordered) nodes.Add(n);
            }

            foreach (var node in ordered) SortNodesRecursive(node.Children, descending);
        }

        /// <summary>
        /// Resets <see cref="ShowTreeRoots"/> in place so the tree grid re-reads the re-ordered node hierarchy. A
        /// single Reset notification fires while the collection already holds the (unchanged) roots, letting the
        /// grid rebuild the tree while preserving each node's expanded state by identity.
        /// </summary>
        private void RefreshRoots()
        {
            var items = ShowTreeRoots.ToList();
            var wasNotifying = ShowTreeRoots.IsNotifying;
            ShowTreeRoots.IsNotifying = false;
            try
            {
                ShowTreeRoots.Clear();
                foreach (var item in items) ShowTreeRoots.Add(item);
            }
            finally
            {
                ShowTreeRoots.IsNotifying = wasNotifying;
            }
            ShowTreeRoots.Refresh();
        }
    }
}
