using System.ComponentModel.Composition;
using System.Data;
using DaxStudio.UI.Model;

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

        public DataView ResultsDataView
        { get { return _resultsTable==null?new DataTable("blank").AsDataView():  _resultsTable.AsDataView(); } }
    }
}
