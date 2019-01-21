using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
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

        #region Standard Properties
        public string Name => "Static";
        public string Group => "Excel";
        public bool IsDefault => false;
        public bool IsAvailable => _host.IsExcel;
        public int DisplayOrder => 110;
        public string Message => "Static Results will be sent to Excel";
        public OutputTargets Icon => OutputTargets.Static;

        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        public Task OutputResultsAsync(IQueryRunner runner)
        {
            return Task.Run(() =>
                {
                    try
                    {
                        runner.OutputMessage("Query Started");
                        var sw = Stopwatch.StartNew();

                        var dq = runner.QueryText;
                        var res = runner.ExecuteDataTableQuery(dq);

                        sw.Stop();
                        var durationMs = sw.ElapsedMilliseconds;


                        // write results to Excel
                        runner.Host.Proxy.OutputStaticResultAsync(res, runner.SelectedWorksheet).ContinueWith((ascendant) => {
                            runner.OutputMessage(
                                string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
                                              res.Rows.Count == 1 ? "" : "s"), durationMs);
                            runner.RowCount = res.Rows.Count;
                            runner.ActivateOutput();
                            runner.SetResultsMessage("Static results sent to Excel", OutputTargets.Static);
                        },TaskScheduler.Default);
                    }
                    catch (Exception ex)
                    {
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
                    }
                    finally
                    {
                        runner.QueryCompleted();
                    }
                });
        }



    }


}
