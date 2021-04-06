using ADOTabular.AdomdClientWrappers;
using DaxStudio.Interfaces;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace DaxStudio.UI.Interfaces
{
    public enum OutputTarget
    {
        Grid,
        Timer,
        Linked,
        Static,
        File,
        Clipboard
    }
    public interface IQueryRunner
    {
        string QueryText { get; }
        Task<DataTable> ExecuteDataTableQueryAsync(string daxQuery);
        AdomdDataReader ExecuteDataReaderQuery(string daxQuery, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList);
        DataTable ResultsTable { get; set; }
        DataSet ResultsDataSet { get; set; }
        void OutputMessage(string message);
        void OutputMessage(string message, double duration);
        void OutputWarning(string warning);
        void OutputError(string errorMessage);
        void OutputError(string errorMessage, double duration);
        void ActivateResults();
        void ActivateOutput();
        //bool IsOutputActive { get; }
        void QueryCompleted();
        void QueryCompleted(bool isCancelled);
        IDaxStudioHost Host { get; }
        string SelectedWorksheet { get; set; }
        string ConnectionStringWithInitialCatalog { get; }
        bool ConnectedToPowerPivot { get; }

        void SetResultsMessage(string message, OutputTarget icon);
        IStatusBarMessage NewStatusBarMessage(string message);
        int RowCount { get; set; }

        IGlobalOptions Options { get; }
        //ADOTabular.ADOTabularConnection Connection { get; }
    }
}
