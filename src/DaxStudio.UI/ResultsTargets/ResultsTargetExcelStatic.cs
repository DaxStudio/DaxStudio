using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Events;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.Model
{
    // This is the target which writes the static results out to
    // a range in Excel
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelStatic: IResultsTarget, IActivateResults
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


                        // write results to Excel
                        runner.Host.Proxy.OutputStaticResultAsync(res, runner.SelectedWorksheet).ContinueWith((ascendant) => {
                            runner.OutputMessage(
                                string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
                                              res.Rows.Count == 1 ? "" : "s"), durationMs);
                            runner.ActivateOutput();
                            runner.SetResultsMessage("Static results sent to Excel", OutputTargets.Static);
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


        public string Message
        {
            get {
            return "Static Results will be sent to Excel";
            }
        }
        public OutputTargets Icon
        {
            get
            {
                return OutputTargets.Static;
            }
        }
    }


}
