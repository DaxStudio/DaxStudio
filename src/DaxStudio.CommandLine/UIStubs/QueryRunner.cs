using Caliburn.Micro;
using DaxStudio.CommandLine.Helpers;
using DaxStudio.CommandLine.Interfaces;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.AnalysisServices.AdomdClient;
using Serilog;
using System;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Threading.Tasks;

namespace DaxStudio.CommandLine.UIStubs
{
    internal class QueryRunner : IQueryRunner
    {
        static IEventAggregator EventAggregator { get; set; } = new EventAggregator();
        private ISettingProvider _settingProvider;

        public QueryRunner(ISettingsConnection settings)
        {

            ConnectionStringWithInitialCatalog = settings.FullConnectionString;
            _settingProvider = SettingsProviderFactory.GetSettingProvider();
            Options = new OptionsViewModel(EventAggregator, _settingProvider);
            // this supports interactive Entra Auth if needed
            if (AccessTokenHelper.IsAccessTokenNeeded(ConnectionStringWithInitialCatalog)) {
            AccessToken = AccessTokenHelper.GetAccessToken();
            }
        }



        private string _queryText = string.Empty;
        public string QueryText => _queryText;

        public DataTable ResultsTable { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public DataSet ResultsDataSet { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public IDaxStudioHost Host => throw new System.NotImplementedException();

        public string SelectedWorksheet { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public string ConnectionStringWithInitialCatalog { get; }

        public bool ConnectedToPowerPivot => false;

        public int RowCount { get; set; }

        public IGlobalOptions Options { get; }

        public ConnectionManager Connection { get; private set; }

        public void ActivateOutput()
        {
            // not applicable for cmdline
        }

        public void ActivateResults()
        {
            // not applicable for cmdline
        }

        private AccessToken AccessToken { get; set; } 

        public global::ADOTabular.AdomdClientWrappers.AdomdDataReader ExecuteDataReaderQuery(string daxQuery, System.Collections.Generic.List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList)
        {
            System.Diagnostics.Debug.WriteLine("Execute Data Reader");
            Connection = new ConnectionManager(EventAggregator);
            var msg = new ConnectEvent() { 
                ConnectionString = ConnectionStringWithInitialCatalog,
                AccessToken = this.AccessToken
            };
            Connection.Connect(msg);
            
            //var cnn = new ADOTabular.ADOTabularConnection(ConnectionStringWithInitialCatalog, ADOTabular.Enums.AdomdType.AnalysisServices);
            return this.Connection.ExecuteReader(daxQuery, paramList);
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

        public void OutputQueryError(string errorMessage)
        {
            Log.Error(errorMessage);
        }

        public void ClearQueryError()
        {
            // TODO
        }
    }
}
