using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;

namespace DaxStudio.ResultTargets
{
    // This is the target which writes the results out to
    // the a linked table in Excel (so that it can be refreshed without using Dax Studio)
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelLinked: IResultsTarget
    {
        public string Name {get { return "Linked"; }
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

                        // TODO write results to Excel
                        runner.Host.OutputQueryTableResult(runner.ConnectionString,runner.QueryText,runner.SelectedWorksheet,runner );
                        
                        //runner.ResultsTable = res;
                        
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

        public Task OutputResultsAsync(IQueryRunner runner)
        {
            return Task.Factory.StartNew(() =>
                {
                    try
                    {
                        runner.OutputMessage("Query Started");
                        var start = DateTime.Now;

                        var dq = runner.QueryText;
                        var res = runner.ExecuteQuery(dq);

                        var end = DateTime.Now;
                        var durationMs = (end - start).TotalMilliseconds;


                        // TODO write results to Excel
                        runner.Host.OutputQueryTableResult(runner.ConnectionString, runner.QueryText, runner.SelectedWorksheet, runner);
                        //runner.ResultsTable = res;

                        runner.OutputMessage(
                            string.Format("Query Completed ({0} row{1} returned)", res.Rows.Count,
                                          res.Rows.Count == 1 ? "" : "s"), durationMs);
                        runner.ActivateResults();
                        runner.QueryCompleted();

                    }
                    catch (Exception ex)
                    {
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
                    }
                });
        }
    }


}
