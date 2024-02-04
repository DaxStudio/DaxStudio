using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using Serilog;
using Caliburn.Micro;

namespace DaxStudio.UI.ResultsTargets
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
        public string Name => "Log Timer";
        public string Group => "Standard";
        public bool IsDefault => false;
        public bool IsAvailable => true;
        public int DisplayOrder => 20;
        public string Message => "Query timings sent to Log tab";
        public OutputTarget Icon => OutputTarget.Timer;
        public string ImageResource => "results_timerDrawingImage";
        public string Tooltip => "Runs the query and discards the results recording the time taken";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string filename)
        {

            await Task.Run(() => { 
                var sw = Stopwatch.StartNew();

                var dq = textProvider.QueryText;

                var rowCnt = 0;
                using (var rdr = runner.ExecuteDataReaderQuery(dq, textProvider.ParameterCollection))
                {
                    if (rdr != null)
                    {
                        while (rdr.Read())
                        {
                            rowCnt++;
                        }
                    }
                }

                sw.Stop();
                var durationMs = sw.ElapsedMilliseconds;
                runner.OutputMessage(string.Format("Query Completed ({0:N0} row{1} returned)", rowCnt, rowCnt == 1 ? "" : "s"), durationMs);
                runner.RowCount = rowCnt;
                runner.SetResultsMessage("Query timings sent to Log tab", OutputTarget.Timer);
                runner.ActivateOutput();

            });
        }

    }
}
