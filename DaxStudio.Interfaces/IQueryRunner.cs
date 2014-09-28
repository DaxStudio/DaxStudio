using System.Data;
using System.Threading.Tasks;

namespace DaxStudio.Interfaces
{
    public interface IQueryRunner
    {
        string QueryText { get; }
        DataTable ExecuteQuery(string daxQuery);
        Task<DataTable> ExecuteQueryAsync(string daxQuery);
        DataTable ResultsTable { get; set; }
        void OutputMessage(string message);
        void OutputMessage(string message, double duration);
        void OutputWarning(string warning);
        void OutputError(string error);
        void ActivateResults();
        void ActivateOutput();
        void QueryCompleted();
        IDaxStudioHost Host { get; }
        string SelectedWorksheet { get; set; }
        string ConnectionString { get; }
        bool ConnectedToPowerPivot { get; }

        IStatusBarMessage NewStatusBarMessage(string message);
    }
}
