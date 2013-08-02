using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.UI.Model;

namespace DaxStudio.ResultTargets
{
    // This is the default target which writes the results out to
    // the built-in grid
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelStatic: IResultsTarget
    {
        public string Name {get { return "Static"; }
        }
        public string Group {get { return "Excel"; }
        }

        public void OutputResults(IQueryRunner runner)
        {
            
            try
            {
                runner.OutputMessage("Query Started");
                var start = DateTime.Now;
                
                var dq = runner.QueryText;
                runner.ExecuteQueryAsync(dq).ContinueWith((antecendant) => 
                    {
                        var end = DateTime.Now;
                        var durationMs = (end - start).TotalMilliseconds;
                        var res = antecendant.Result;
                        // write results to Excel
                        runner.ResultsTable = res;
                        
                        runner.OutputMessage(
                            string.Format("Query Completed ({0} row{1} returned)", res.Rows.Count,
                                          res.Rows.Count == 1 ? "" : "s"), durationMs);
                        runner.ActivateResults();
                        runner.QueryCompleted();
                    },TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                runner.ActivateOutput();
                runner.OutputError(ex.Message);
            }
        }

    }


}
