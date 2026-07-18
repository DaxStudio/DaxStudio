using System.Collections.Generic;
using System.Data;
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
                    break;
                case ShowType.MaxUpdated:
                    _title = "Most Recently Updated";
                    ShowTreeTimestampColumn = true;
                    ShowTreeExtraColumns = false;
                    break;
                case ShowType.Dependencies:
                default:
                    _title = "Dependencies";
                    ShowTreeTimestampColumn = false;
                    ShowTreeExtraColumns = false;
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
    }
}
