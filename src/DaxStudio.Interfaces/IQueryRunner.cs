using ADOTabular.AdomdClientWrappers;
using System.Data;
using System.Threading.Tasks;

namespace DaxStudio.Interfaces
{
    public enum OutputTargets
    {
        Grid,
        Timer,
        Linked,
        Static,
        File
    }
    public interface IQueryRunner
    {
        string QueryText { get; }
        DataTable ExecuteDataTableQuery(string daxQuery);
        AdomdDataReader ExecuteDataReaderQuery(string daxQuery);
        Task<DataTable> ExecuteQueryAsync(string daxQuery);
        DataTable ResultsTable { get; set; }
        DataSet ResultsDataSet { get; set; }
        void OutputMessage(string message);
        void OutputMessage(string message, double duration);
        void OutputWarning(string warning);
        void OutputError(string error);
        void OutputError(string error, double duration);
        void ActivateResults();
        void ActivateOutput();
        //bool IsOutputActive { get; }
        void QueryCompleted();
        void QueryCompleted(bool isCancelled);
        IDaxStudioHost Host { get; }
        string SelectedWorksheet { get; set; }
        string ConnectionStringWithInitialCatalog { get; }
        bool ConnectedToPowerPivot { get; }

        void SetResultsMessage(string message, OutputTargets icon);
        IStatusBarMessage NewStatusBarMessage(string message);
        int RowCount { get; set; }
    }
}
