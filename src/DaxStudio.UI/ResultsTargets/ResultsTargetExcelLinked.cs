using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using Serilog;

namespace DaxStudio.UI.ResultsTargets
{
    // This is the target which writes the static results out to
    // a range in Excel
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelLinked: PropertyChangedBase, 
        IResultsTarget, 
        IActivateResults, 
        IHandle<ConnectionChangedEvent>,
        IHandle<ActivateDocumentEvent>
    {
        private IDaxStudioHost _host;
        private IEventAggregator _eventAggregator;
        private bool _isPowerBIOrSSDTConnection;

        [ImportingConstructor]
        public ResultsTargetExcelLinked(IDaxStudioHost host, IEventAggregator eventAggregator)
        {
            _host = host;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
        }

        #region Standard Properties
        public string Name => "Linked";
        public string Group => "Excel";
        public bool IsDefault => false;
        public bool IsAvailable => _host.IsExcel && !_isPowerBIOrSSDTConnection;
        public int DisplayOrder => 300;
        public string Message => "Query will be sent to Excel for execution";
        public OutputTarget Icon => OutputTarget.Linked;
        public string Tooltip => "Sends the Query text to Excel for execution";
        public bool IsEnabled => !_isPowerBIOrSSDTConnection;

        public string DisabledReason => "Linked Excel output is not supported against Power BI Desktop or SSDT based connections";

        public void Handle(ConnectionChangedEvent message)
        {
            _isPowerBIOrSSDTConnection = message.IsPowerBIorSSDT;
            NotifyOfPropertyChange(() => IsEnabled);
            _eventAggregator.PublishOnUIThread(new RefreshOutputTargetsEvent());
        }

        public void Handle(ActivateDocumentEvent message)
        {
            _isPowerBIOrSSDTConnection = message.Document.Connection?.IsPowerBIorSSDT ?? false;
            NotifyOfPropertyChange(() => IsEnabled);
            _eventAggregator.PublishOnUIThread(new RefreshOutputTargetsEvent());
        }
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider)
        {
            await Task.Run(() =>
                {
                    try
                    {
                        runner.OutputMessage("Query Started");
                        var sw = Stopwatch.StartNew();
                        var dq = textProvider.QueryText;
                        
                        //  write results to Excel
                        runner.Host.Proxy.OutputLinkedResultAsync(dq
                            , runner.SelectedWorksheet
                            , runner.ConnectedToPowerPivot?"":runner.ConnectionStringWithInitialCatalog).ContinueWith((ascendant) => {

                                sw.Stop();
                                var durationMs = sw.ElapsedMilliseconds;
                     
                                runner.OutputMessage(
                                    string.Format("Query Completed - Query sent to Excel for execution)"), durationMs);
                                runner.ActivateOutput();
                                runner.SetResultsMessage("Query sent to Excel for execution", OutputTarget.Linked);

                            },TaskScheduler.Default);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ResultsTargetExcelLinked), nameof(OutputResultsAsync), ex.Message);
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
