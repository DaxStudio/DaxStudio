using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using Serilog;

namespace DaxStudio.UI.Model
{
    // This Results Target discards the returned dataset
    // this is primarily aimed at scenarios like performance tuning
    // where you are interested in the speed of the raw query and
    // do not want to be influenced by the time taken to render
    // the results.
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetTimer :  IResultsTarget
    {
        #region Standard Properties
        public string Name => "Timer";
        public string Group => "Standard";
        public bool IsDefault => false;
        public bool IsAvailable => true;
        public int DisplayOrder => 20;
        public string Message => "Query timings sent to output tab";
        public OutputTarget Icon => OutputTarget.Timer;
        public string Tooltip => "Runs the query and discards the results recording the time taken";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider)
        {
            try
            {
                runner.OutputMessage("Query Started");
                var sw = Stopwatch.StartNew();

                var dq = textProvider.QueryText;
                var res = await runner.ExecuteDataTableQueryAsync(dq);

                sw.Stop();
                var durationMs = sw.ElapsedMilliseconds;
                runner.OutputMessage(string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count, res.Rows.Count == 1 ? "" : "s"), durationMs);
                runner.RowCount = res.Rows.Count;
                runner.SetResultsMessage("Query timings sent to output tab", OutputTarget.Timer);
                //runner.QueryCompleted();
                runner.ActivateOutput();
            }
            catch (Exception ex)
            {
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ResultsTargetTimer), nameof(OutputResultsAsync), ex.Message);
                runner.ActivateOutput();
                runner.OutputError(ex.Message);
            }
            finally
            {
                runner.QueryCompleted();
            }
        }

    }
}
