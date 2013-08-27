using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Caliburn.Micro;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.Model
{
    // This is the default target which writes the results out to
    // the built-in grid
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetGrid: IResultsTarget 
    {
        public string Name {get { return "Grid"; }
        }
        public string Group {get { return "Standard"; }
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
                        using (new StatusBarMessage("Executing Query..."))
                        {
                            var end = DateTime.Now;
                            var durationMs = (end - start).TotalMilliseconds;
                            var res = antecendant.Result;
                            runner.ResultsTable = res;

                            runner.OutputMessage(
                                string.Format("Query Completed ({0} row{1} returned)", res.Rows.Count,
                                              res.Rows.Count == 1 ? "" : "s"), durationMs);
                            runner.ActivateResults();
                            runner.QueryCompleted();
                        }
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
