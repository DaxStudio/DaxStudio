using System.Windows.Controls;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for DeltaAnalyzerView.xaml
    /// </summary>
    public partial class DeltaAnalyzerView : UserControl
    {
        public DeltaAnalyzerView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles column-header sorting for the results tree. The built-in DataGrid sort would flatten the
        /// hierarchy, so it is suppressed here and replaced with a two-level sort (tables, then columns within
        /// each table) performed by the ViewModel.
        /// </summary>
        private void ResultsTree_Sorting(object sender, System.Windows.Controls.DataGridSortingEventArgs e)
        {
            e.Handled = true;

            var path = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(path)) return;

            // Toggle the sort direction for the clicked column and clear the indicator on the others.
            var direction = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending;

            if (sender is DataGrid grid)
            {
                foreach (var column in grid.Columns)
                {
                    if (column != e.Column) column.SortDirection = null;
                }
            }
            e.Column.SortDirection = direction;

            var propertyName = path.StartsWith("Data.") ? path.Substring("Data.".Length) : path;
            (DataContext as ViewModels.DeltaAnalyzerViewModel)?.SortTree(
                propertyName, direction == System.ComponentModel.ListSortDirection.Descending);
        }
    }
}
