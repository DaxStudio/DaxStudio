using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;

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
        private bool _isPowerBIOrSSDTConnection = false;

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
        public int DisplayOrder => 100;
        public string Message => "Query will be sent to Excel for execution";
        public OutputTargets Icon => OutputTargets.Linked;

        public bool IsEnabled => !_isPowerBIOrSSDTConnection;

        public string DisabledReason => "Linked Excel output is not supported against Power BI Desktop or SSDT based connections";

        public void Handle(ConnectionChangedEvent message)
        {
            _isPowerBIOrSSDTConnection = message.Connection?.IsPowerBIorSSDT ?? false;
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
