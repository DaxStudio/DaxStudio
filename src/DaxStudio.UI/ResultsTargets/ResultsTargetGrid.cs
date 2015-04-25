using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using System.Diagnostics;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Model
{
    // This is the default target which writes the results out to
    // the built-in grid
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetGrid: IResultsTarget 
    {
        private IEventAggregator _eventAggregator;

        [ImportingConstructor]
        public ResultsTargetGrid(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }
        public string Name {get { return "Grid"; }
        }
        public string Group {get { return "Standard"; }
        }

        /*
        public void OutputResults(IQueryRunner runner)
        {
            
            try
            {
                runner.OutputMessage("Query Started");
                var sw = Stopwatch.StartNew();
                
                var dq = runner.QueryText;
                runner.ExecuteQueryAsync(dq).ContinueWith((antecendant) => 
                    {
                        using (runner.NewStatusBarMessage("Executing Query..."))
                        {
                            sw.Stop();
                            var durationMs = sw.ElapsedMilliseconds;
                            var res = antecendant.Result;
                            runner.ResultsTable = res;

                            runner.OutputMessage(
                                string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
                                              res.Rows.Count == 1 ? "" : "s"), durationMs);
                            // activate the result only when Counters are not selected...
                            runner.ActivateResults();
                            runner.QueryCompleted();
                        }
                    }); 
            }
            catch (Exception ex)
            {
                runner.ActivateOutput();
                runner.OutputError(ex.Message);
            }
        }
         */ 
        public int DisplayOrder
        {
            get { return 10; }
        }

        public Task OutputResultsAsync(IQueryRunner runner)
        {
            return Task.Factory.StartNew(() =>
                {
                    try
                    {
                        runner.OutputMessage("Query Started");
                        var sw = Stopwatch.StartNew();

                        var dq = runner.QueryText;
                        var res = runner.ExecuteQuery(dq);
                        if (res != null)
                        {
                            sw.Stop();
                            var durationMs =sw.ElapsedMilliseconds;
                            runner.ResultsTable = res;
                            runner.OutputMessage(
                                string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
                                                res.Rows.Count == 1 ? "" : "s"), durationMs);
                            // activate the result only when Counters are not selected...
                            runner.ActivateResults();
                            //runner.QueryCompleted();
                        }
                        
                    }
                    catch (Exception ex)
                    {
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
                    }
                });
        }




        public bool IsDefault
        {
            get { return true; }
        }

        public bool IsEnabled
        {
            get { return true; }
        }


        public string Message
        {
            get { return string.Empty;}
        }
        public OutputTargets Icon
        {
            get { return OutputTargets.Grid; }
        }
    }


}
