using Caliburn.Micro;
using DaxStudio.CommandLine.Interfaces;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Serilog;
using System.Data;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine
{
    internal class QueryRunner : IQueryRunner
    {
        static IEventAggregator EventAggregator { get; set; } = new EventAggregator();
        private ISettingProvider _settingProvider;

        public QueryRunner(ISettingsConnection settings)
        {
            // TODO - how to support AzureAD auth??
            ConnectionStringWithInitialCatalog = settings.FullConnectionString;
            _settingProvider = SettingsProviderFactory.GetSettingProvider();
            Options = new OptionsViewModel(EventAggregator, _settingProvider);
        }

        private string _queryText = string.Empty;
        public string QueryText => _queryText;

        public DataTable ResultsTable { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public DataSet ResultsDataSet { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public IDaxStudioHost Host => throw new System.NotImplementedException();

        public string SelectedWorksheet { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public string ConnectionStringWithInitialCatalog { get; }

        public bool ConnectedToPowerPivot => false;

        public int RowCount { get ; set ; }

        public IGlobalOptions Options { get; }

        public ConnectionManager Connection => throw new System.NotImplementedException();

        public void ActivateOutput()
        {
            // not applicable for cmdline
        }

        public void ActivateResults()
        {
            // not applicable for cmdline
        }

        public global::ADOTabular.AdomdClientWrappers.AdomdDataReader ExecuteDataReaderQuery(string daxQuery, System.Collections.Generic.List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList)
        {
            System.Diagnostics.Debug.WriteLine("Execute Data Reader");
            var cnn = new ADOTabular.ADOTabularConnection(ConnectionStringWithInitialCatalog, ADOTabular.Enums.AdomdType.AnalysisServices);
            return cnn.ExecuteReader(daxQuery, paramList);
        }

        public Task<DataTable> ExecuteDataTableQueryAsync(string daxQuery)
        {
            throw new System.NotImplementedException();
        }

        public IStatusBarMessage NewStatusBarMessage(string message)
        {
            // na
            return new StatusBarMessage(null, message);
        }

        public void OutputError(string errorMessage)
        {
            Log.Error(errorMessage);
        }

        public void OutputError(string errorMessage, double duration)
        {
            Log.Error(errorMessage);
        }

        public void OutputMessage(string message)
        {
            Log.Information(message);
        }

        public void OutputMessage(string message, double duration)
        {
            Log.Information($"{message} ({duration}ms)");
        }

        public void OutputWarning(string warning)
        {
            Log.Warning(warning);
        }

        public void QueryCompleted()
        {
            // TODO
        }

        public void QueryCompleted(bool isCancelled)
        {
            // TODO
        }

        public void SetResultsMessage(string message, OutputTarget icon)
        {
            // TODO
        }

        public void QueryFailed(string errorMessage)
        {
            // TODO
        }
    }
}
