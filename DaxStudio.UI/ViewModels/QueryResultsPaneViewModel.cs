using System.ComponentModel.Composition;
using System.Data;
using DaxStudio.UI.Model;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using System.Windows.Input;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(IToolWindow))]
    public class QueryResultsPaneViewModel: ToolWindowBase
    {
        private DataTable _resultsTable;

        [ImportingConstructor]
        public QueryResultsPaneViewModel() : this(new DataTable("Empty"))
        {}

        public QueryResultsPaneViewModel(DataTable resultsTable)
        {
            _resultsTable = resultsTable;
        }

        public override string Title
        {
            get { return "Results"; }
        }

        public DataTable ResultsDataTable
        {
            get { return _resultsTable; }
            set { _resultsTable = value;
            NotifyOfPropertyChange(()=> ResultsDataView);}
        }

        public void CopyAllResultsToClipboard(object obj)
        {
            System.Diagnostics.Debug.WriteLine(obj);
            Clipboard.SetData("CommaSeparatedValue", ResultsDataTable.ToCsv());
        }

        public DataView ResultsDataView
        { get { return _resultsTable==null?new DataTable("blank").AsDataView():  _resultsTable.AsDataView(); } }

        public void OnListViewItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("in OnListViewItemPreviewMouseRightButtonDown");
        }

        private void ResultsAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            
            if ((e.PropertyName.Contains(".")
                || e.PropertyName.Contains("/")
                || e.PropertyName.Contains("(")
                || e.PropertyName.Contains(")")
                || e.PropertyName.Contains("[")
                || e.PropertyName.Contains("]")
                ) && e.Column is DataGridBoundColumn)
            {
                DataGridBoundColumn dataGridBoundColumn = e.Column as DataGridBoundColumn;
                dataGridBoundColumn.Binding = new Binding("[" + e.PropertyName + "]");
            }
        }

    }
}
