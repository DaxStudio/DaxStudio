using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Events;
using System.Diagnostics;

namespace DaxStudio.UI.ResultsTargets
{
    // This is the target which writes the static results out to
    // a range in Excel
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelLinked: IResultsTarget
    {
        private IDaxStudioHost _host;
        [ImportingConstructor]
        public ResultsTargetExcelLinked(IDaxStudioHost host)
        {
            _host = host;
        }
        public string Name {get { return "Linked"; }
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

                    using (runner.NewStatusBarMessage("Executing Query..."))
                    {
                        sw.Stop();
                        var durationMs = sw.ElapsedMilliseconds;
                        runner.Host.Proxy.OutputLinkedResultAsync(
                            dq
                            , runner.SelectedWorksheet
                            , runner.ConnectedToPowerPivot?"":runner.ConnectionString).ContinueWith((ascendant) =>
                        {
                            
                            // TODO - what message should we output here?
                            runner.OutputMessage(
                                string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
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

                        //  write results to Excel
                        runner.Host.Proxy.OutputLinkedResultAsync(dq
                            , runner.SelectedWorksheet
                            , runner.ConnectedToPowerPivot?"":runner.ConnectionString).ContinueWith((ascendant) => {

                            runner.OutputMessage(
                                string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
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
            get { return 100; }
        }
    }


}
