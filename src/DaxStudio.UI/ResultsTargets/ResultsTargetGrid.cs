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
using DaxStudio.Common.Extensions;

namespace DaxStudio.UI.ResultsTargets
{
    // This is the default target which writes the results out to
    // the built-in grid
    [Export(typeof(IResultsTarget))]
    public class ResultsTargetGrid: PropertyChangedBase, IResultsTarget 
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IGlobalOptions _options;



        [ImportingConstructor]
        public ResultsTargetGrid(IEventAggregator eventAggregator, IGlobalOptions options)
        {
            _eventAggregator = eventAggregator;
            _options = options;
        }

        #region Standard Properties
        public string Name => "Results Table";
        public string Group => "Standard";
        public int DisplayOrder => 10;
        public bool IsDefault => true;
        public bool IsAvailable => true;
        public string Message => "Query results will be displayed in a data grid";
        public OutputTarget Icon => OutputTarget.Grid;
        public string ImageResource => "results_tableDrawingImage";
        public string Tooltip => "Displays the Query results in a data grid";
        public bool IsEnabled => true;

        public string DisabledReason => "";
        #endregion

        // This is the core method that handles the output of the results
        public async Task OutputResultsAsync(IQueryRunner runner, IQueryTextProvider textProvider, string filename)
        {
            // Read the AutoFormat option from the options singleton
            bool autoFormat = _options.ResultAutoFormat;
            string autoDateFormat = _options.DefaultDateAutoFormat;
            await Task.Run(() =>
                {
                    long durationMs = 0;
                    int queryCnt = 1;

                    // Clear any existing results
                    runner.ResultsDataSet = new DataSet();
                    runner.SetResultsMessage("Waiting for query results", OutputTarget.Grid);
                    runner.RowCount = 0;

                    var sw = Stopwatch.StartNew();

                    var dq = textProvider.QueryText;
                    //var res = runner.ExecuteDataTableQuery(dq);
                    var isSessionsDmv = dq.Contains(Common.Constants.SessionsDmv, StringComparison.OrdinalIgnoreCase);


                    using (var dataReader = runner.ExecuteDataReaderQuery(dq, textProvider.ParameterCollection))
                    {
                        if (dataReader != null)
                        {
                            Log.Verbose("Start Processing Grid DataReader (Elapsed: {elapsed})" , sw.ElapsedMilliseconds);
                            runner.ResultsDataSet = dataReader.ConvertToDataSet(autoFormat, isSessionsDmv, autoDateFormat, runner.Connection);
                            Log.Verbose("End Processing Grid DataReader (Elapsed: {elapsed})", sw.ElapsedMilliseconds);

                            sw.Stop();

                            // add extended properties to DataSet
                            runner.ResultsDataSet.ExtendedProperties.Add("QueryText", dq);
                            runner.ResultsDataSet.ExtendedProperties.Add("IsDiscoverSessions", isSessionsDmv);

                            durationMs = sw.ElapsedMilliseconds;
                            var rowCnt = runner.ResultsDataSet.Tables[0].Rows.Count;
                            foreach (DataTable tbl in runner.ResultsDataSet.Tables)
                            {
                                runner.OutputMessage(
                                    string.Format("Query {2} Completed ({0:N0} row{1} returned)", tbl.Rows.Count,
                                                    tbl.Rows.Count == 1 ? "" : "s", queryCnt));
                                queryCnt++;
                            }
                            runner.RowCount = rowCnt;
                            // activate the result only when Counters are not selected...
                            runner.ActivateResults();
                            runner.OutputMessage("Query Batch Completed", durationMs);
                        }
                        else
                            runner.OutputError("Query Batch Completed with errors listed above (you may need to scroll up)", durationMs);

                    }

                });
        }

    }

}
