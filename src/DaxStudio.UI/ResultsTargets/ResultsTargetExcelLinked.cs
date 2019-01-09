using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;

namespace DaxStudio.UI.ResultsTargets
{
    // This is the target which writes the static results out to
    // a range in Excel
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelLinked: IResultsTarget, IActivateResults
    {
        private IDaxStudioHost _host;
        [ImportingConstructor]
        public ResultsTargetExcelLinked(IDaxStudioHost host)
        {
            _host = host;
        }

        #region Standard Properties
        public string Name => "Linked";
        public string Group => "Excel";
        public bool IsDefault => false;
        public bool IsEnabled => _host.IsExcel;
        public int DisplayOrder => 100;
        public string Message => "Query will be sent to Excel for execution";
        public OutputTargets Icon => OutputTargets.Linked;
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
                        
                        //  write results to Excel
                        runner.Host.Proxy.OutputLinkedResultAsync(dq
                            , runner.SelectedWorksheet
                            , runner.ConnectedToPowerPivot?"":runner.ConnectionStringWithInitialCatalog).ContinueWith((ascendant) => {

                                sw.Stop();
                                var durationMs = sw.ElapsedMilliseconds;
                     
                                runner.OutputMessage(
                                    string.Format("Query Completed - Query sent to Excel for execution)"), durationMs);
                                runner.ActivateOutput();
                                runner.SetResultsMessage("Query sent to Excel for execution", OutputTargets.Linked);

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
