using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Model
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
    }
}
