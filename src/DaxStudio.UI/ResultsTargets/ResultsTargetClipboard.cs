using System.ComponentModel.Composition;
using System.Threading.Tasks;
using DaxStudio.Interfaces;
using System.Diagnostics;
using Caliburn.Micro;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Extensions;
using System.Text;
using DaxStudio.Common;
using System.IO;
using DaxStudio.UI.Events;
using System.Windows;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.ResultsTargets
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
        public string ImageResource => "results_clipboardDrawingImage";
        public string Tooltip => "Exports Query results to the Clipboard";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        // This is the core method that handles the output of the results
        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string filename)
        {
            StringBuilder sb = new StringBuilder();

            await Task.Run(() =>
                {
                    long durationMs = 0;

                    var sw = Stopwatch.StartNew();
                        
                    string sep = "\t";
                    bool shouldQuoteStrings = true; //default to quoting all string fields
                    string decimalSep = System.Globalization.CultureInfo.CurrentUICulture.NumberFormat.CurrencyDecimalSeparator;
                    string isoDateFormat = string.Format(Constants.IsoDateMask, decimalSep);
                    Encoding enc = new UTF8Encoding(false);
                           
                    var daxQuery = textProvider.QueryText;

                    using (var reader = runner.ExecuteDataReaderQuery(daxQuery, textProvider.ParameterCollection))
                    using (var statusProgress = runner.NewStatusBarMessage("Starting Export"))
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
                                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, "Output to Clipboard only copies the first table of results"));
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

                    }


                });

            // copy output to clipboard
            Application.Current.Dispatcher.Invoke(() => {
                ClipboardManager.SetText(sb.ToString());
            }, System.Windows.Threading.DispatcherPriority.Normal);
        }

    }

}
