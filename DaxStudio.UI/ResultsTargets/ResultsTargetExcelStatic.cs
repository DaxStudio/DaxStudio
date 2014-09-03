using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Events;
using System.Diagnostics;

namespace DaxStudio.UI.Model
{
    // This is the target which writes the static results out to
    // a range in Excel
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelStatic: IResultsTarget
    {
        private IDaxStudioHost _host;
        [ImportingConstructor]
        public ResultsTargetExcelStatic(IDaxStudioHost host)
        {
            _host = host;
        }
        public string Name {get { return "Static"; }
        }
        public string Group {get { return "Excel"; }
        }

        public void OutputResults(IQueryRunner runner)
        {

            Task.Factory.StartNew(() =>
            {
                try
                {
                    runner.OutputMessage("Query Started");
                    var sw = Stopwatch.StartNew();

                    var dq = runner.QueryText;
                    var res = runner.ExecuteQuery(dq);

                    using (new StatusBarMessage("Executing Query..."))
                    {
                        sw.Stop();
                        var durationMs = sw.ElapsedMilliseconds;

                        runner.Host.Proxy.OutputStaticResultAsync(res, runner.SelectedWorksheet).ContinueWith((ascendant) =>
                        {
                            //runner.ResultsTable = res;

                            runner.OutputMessage(
                                string.Format("Query Completed ({0} row{1} returned)", res.Rows.Count,
                                              res.Rows.Count == 1 ? "" : "s"), durationMs);
                            runner.ActivateOutput();
                            runner.QueryCompleted();
                        });

                        
                    }

                }
                catch (Exception ex)
                {
                    runner.ActivateOutput();
                    runner.OutputError(ex.Message);
                }
            });
            /*

            try
            {
                runner.OutputMessage("Query Started");
                var start = DateTime.Now;
                
                var dq = runner.QueryText;
                runner.ExecuteQueryAsync(dq).ContinueWith((antecendant) => 
                    {
                        var end = DateTime.Now;
                        var durationMs = (end - start).TotalMilliseconds;
                        var res = antecendant;

                        // TODO write results to Excel
                        await runner.Host.Proxy.OutputStaticResultAsync(res, runner.SelectedWorksheet).ContinueWith(() =>
                        {
                            //runner.ResultsTable = res;

                            runner.OutputMessage(
                                string.Format("Query Completed ({0} row{1} returned)", res.Rows.Count,
                                              res.Rows.Count == 1 ? "" : "s"), durationMs);
                            runner.ActivateResults();
                            runner.QueryCompleted();
                        });
                    },TaskScheduler.FromCurrentSynchronizationContext());
            }
            catch (Exception ex)
            {
                runner.ActivateOutput();
                runner.OutputError(ex.Message);
            }
            */
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

                        sw.Stop();
                        var durationMs = sw.ElapsedMilliseconds;


                        // TODO write results to Excel
                        runner.Host.Proxy.OutputStaticResultAsync(res, runner.SelectedWorksheet).ContinueWith((ascendant) => {
                            //runner.ResultsTable = res;

                            runner.OutputMessage(
                                string.Format("Query Completed ({0} row{1} returned)", res.Rows.Count,
                                              res.Rows.Count == 1 ? "" : "s"), durationMs);
                            runner.ActivateOutput();
                            runner.QueryCompleted();
                        });
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
            get { return false; }
        }

        public bool IsEnabled
        {
            get { return _host.IsExcel; }
        }
        public int DisplayOrder
        {
            get { return 110; }
        }
    }


}
