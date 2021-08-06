using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using Serilog;
using DaxStudio.UI.Extensions;
using System.Data;
using System.Collections;
using System.Text;
using DaxStudio.Common;
using System.IO;
using DaxStudio.UI.Events;
using System.Windows;

namespace DaxStudio.UI.Model
{
    // This is the default target which writes the results out to
    // the built-in grid
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetClipboard: IResultsTarget 
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IGlobalOptions _options;

        [ImportingConstructor]
        public ResultsTargetClipboard(IEventAggregator eventAggregator, IGlobalOptions options)
        {
            _eventAggregator = eventAggregator;
            _options = options;
        }

        #region Standard Properties
        public string Name => "Clipboard";
        public string Group => "Standard";
        public int DisplayOrder => 210;
        public bool IsDefault => false;
        public bool IsAvailable => true;
        public string Message => "Query output sent to Clipboard";
        public OutputTarget Icon => OutputTarget.Clipboard;
        public string Tooltip => "Exports Query results to the Clipboard";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        // This is the core method that handles the output of the results
        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider)
        {
            StringBuilder sb = new StringBuilder();

            await Task.Run(() =>
                {
                    long durationMs = 0;
                    try
                    {
                        runner.OutputMessage("Query Started");

                        var sw = Stopwatch.StartNew();
                        
                        string sep = "\t";
                        bool shouldQuoteStrings = true; //default to quoting all string fields
                        string decimalSep = System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator;
                        string isoDateFormat = string.Format(Constants.IsoDateMask, decimalSep);
                        Encoding enc = new UTF8Encoding(false);
                           
                        var daxQuery = textProvider.QueryText;
                        var reader = runner.ExecuteDataReaderQuery(daxQuery, textProvider.ParameterCollection);

                        using (var statusProgress = runner.NewStatusBarMessage("Starting Export"))
                        {

                            try
                            {
                                if (reader != null)
                                {


                                    runner.OutputMessage("Command Complete, writing output to clipboard");

                                    bool moreResults = true;

                                    while (moreResults)
                                    {

                                        int iRowCnt = 0;
                                        
                                        
                                        using (StringWriter textWriter = new StringWriter(sb))
                                        //using (var textWriter = new System.IO.StreamWriter( stringWriter, false, enc))
                                        {
                                            iRowCnt = reader.WriteToStream(textWriter, sep, shouldQuoteStrings, isoDateFormat, statusProgress);
                                        }

                                        runner.OutputMessage(
                                                string.Format("Query Completed ({0:N0} row{1} returned)"
                                                            , iRowCnt
                                                            , iRowCnt == 1 ? "" : "s")
                                                );

                                        runner.RowCount = iRowCnt;

                                        moreResults = reader.NextResult();

                                        if (moreResults)
                                        {
                                            _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, "Output to Clipboard only copies the first table of results"));
                                            while (reader.NextResult())
                                            {
                                                // loop thru 
                                            }
                                        }
                                    }

                                    sw.Stop();
                                    durationMs = sw.ElapsedMilliseconds;

                                    runner.SetResultsMessage("Query results written to file", OutputTarget.File);
                                    runner.ActivateOutput();
                                }
                                else
                                    runner.OutputError("Query Batch Completed with errors listed above (you may need to scroll up)", durationMs);
                            }
                            finally
                            {
                                if (reader != null)
                                {
                                    reader.Dispose();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ResultsTargetClipboard), nameof(OutputResultsAsync), ex.Message);
                        runner.ActivateOutput();
                        runner.OutputError(ex.Message);
#if DEBUG
                        runner.OutputError(ex.StackTrace);
#endif
                    }
                    finally
                    {
                        runner.OutputMessage("Query Batch Completed", durationMs);
                        runner.QueryCompleted();
                    }

                });

            // copy output to clipboard
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => {
                System.Windows.Forms.Clipboard.SetText(sb.ToString());
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

    }

}
