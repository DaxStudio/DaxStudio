using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using Serilog;
using System.Threading;

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
            _eventAggregator.SubscribeOnPublishedThread(this);
        }

        #region Standard Properties
        public string Name => "Linked";
        public string Group => "Excel";
        public bool IsDefault => false;
        public bool IsAvailable => _host.IsExcel && !_isPowerBIOrSSDTConnection;
        public int DisplayOrder => 300;
        public string Message => "Query will be sent to Excel for execution";
        public OutputTarget Icon => OutputTarget.Linked;
        public string ImageResource => "results_excel_linked_smallDrawingImage";
        public string Tooltip => "Sends the Query text to Excel for execution";
        public bool IsEnabled => !_isPowerBIOrSSDTConnection;

        public string DisabledReason => "Linked Excel output is not supported against Power BI Desktop or SSDT based connections";

        public async Task HandleAsync(ConnectionChangedEvent message, CancellationToken cancellationToken)
        {
            _isPowerBIOrSSDTConnection = message.IsPowerBIorSSDT;
            NotifyOfPropertyChange(() => IsEnabled);
            await _eventAggregator.PublishOnUIThreadAsync(new RefreshOutputTargetsEvent());
            return; 
        }

        public async Task HandleAsync(ActivateDocumentEvent message, CancellationToken cancellationToken)
        {
            _isPowerBIOrSSDTConnection = message.Document.Connection?.IsPowerBIorSSDT ?? false;
            NotifyOfPropertyChange(() => IsEnabled);
            await _eventAggregator.PublishOnUIThreadAsync(new RefreshOutputTargetsEvent());
        }
        #endregion

        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string filename)
        {
            await Task.Run(async () =>
                {

                    var sw = Stopwatch.StartNew();
                    var dq = textProvider.QueryText;
                        
                    //  write results to Excel
                    await runner.Host.Proxy.OutputLinkedResultAsync(dq
                        , runner.SelectedWorksheet
                        , runner.ConnectedToPowerPivot?"":runner.ConnectionStringWithInitialCatalog);


                    sw.Stop();
                    var durationMs = sw.ElapsedMilliseconds;
                     
                    runner.OutputMessage(
                        string.Format("Query Completed - Query sent to Excel for execution)"), durationMs);
                    runner.ActivateOutput();
                    runner.SetResultsMessage("Query sent to Excel for execution", OutputTarget.Linked);


                });
        }

    }


}
