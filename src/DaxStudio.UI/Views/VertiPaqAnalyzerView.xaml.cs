using DaxStudio.UI.Controls;
using System.Windows.Controls;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for VertiPaqAnalyzerView.xaml
    /// </summary>
    public partial class VertiPaqAnalyzerView : ZoomableUserControl
    {
        public VertiPaqAnalyzerView()
        {
            InitializeComponent();
        }

        private void OnSorting(object sender, DataGridSortingEventArgs e)
        {
            DataGrid dg = sender as DataGrid;
            dg.Items.SortDescriptions.Clear();
            var sortDir = e.Column.SortDirection?? System.ComponentModel.ListSortDirection.Descending;
            sortDir = sortDir == System.ComponentModel.ListSortDirection.Descending?System.ComponentModel.ListSortDirection.Ascending:System.ComponentModel.ListSortDirection.Descending;
            dg.Items.SortDescriptions.Add(new System.ComponentModel.SortDescription(e.Column.SortMemberPath, sortDir));
            e.Column.SortDirection = sortDir;
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine("Sorting");
        }
    }
}
