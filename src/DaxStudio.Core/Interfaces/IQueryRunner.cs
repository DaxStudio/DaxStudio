using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
using DaxStudio.Core.Events;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace DaxStudio.Core.Interfaces
{
    public interface IQueryRunner
    {
        string QueryText { get; }
        Task<DataTable> ExecuteDataTableQueryAsync(string daxQuery);
        AdomdDataReader ExecuteDataReaderQuery(string daxQuery, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList);
        DataSet ResultsDataSet { get; set; }
        void OutputMessage(string message);
        void OutputMessage(string message, double duration);
        void OutputMessage(OutputMessage message);
        void OutputWarning(string warning);
        void OutputError(string errorMessage);
        void OutputError(string errorMessage, double duration);
        void ActivateResults();
        void ActivateOutput();
        //bool IsOutputActive { get; }
        void QueryCompleted();
        void QueryCompleted(bool isCancelled);
        void QueryFailed(string errorMessage);
        IDaxStudioHost Host { get; }
        string SelectedWorksheet { get; set; }
        string ConnectionStringWithInitialCatalog { get; }
        bool ConnectedToPowerPivot { get; }

        void SetResultsMessage(string message, OutputTarget icon);
        void SetResultsMessage(string message, OutputTarget icon, string fileName);
        IStatusBarMessage NewStatusBarMessage(string message);
        int RowCount { get; set; }

        IGlobalOptions Options { get; }
        //ADOTabular.ADOTabularConnection Connection { get; }
        DaxStudio.Core.Connections.ConnectionManager Connection { get; }
        void OutputQueryError(string errorMessage);
        void ClearQueryError();
        void ClearQueryResults();

        /// <summary>
        /// Populates the Results pane with an ordered, interspersed set of tabs - a mix of query-result
        /// data grids and the tree-grids produced by Comment Script <c>--&gt; SHOW</c> commands
        /// (DEPENDENCIES / LAST_UPDATED / MAX_UPDATED) - preserving batch execution order.
        /// </summary>
        void SetResultTabs(IList<DaxStudio.Core.Model.ResultTabDescriptor> tabs);
    }
}
