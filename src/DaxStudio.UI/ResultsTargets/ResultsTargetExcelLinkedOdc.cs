﻿using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using DaxStudio.UI.Interfaces;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils;
using System.Threading;
using System.IO;
using Serilog;

namespace DaxStudio.UI.ResultsTargets
{
    // This is the target which writes the static results out to
    // a range in Excel
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetExcelLinkedOdc: PropertyChangedBase, 
        IResultsTarget, 
        IActivateResults, 
        IHandle<ConnectionChangedEvent>,
        IHandle<ActivateDocumentEvent>
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private bool _isPowerBIOrSSDTConnection;

        [ImportingConstructor]
        public ResultsTargetExcelLinkedOdc(IDaxStudioHost host, IEventAggregator eventAggregator)
        {
            _host = host;
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnPublishedThread(this);
        }

        #region Standard Properties
        public string Name => "Linked";
        public string Group => "Excel";
        public bool IsDefault => false;
        public bool IsAvailable => !_host.IsExcel;
        public int DisplayOrder => 410;
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

                    runner.OutputMessage("Opening .odc file in Excel");
                    runner.ClearQueryResults();
                    var sw = Stopwatch.StartNew();
                    var dq = textProvider.QueryText;

                    // odc queries require 'mdx compatibility=1'
                    var fixedConnStr = runner.ConnectionStringWithInitialCatalog.Replace("mdx compatibility=3", "mdx compatibility=1");

                    // create odc file
                    var odcFile = OdcHelper.CreateOdcQueryFile(fixedConnStr, dq );


                    Process.Start(odcFile);
                    //  write results to Excel
                 

                    sw.Stop();
                    var durationMs = sw.ElapsedMilliseconds;
                     
                    runner.OutputMessage(
                        "Query Completed - Query sent to Excel for execution", durationMs);
                    runner.OutputMessage("Note: odc files can only handle a query that returns a single result set. If you see an error try using one of the other output types to ensure your query is valid.");
                        
                    runner.ActivateOutput();
                    runner.SetResultsMessage("Query sent to Excel for execution", OutputTarget.Linked);

                    await CleanUpOdcAsync(odcFile);

                });
        }

        private static async Task CleanUpOdcAsync(string odcFile)
        {
            await Task.Factory.StartNew(() =>
            {
                // wait before cleaning up the 
                Thread.Sleep(20000);
                try
                {
                    File.Delete(odcFile);
                    Debug.Write($"ODC file deleted - {odcFile}");
                    Log.Debug(Common.Constants.LogMessageTemplate, nameof(ResultsTargetExcelLinkedOdc), nameof(CleanUpOdcAsync), $"Deleted odc file: {odcFile}");
                }
                catch
                {
                    // swallow any errors
                }
            });
        }
    }


}
