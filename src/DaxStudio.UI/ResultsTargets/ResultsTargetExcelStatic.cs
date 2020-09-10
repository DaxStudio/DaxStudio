using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using System.Data;
using Serilog;

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
        public int DisplayOrder => 310;
        public string Message => "Static Results will be sent to Excel";
        public OutputTarget Icon => OutputTarget.Static;
        public string Tooltip => "Sends a copy of the results to Excel";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider)
        {
            await Task.Run(async () =>
                {
                    try
                    {
                        runner.OutputMessage("Query Started");
                        var sw = Stopwatch.StartNew();

                        var dq = textProvider.QueryText;

                        DataTable res = await runner.ExecuteDataTableQueryAsync(dq);

                        if (res == null || res.Rows?.Count == 0)
                        {
                            Log.Warning("{class} {method} {message}", nameof(ResultsTargetExcelStatic), nameof(OutputResultsAsync), "Query Result DataTable has no rows");
                            runner.ActivateOutput();
                            runner.OutputWarning("Unable to send results to Excel as there are no rows in the result set");
                            return;
                        }

                        sw.Stop();
                        var durationMs = sw.ElapsedMilliseconds;

                        // write results to Excel
                        await runner.Host.Proxy.OutputStaticResultAsync(res, runner.SelectedWorksheet); //.ContinueWith((ascendant) => {
                        
                        runner.OutputMessage(
                            string.Format("Query Completed ({0:N0} row{1} returned)", res.Rows.Count,
                                            res.Rows.Count == 1 ? "" : "s"), durationMs);
                        runner.RowCount = res.Rows.Count;
                        runner.ActivateOutput();
                        runner.SetResultsMessage("Static results sent to Excel", OutputTarget.Static);
                        //},TaskScheduler.Default);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "{class} {method} {message}", nameof(ResultsTargetExcelStatic), nameof(OutputResultsAsync), ex.Message);
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
