using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.ViewModels;
using System.Diagnostics;

namespace DaxStudio.UI.Model
{
    // This Results Target discards the returned dataset
    // this is primarily aimed at scenarios like performance tuning
    // where you are interested in the speed of the raw query and
    // do not want to be influenced by the time taken to render
    // the results.
    [Export(typeof(IResultsTarget))]
    public class ResultTargetTimer :  IResultsTarget
    {
        public string Name {get { return "Timer"; } }
        public string Group {get { return "Standard"; } }
        
        private void OutputResults(IQueryRunner runner)
        {
            try
            {
                runner.OutputMessage("Query Started");
                var sw = Stopwatch.StartNew();
                var dq = runner.QueryText;
                var res = runner.ExecuteQuery(dq);
                sw.Stop();
                var durationMs = sw.ElapsedMilliseconds;
                runner.OutputMessage(string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count, res.Rows.Count == 1 ? "" : "s"), durationMs);
                runner.SetResultsMessage("Query timings sent to output tab", OutputTargets.Timer);
                runner.QueryCompleted();
                runner.ActivateOutput();
            }
            catch (Exception ex)
            {
                runner.ActivateOutput();
                runner.OutputError(ex.Message);
            }
        }

        public Task OutputResultsAsync(IQueryRunner runner)
        {
            return Task.Factory.StartNew(() => OutputResults(runner));
        }


        public bool IsDefault
        {
            get { return false; }
        }

        public bool IsEnabled
        {
            get { return true; }
        }
        public int DisplayOrder
        {
            get { return 20; }
        }
    }
}
