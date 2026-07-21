using Caliburn.Micro;
using DaxStudio.CommandLine.Helpers;
using DaxStudio.CommandLine.Interfaces;
using DaxStudio.Common;
using DaxStudio.Common.Extensions;
using DaxStudio.Interfaces;
using DaxStudio.Core.Events;
using DaxStudio.Core.Connections;
using DaxStudio.Core.Options;
using DaxStudio.Core.Settings;
using Microsoft.AnalysisServices.AdomdClient;
using DaxStudio.Core.Interfaces;
using DaxStudio.Core.Model;
#if NET8_0_OR_GREATER
using AccessToken = Microsoft.AnalysisServices.AccessToken;
#endif
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
            Options = new OptionsModel(EventAggregator, _settingProvider);
            // this supports interactive Entra Auth if needed
            if (AccessTokenHelper.IsAccessTokenNeeded(ConnectionStringWithInitialCatalog)) {
            AccessToken = AccessTokenHelper.GetAccessToken(ConnectionStringWithInitialCatalog);
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

        public DaxStudio.Core.Connections.ConnectionManager Connection { get; private set; }

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
            Connection = new DaxStudio.Core.Connections.ConnectionManager(EventAggregator);
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
            // Not applicable for cmdline
        }

        public void QueryCompleted(bool isCancelled)
        {
            // Not applicable for cmdline
        }

        public void SetResultsMessage(string message, OutputTarget icon)
        {
            // Not applicable for cmdline
        }

        public void QueryFailed(string errorMessage)
        {
            // Not applicable for cmdline
        }

        public void OutputQueryError(string errorMessage)
        {
            Log.Error(errorMessage);
        }

        public void ClearQueryError()
        {
            // Not applicable for cmdline
        }

        public void ClearQueryResults()
        {
            // Not applicable for cmdline
        }

        public void OutputMessage(OutputMessage message)
        {
            switch (message.MessageType)
            {
                case MessageType.Information:
                    Log.Information(message.Text);
                    break;
                case MessageType.Success:
                    Log.Information($"{message.Text} ({message.DurationString}ms)");
                    break;
                case MessageType.Warning:
                    Log.Warning(message.Text);
                    break;
                case MessageType.Error:
                    Log.Error($"{message.Text} ({message.DurationString}ms)");
                    break;
                default:
                    throw new ArgumentOutOfRangeException("message.MessageType", message.MessageType, null);
            }
        }

        public void SetResultsMessage(string message, OutputTarget icon, string fileName)
        {
            Log.Information(message);
        }

        // The command-line assertion flow evaluates "--> ASSERT" commands directly via the shared
        // AssertionEngine (see the file/test commands), not through the interactive per-batch Test
        // Results pane hooks, so these are no-ops here.
        public void PrepareBatchAssertions(int batchIndex) { }

        public System.Threading.Tasks.Task ProcessBatchAssertionsAsync(int batchIndex, System.Collections.Generic.IReadOnlyList<System.Data.DataTable> batchTables)
            => System.Threading.Tasks.Task.CompletedTask;

        public void SetResultTabs(System.Collections.Generic.IList<DaxStudio.Core.Model.ResultTabDescriptor> tabs)
        {
            if (tabs == null) return;
            foreach (var tab in tabs)
            {
                if (tab.IsShowTree)
                {
                    Log.Information("SHOW {showType}", tab.ShowType);
                    if (tab.ShowTreeRoots == null) continue;
                    foreach (var root in tab.ShowTreeRoots)
                    {
                        LogShowTreeNode(root, 0, tab.ShowType);
                    }
                }
                else if (tab.Table != null)
                {
                    Log.Information("Result table '{tableName}' ({rowCount} rows)", tab.Table.TableName, tab.Table.Rows.Count);
                }
            }
        }

        private static void LogShowTreeNode(ShowTreeNode node, int depth, DaxStudio.Parsers.CommentScript.ShowType showType)
        {
            if (node == null) return;
            var indent = new string(' ', depth * 2);
            var timestamp = showType == DaxStudio.Parsers.CommentScript.ShowType.Dependencies || string.IsNullOrEmpty(node.LastModifiedDisplay)
                ? string.Empty
                : $"  [{node.LastModifiedDisplay}]";
            Log.Information("{indent}{name} ({objectType}){timestamp}", indent, node.Name, node.ObjectType, timestamp);
            foreach (var child in node.Children)
            {
                LogShowTreeNode(child, depth + 1, showType);
            }
        }
    }
}
