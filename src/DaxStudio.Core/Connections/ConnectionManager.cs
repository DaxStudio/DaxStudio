using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Enums;
using ADOTabular.MetadataInfo;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.Core.Events;
using DaxStudio.Core.Model;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DaxStudio.Common.Enums;
using System.Xml.XPath;
using System.IO;
using ADOTabular.Utils;
using TOM = Microsoft.AnalysisServices;
using System.Xml;
using ADOTabular.Interfaces;
using DaxStudio.Common;
using ADOTabular.Extensions;
using DaxStudio.Core.Extensions;
using System.Threading;

namespace DaxStudio.Core.Connections
{

    // TODO - load metadata/tables from a different connection so that someone can type in the main window
    // TODO - add retry logic around queries and metadata refresh
    // TODO - flush metadata on connection failure
    // TODO - cache functions and dmvs unless we change the connection

    /// <summary>
    /// The purpose of the ConnectionManager is to centralize all the connection handling into one place
    /// This allows for consistent retry policies and allows us to use a secondary connection for things 
    /// like metadata refreshes.
    /// </summary>
    public class ConnectionManager : IConnectionManager
        , IDmvProvider
        , IFunctionProvider
        , IConnection
        , IModelIntellisenseProvider
        , IDisposable
    {
        public bool IsConnecting { get; private set; }

        protected ADOTabularConnection _connection;
        protected ADOTabularConnection _dmvConnection;
        protected readonly IEventAggregator _eventAggregator;
        protected RetryPolicy _retry;
        private RetryPolicy _dmvRetry;
        private static readonly IEnumerable<string> _keywords;
        private static readonly Regex guidRegex = new Regex("([0-9A-Fa-f]{8}[-]?[0-9A-Fa-f]{4}[-]?[0-9A-Fa-f]{4}[-]?[0-9A-Fa-f]{4}[-]?[0-9A-Fa-f]{12})", RegexOptions.Compiled);
        public event EventHandler AfterReconnect;
#pragma warning disable CS0414 // The field 'ConnectionManager.processModelTemplate' is assigned but its value is never used
        private string processModelTemplate = @"
<Batch Transaction=""false"" xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">
  <Refresh xmlns=""http://schemas.microsoft.com/analysisservices/2014/engine"">
    <DatabaseID>3728f81b-7e47-4c69-b519-c5b3060c2a33</DatabaseID>
    <Model>
      <xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns:sql=""urn:schemas-microsoft-com:xml-sql"">
        <xs:element>
          <xs:complexType>
            <xs:sequence>
              <xs:element type=""row""/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:complexType name=""row"">
          <xs:sequence>
            <xs:element name=""RefreshType"" type=""xs:long"" sql:field=""RefreshType"" minOccurs=""0""/>
          </xs:sequence>
        </xs:complexType>
      </xs:schema>
      <row xmlns=""urn:schemas-microsoft-com:xml-analysis:rowset"">
        <RefreshType>3</RefreshType>
      </row>
    </Model>
  </Refresh>
  <SequencePoint xmlns=""http://schemas.microsoft.com/analysisservices/2014/engine"">
    <DatabaseID>3728f81b-7e47-4c69-b519-c5b3060c2a33</DatabaseID>
  </SequencePoint>
</Batch>
";
#pragma warning restore CS0414 // The field 'ConnectionManager.processModelTemplate' is assigned but its value is never used

#pragma warning disable CS0414 // The field 'ConnectionManager.processTableTemplate' is assigned but its value is never used
        private string processTableTemplate = @"
<Batch Transaction=""true"" xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">
  <Refresh xmlns=""http://schemas.microsoft.com/analysisservices/2014/engine"">
    <DatabaseID>Adventure Works</DatabaseID>
    <Tables>
      <xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns:sql=""urn:schemas-microsoft-com:xml-sql"">
        <xs:element>
          <xs:complexType>
            <xs:sequence>
              <xs:element type=""row""/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:complexType name=""row"">
          <xs:sequence>
            <xs:element name=""ID"" type=""xs:unsignedLong"" sql:field=""ID"" minOccurs=""0""/>
            <xs:element name=""ID.Table"" type=""xs:string"" sql:field=""ID.Table"" minOccurs=""0""/>
            <xs:element name=""RefreshType"" type=""xs:long"" sql:field=""RefreshType"" minOccurs=""0""/>
          </xs:sequence>
        </xs:complexType>
      </xs:schema>
      <row xmlns=""urn:schemas-microsoft-com:xml-analysis:rowset"">
        <ID>22</ID>
        <RefreshType>8</RefreshType>
      </row>
    </Tables>
  </Refresh>
  <SequencePoint xmlns=""http://schemas.microsoft.com/analysisservices/2014/engine"">
    <DatabaseID>Adventure Works</DatabaseID>
  </SequencePoint>
</Batch>
";
#pragma warning restore CS0414 // The field 'ConnectionManager.processModelTemplate' is assigned but its value is never used
        static ConnectionManager()
        {
            _keywords = new List<string>()
            {   "COLUMN",
                "DEFINE",
                "EVALUATE",
                "MEASURE",
                "MPARAMETER",
                "ORDER BY",
                "RETURN",
                "TABLE",
                "VAR" };
        }
        public ConnectionManager(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            ConfigureRetryPolicy();
        }

        public IEnumerable<string> AllFunctions => _connection.AllFunctions;

        public IEnumerable<string> Keywords => _keywords;
        public string ApplicationName => _connection?.ApplicationName ?? "DAX Studio";

        public void Cancel()
        {
            _connection.Cancel();
        }

        //public ConnectionManager Clone()
        //{
        //    var newConn = new ConnectionManager(_eventAggregator);
        //    newConn.ConnectAsync(new ConnectEvent(ConnectionStringWithInitialCatalog, IsPowerPivot, ApplicationName, FileName??String.Empty, this.ServerType, false));
        //    return newConn;
        //}

        public void Close()
        {
            Close(false);
        }

        public void Close(bool closeSession)
        {
            ClearSupportedTraceEventClasses();
            if (_connection != null)
            {
                if (_connection.State != ConnectionState.Closed && _connection.State != ConnectionState.Broken)
                {
                    _connection.Close(closeSession);
                }
            }
            if (_dmvConnection != null)
            {
                if (_dmvConnection.State != ConnectionState.Closed && _dmvConnection.State != ConnectionState.Broken)
                {
                    _dmvConnection.Close(closeSession);
                }
            }
        }

        private void ConfigureRetryPolicy()
        {
            _retry = Policy
                .HandleInner<Microsoft.AnalysisServices.AdomdClient.AdomdConnectionException>()
                .WaitAndRetry(3, retryCount => TimeSpan.FromMilliseconds(200),
                    (exception, timespan, retryCount, context) =>
                    {
                        var contextDb = context.GetDatabaseName();
                        var currentDb = contextDb ?? Database?.Name ?? string.Empty;
                        
                        // cache the AccessToken
                        var accessTokenCopy = _connection.AccessToken;
                        var onAccessTokenExpiredCopy = _connection.OnAccessTokenExpired;
                        
                        _connection.Close(true); // force the connection closed and close the session

                        _connection = new ADOTabularConnection(_connection.ConnectionString, _connection.Type);
                        if (accessTokenCopy.IsNotNull())
                        {
                            _connection.AccessToken = accessTokenCopy;
                            _connection.OnAccessTokenExpired = onAccessTokenExpiredCopy;
                        }
                        _connection.ChangeDatabase(currentDb);

                        _eventAggregator.PublishAsync(new ReconnectEvent(_connection.SessionId));
                        var msg =
                            $"A connection error occurred: {exception.Message}\nAttempting to reconnect (retry: {retryCount})";
                        _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, msg));
                        Log.Warning(exception, Common.Constants.LogMessageTemplate, nameof(ConnectionManager),
                            "RetryPolicy", msg);

                        // trigger any after retry code
                        if (AfterReconnect != null) { AfterReconnect(this, EventArgs.Empty); }
                    });

            _dmvRetry = Policy
                .HandleInner<Microsoft.AnalysisServices.AdomdClient.AdomdConnectionException>()
                .WaitAndRetry(3, retryCount => TimeSpan.FromMilliseconds(200),
                    (exception, timespan, retryCount, context) =>
                    {
                        var contextDb = context.GetDatabaseName();
                        var currentDb = contextDb ?? Database?.Name ?? string.Empty;

                        // cache the AccessToken
                        var accessTokenCopy = _connection.AccessToken;
                        var onAccessTokenExpiredCopy = _connection.OnAccessTokenExpired;

                        _dmvConnection.Close(true); // force the connection closed and close the session

                        _dmvConnection = new ADOTabularConnection(_dmvConnection.ConnectionString, _dmvConnection.Type);
                        if (accessTokenCopy.IsNotNull())
                        {
                            _dmvConnection.AccessToken = accessTokenCopy;
                            _dmvConnection.OnAccessTokenExpired = onAccessTokenExpiredCopy;
                        }
                        _dmvConnection.ChangeDatabase(currentDb);

                        _eventAggregator.PublishAsync(new ReconnectEvent(_dmvConnection.SessionId));
                        var msg =
                            $"A connection error occurred: {exception.Message}\nAttempting to reconnect (retry: {retryCount})";
                        _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, msg));
                        Log.Warning(exception, Common.Constants.LogMessageTemplate, nameof(ConnectionManager),
                            "RetryPolicy", msg);
                    });
        }

        public string ConnectionString => _connection?.ConnectionString ?? string.Empty;

        public string ConnectionStringWithInitialCatalog =>
            _connection?.ConnectionStringWithInitialCatalog ?? string.Empty;

        public ADOTabularDatabase Database => _dmvRetry.Execute(() => {
            // Capture the current connection reference once - the field can be
            // reassigned by the retry policies on a background thread while a
            // consumer (e.g. the QueryHistory pane filter) is reading the
            // property. We must not throw NRE if the connection is null or has
            // not finished re-opening yet, otherwise callers like
            // ICollectionView.Refresh() will abort their filter pass.
            var dmv = _dmvConnection;
            if (dmv == null || dmv.State != ConnectionState.Open) return null;
            try { return dmv.Database; }
            catch (NullReferenceException) { return null; }
        });
        public string DatabaseName
        {
            get
            {
                try
                {
                    return _dmvRetry.Execute(() =>
                    {
                        var dmv = _dmvConnection;
                        if (dmv == null || dmv.State != ConnectionState.Open) return string.Empty;
                        return dmv.Database?.Name ?? string.Empty;
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(DatabaseName), "Error getting database name");
                    _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, $"Error getting database name: {ex.Message}"));
                    return string.Empty;
                }
            }
        }
        public DaxMetadata DaxMetadataInfo {
            get {
                return _dmvConnection?.DaxMetadataInfo;
            }
        }
        public DaxColumnsRemap DaxColumnsRemapInfo
        {
            get
            {
                ADOTabularConnection newConn = null;
                ADOTabularConnection conn;
                try
                {
                    // if the connection contains EffectiveUserName or Roles we clone it and strip those out
                    // so that we can run the discover command to get the column remap info
                    // Otherwise we just use the current connection

                    if (_dmvConnection.IsTestingRls)
                    {
                        newConn = _dmvConnection.CloneWithoutRLS();
                        conn = newConn;
                    }
                    else
                    {
                        conn = _dmvConnection;
                    }

                    var remapInfo = _dmvRetry.Execute(() =>  conn?.DaxColumnsRemapInfo);
                    return remapInfo;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager),
                        nameof(DaxColumnsRemapInfo), "Error getting column remap information");
                    _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning,
                        $"Unable to get column re-map information, this will mean that some of the xmSQL simplification cannot be done\nThis may be caused by connection parameters like Roles and EffectiveUserName that alter the permissions:\n {ex.Message}"));
                    return new DaxColumnsRemap();
                }
                finally
                {
                    // close the temporary connection if it's not null
                    newConn?.Close();
                }

            }

        }

        public DaxTablesRemap DaxTablesRemapInfo
        {
            get
            {
                ADOTabularConnection newConn = null;
                ADOTabularConnection conn;
                try
                {
                    // if the connection contains EffectiveUserName or Roles we clone it and strip those out
                    // so that we can run the discover command to get the column remap info
                    // Otherwise we just use the current connection
                    if (_connection.IsTestingRls)
                    {
                        newConn = _dmvConnection.CloneWithoutRLS();
                        conn = newConn;
                    }
                    else
                    {
                        conn = _dmvConnection;
                    }
                    var remapInfo = _dmvRetry.Execute(() => conn?.DaxTablesRemapInfo);
                    return remapInfo;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager),
                        nameof(DaxColumnsRemapInfo), "Error getting column remap information");
                    _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning,
                        $"Unable to get column re-map information, this will mean that some of the xmSQL simplification cannot be done\nThis may be caused by connection parameters like Roles and EffectiveUserName that alter the permissions:\n {ex.Message}"));
                    return new DaxTablesRemap();
                }
                finally
                {
                    // close the temporary connection if it's not null
                    newConn?.Close();
                }

            }
        }

        #region Query Exection

        public DataTable ExecuteMetadataDaxQueryDataTable(string query)
        {
            return _dmvRetry.Execute(() =>
            {
                return _dmvConnection.ExecuteDaxQueryDataTable(query);
            });
        }

        public DataTable ExecuteDaxQueryDataTable(string query)
        {
            return _retry.Execute(() =>
            {
               return _connection.ExecuteDaxQueryDataTable(query);    
            });
        }

        public AdomdDataReader ExecuteReader(string query, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList)
        {
            return _retry.Execute(() =>
            {

                return _connection.ExecuteReader(query, paramList);
            });
        }

        public AdomdDataReader ExecuteReaderForPrepare(string query, List<Microsoft.AnalysisServices.AdomdClient.AdomdParameter> paramList)
        {
            return _retry.Execute(() =>
            {

                return _connection.ExecuteReaderForPrepare(query, paramList);
            });
        }

        public string FileName
        {
            get => _connection?.FileName;
            set
            {
                if (_connection != null)
                {
                    _connection.FileName = value;
                }
                if (_dmvConnection != null)
                {
                    _dmvConnection.FileName = value;
                }
            }
        }

        private ADOTabularDynamicManagementViewCollection _dynamicManagementViews;
        public ADOTabularDynamicManagementViewCollection DynamicManagementViews
        {
            get
            {
                if (_dynamicManagementViews == null && _dmvConnection != null) _dynamicManagementViews = new ADOTabularDynamicManagementViewCollection(_dmvConnection);
                return _dynamicManagementViews;
            }
        }

        #endregion


        public async Task<bool> HasSchemaChangedAsync()
        {
            if (!this.IsConnected) return false;

            return await _dmvRetry.Execute(async () =>
            {
                try
                {
                    bool hasChanged = await Task.Run(() =>
                    {
                        var conn = new ADOTabularConnection(this.ConnectionString, this.Type);
                        if (this.AccessToken.IsNotNull())
                        {
                            conn.AccessToken = this.AccessToken;
                            conn.OnAccessTokenExpired = this.OnAccessTokenExpired;
                        }
                        conn.ChangeDatabase(this.DatabaseName);
                        if (conn.State != ConnectionState.Open) conn.Open();
                        var dbChanges = conn.Database?.LastUpdate > _lastSchemaUpdate;
                        _lastSchemaUpdate = conn.Database?.LastUpdate ?? DateTime.MinValue;
                        conn.Close(true); // close and end the session
                        conn.Dispose();
                        return dbChanges;
                    });
                    return hasChanged;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(HasSchemaChangedAsync), "Error checking if schema has been changed");
                    Close();
                    return false;
                }


            });

        }

        public ADOTabularDatabaseCollection Databases {
            get {
                return _dmvConnection.Databases;
            }
        }
        public bool IsAdminConnection => _connection?.ServerType != ServerType.Offline && ( _connection?.IsAdminConnection ?? false);

        public bool IsConnected { get
            {
                if (_connection == null) return false;
                // make sure both connections are open so that we can run queries 
                // and return metadata information
                return _connection.State == ConnectionState.Open && _dmvConnection.State == ConnectionState.Open;
            }
        }
        public bool IsPowerBIorSSDT => _connection?.IsPowerBIorSSDT ?? false;
        public bool IsPowerPivot {
            get => _connection?.IsPowerPivot ?? false;
            set
            {
                _connection.IsPowerPivot = value;
                _dmvConnection.IsPowerPivot = value;
            }
        }

        public void Open()
        {
            _connection.Open();
            _dmvConnection.Open();
        }
        public void Refresh()
        {
            if (_connection?.State == ConnectionState.Open) {
                _connection.Refresh();
                _dmvConnection.Refresh();
            }
        }
        public string ServerEdition => _connection.ServerEdition;
        public string ServerLocation => _connection.ServerLocation;
        public string ServerMode { get { return _connection.ServerMode; } }
        public string ServerName => _connection?.ServerName ?? string.Empty;
        public string ServerNameForHistory => !string.IsNullOrEmpty(FileName) ? "<Power BI>" : ServerName;
        public string ServerVersion => _connection.ServerVersion;
        public string SessionId => _connection.SessionId;
        public ServerType ServerType { get => _connection?.ServerType??ServerType.AnalysisServices; 
            private set {
                if (_connection == null) return;
                _connection.ServerType = value; 
            } 
        }

        public int SPID { get { return _connection?.State != ConnectionState.Open ? 0 : _connection?.SPID??0; } }
        public string ShortFileName => _connection.ShortFileName;

        public  bool ShouldAutoRefreshMetadata( IGlobalOptions options)
        {
            switch (_connection.ConnectionType)
            {
                case ADOTabularConnectionType.Cloud:
                    return options.AutoRefreshMetadataCloud;
                case ADOTabularConnectionType.LocalNetwork:
                    return options.AutoRefreshMetadataLocalNetwork;
                case ADOTabularConnectionType.LocalMachine:
                    return options.AutoRefreshMetadataLocalMachine;
                default:
                    return true;
            }
        }

        private ADOTabularFunctionGroupCollection _functionGroups;
        private DateTime _lastSchemaUpdate;

        public ADOTabularFunctionGroupCollection FunctionGroups
        {
            get
            {
                if (_functionGroups == null && _dmvConnection != null) _functionGroups = new ADOTabularFunctionGroupCollection(_dmvConnection);
                return _functionGroups;
            }
        }

        public ADOTabularDatabaseCollection GetDatabases()
        {
            return _dmvRetry.Execute(() => {
                return _dmvConnection.Databases;
            });
        }

        public ADOTabularModelCollection GetModels()
        {
            if (_dmvConnection == null) return null;
            if (_dmvConnection.State != ConnectionState.Open && _connection?.ServerType != ServerType.Offline) return null;
            return _dmvRetry.Execute(() => { return _dmvConnection?.Database?.Models; });
        }

        public ADOTabularTableCollection GetTables()
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(GetTables), "Start");
            return _dmvRetry.Execute(() =>
            {
                try
                {
                    var tables = _dmvConnection.Database.Models[SelectedModelName].Tables;
                    if (tables.Count == 0)
                    {
                        Log.Warning(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(GetTables), "No tables found in model");
                        _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, "No tables found in model"));
                    }
                    return tables;
                }
                catch 
                {
                    throw;
                }
                finally
                {
                    Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(GetTables), "End");
                }
            });
            
        }

        public AdomdType Type => AdomdType.AnalysisServices; // _connection.Type;

        //public string SelectedDatabaseName => SelectedDatabase?.Name ?? string.Empty;

        public string SelectedModelName { get; set; }

        private ADOTabularConnection _sampleDataConnection;
        private readonly object _sampleDataLock = new object();
        public async Task UpdateColumnSampleDataAsync(ITreeviewColumn column, int sampleSize, CancellationToken cancellationToken) 
        {

            column.UpdatingSampleData = true;
            try
            {
                await Task.Run(() => {
                    // cancel any existing sample data connection
                    _sampleDataConnection?.TryCancel();
                    //_sampleDataConnection?.TryClose();

                    if (column.SampleData.Count != 0) return; // if we already have sample data then don't do anything
                    lock (_sampleDataLock)
                    {
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(UpdateColumnSampleDataAsync), "Updating sample data");
                        using (_sampleDataConnection = _dmvConnection.Clone())
                        {
                            var sampleData = column.InternalColumn.GetSampleData(_sampleDataConnection, sampleSize);
                            Execute.OnUIThread(() =>
                            {
                                if (column.SampleData != null)
                                {
                                    foreach (var item in sampleData)
                                    {
                                        column.SampleData.Add(item);
                                    }
                                }
                            });
                        }
                    }
                }, cancellationToken);
            }
            catch (Microsoft.AnalysisServices.AdomdClient.AdomdErrorResponseException)
            {
                // An error response is expected if the tooltip is cancelled
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error populating tooltip sample data: {ex.Message}";
                Log.Warning(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(UpdateColumnSampleDataAsync), errorMsg);
                await _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, errorMsg));
            }
            finally
            {
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(UpdateColumnSampleDataAsync), "Setting UpdatingSampleData = False");
                column.UpdatingSampleData = false;
            }

        }


        public async Task<ICollection<string>> GetColumnSampleData(ADOTabularColumn column, int sampleSize)
        {
            if (column == null) return new List<string>();
            
            return await Task.Run(() =>
            {
                try
                {
                    using (var newConn = _dmvConnection.Clone())
                    {
                        return column.GetSampleData(newConn, sampleSize);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error getting sample data for column {ColumnName}", column.Name);
                    return new List<string>();
                }
            });
        }

        public void CancelUpdatingColumnSampleData()
        {
            if (_sampleDataConnection != null)
            {
                _sampleDataConnection.Cancel();
                _sampleDataConnection.Close();
                _sampleDataConnection.Dispose();
                _sampleDataConnection = null;
            }
        }

        private ADOTabularConnection _basicStatConnection;
        private readonly object _basicStatsLock = new object();
        public async Task UpdateColumnBasicStatsAsync(ITreeviewColumn column, CancellationToken cancellationToken)
        {

      
            column.UpdatingBasicStats = true;
            try
            {
                await Task.Run(() => {
                    
                     _basicStatConnection?.TryCancel(); // cancel any existing basic stats connection
                     //_basicStatConnection?.TryClose();

                    lock (_basicStatsLock)
                    {
                        using (_basicStatConnection = _dmvConnection.Clone())
                        {
                            column.InternalColumn.UpdateBasicStats(_basicStatConnection);
                            column.MinValue = column.InternalColumn.MinValue;
                            column.MaxValue = column.InternalColumn.MaxValue;
                            column.DistinctValues = column.InternalColumn.DistinctValues;
                        }
                    }
                }, cancellationToken); // add cancellation token
            }
            catch (Microsoft.AnalysisServices.AdomdClient.AdomdErrorResponseException)
            {
                // An error response is expected if the tooltip is cancelled
            }
            catch (Exception ex)
            {
                await _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, $"Error populating tooltip basic statistics data: {ex.Message}"));
            }
            finally
            {
                column.UpdatingBasicStats = false;
            }
        }

        public void CancelUpdatingColumnBasicStats()
        {
            if (_basicStatConnection != null)
            {
                _basicStatConnection.Cancel();
                _basicStatConnection.Close();
                _basicStatConnection.Dispose();
                _basicStatConnection = null;
            }
        }

        public async Task UpdateColumnBasicStatsAsync(ADOTabularColumn column)
        {
            if (column == null) return;
            await Task.Run(() =>
            {
                lock (_basicStatsLock)
                {
                    using (var conn = _dmvConnection.Clone())
                    {
                        column.UpdateBasicStats(conn);
                    }
                }
            });
        }

        public ADOTabularModelCollection ModelList { get; set; }
        public void Ping()
        {
            _retry.Execute(() =>
            {
                var tempConn = _connection.Clone(true);
                tempConn.Open();
                tempConn.Ping();
                tempConn.Close(false);
            });
        }

        public void PingTrace()
        {
            _retry.Execute(() =>
            {
                var tempConn = _connection.Clone(true);
                tempConn.Open();
                tempConn.PingTrace();
                tempConn.Close(false);
            });            
        }

        public void ClearCache()
        {
            if (IsTestingRls)
            {
                var tempConn = _connection.CloneWithoutRLS();
                //tempConn.Open();
                var tmpDb = tempConn.Database;
                tmpDb.ClearCache();
                tempConn.Close();
            }
            else
            {
                var db = _connection.Database;
                db.ClearCache();
            }
        }
        public ADOTabularModel SelectedModel { get; set; }

        public async Task SetSelectedModelAsync(ADOTabularModel model)
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedModelAsync), "Start");

            SelectedModel = model;
            

            if (SelectedModel != null)
            {
                SelectedModelName = model.Name;
                if (_connection.IsMultiDimensional)
                {
                    if (_connection.Is2012SP1OrLater)
                    {
                        _connection.SetCube(SelectedModel.Name);
                        _dmvConnection.SetCube(SelectedModel.Name);
                    }
                    else
                    {
                        await _eventAggregator.PublishAsync( 
                            new OutputMessage(MessageType.Error, 
                                $"DAX Studio can only connect to Multi-Dimensional servers running 2012 SP1 CU4 (11.0.3368.0) or later, this server reports a version number of {_connection.ServerVersion}")
                            );
                    }
                }
                // This allows us to move the loading of the table/column metadata onto a background thread
                await RefreshTablesAsync();
            }
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedModelAsync), "End");
        }

        public void SetSelectedDatabase(ADOTabularDatabase database)
        {
            if (_connection != null)
            {
                if (_connection.State == ConnectionState.Open || _connection.ServerType == ServerType.Offline )
                {
                    if (Database != null && database != null && _connection.Database.Name != database.Name) 
                    {
                        Log.Debug("{Class} {Event} {selectedDatabase}", "MetadataPaneViewModel", "SelectedDatabase:Set (changing)", database.Name);
                        _connection.ChangeDatabase(database.Name);
                        _dmvConnection.ChangeDatabase(database.Name);

                    }
                    if (_dmvConnection.Database != null)
                    {
                        ModelList = _dmvConnection.Database.Models;
                    }
                }
            }

            if (Database != database)
            {
                if (Database != null)
                {
                    _connection?.ChangeDatabase(Database.Name);
                    _dmvConnection?.ChangeDatabase(Database.Name);
                }

                if (_connection?.Database != null)
                    ModelList = _dmvConnection.Database.Models;

                PublishDatabaseChangedWhenStable();
            }

        }

        public void SetSelectedDatabase(string databaseName)
        {
            if (_connection != null)
            {
                if (_connection.State == ConnectionState.Open)
                {
                    if (Database != null && !string.IsNullOrEmpty( databaseName) && _connection.Database.Name != databaseName) 
                    {
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), $"{databaseName} (thread {System.Threading.Thread.CurrentThread.ManagedThreadId})");
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        _connection.ChangeDatabase(databaseName);
                        var queryMs = sw.ElapsedMilliseconds;
                        _dmvConnection.ChangeDatabase(databaseName);
                        sw.Stop();
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), $"ChangeDatabase '{databaseName}' complete (query conn: {queryMs}ms, dmv conn: {sw.ElapsedMilliseconds - queryMs}ms)");
                    }
                    else
                    {
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), $"{databaseName} - skipped ChangeDatabase (already selected)");
                    }
                    if (_dmvConnection.Database != null)
                    {
                        ModelList = _dmvConnection.Database.Models;
                    }
                    PublishDatabaseChangedWhenStable();
                }
            }

        }

        public List<ADOTabularMeasure> GetAllMeasures(string filterTable = null)
        {
            bool allTables = (string.IsNullOrEmpty(filterTable));
            var model = _dmvConnection.Database.Models.BaseModel;
            var modelMeasures = (from t in model.Tables
                                 from m in t.Measures
                                 where (allTables || t.Caption == filterTable)
                                 select m).ToList();
            return modelMeasures;
        }

        // TODO get roles on dmv connection
        public List<string> GetRoles()
        {
            var roleQuery = "select [Name] from $SYSTEM.TMSCHEMA_ROLES";
            var roleTable = ExecuteMetadataDaxQueryDataTable(roleQuery);
            var result = roleTable.AsEnumerable().Select(row => row[0].ToString()).ToList<string>();
            return result;
        }

        public string DefineFilterDumpMeasureExpression(string tableCaption, bool allTables)
        {

            var model = _dmvConnection.Database.Models.BaseModel;
            var distinctColumns = (from t in model.Tables
                                    from c in t.Columns
                                    where c.ObjectType == ADOTabularObjectType.Column
                                        && (allTables || t.Caption == tableCaption)
                                    select c).Distinct().ToList();
            string measureExpression = "\r\nVAR MaxFilters = 3\r\nRETURN\r\n";
            bool firstMeasure = true;
            foreach (var c in distinctColumns)
            {
                if (!firstMeasure) measureExpression += "\r\n & ";
                measureExpression += string.Format(@"IF ( 
    ISFILTERED ( {0}[{1}] ), 
    VAR ___f = FILTERS ( {0}[{1}] ) 
    VAR ___r = COUNTROWS ( ___f ) 
    VAR ___t = TOPN ( MaxFilters, ___f, {0}[{1}] )
    VAR ___d = CONCATENATEX ( ___t, {0}[{1}], "", "" )
    VAR ___x = ""{0}[{1}] = "" & ___d & IF(___r > MaxFilters, "", ... ["" & ___r & "" items selected]"") & "" "" 
    RETURN ___x & UNICHAR(13) & UNICHAR(10)
)", c.Table.DaxName, c.Name);
                    firstMeasure = false;
            }

            return measureExpression;
        }

        public string ExpandDependentMeasure(string measureName, bool ignoreNonUniqueMeasureNames)
        {

            var model = _dmvConnection.Database.Models.BaseModel;

            var dependentMeasures = FindDependentMeasures(measureName);

            var distinctColumns = (from t in model.Tables
                                   from c in t.Columns
                                   where c.ObjectType == ADOTabularObjectType.Column
                                   select c.Name).Distinct().ToList();

            var finalMeasure = dependentMeasures.First(m => m.Name == measureName);

            var resultExpression = finalMeasure.Expression;

            bool foundDependentMeasures;

            do
            {
                foundDependentMeasures = false;
                foreach (var modelMeasure in dependentMeasures)
                {
                    var escapedName = Regex.Escape(modelMeasure.Name).Replace("]", "]]");
                    var escapedTable = Regex.Escape(modelMeasure.Table.DaxName);

                    Regex daxMeasureRegex = new Regex($@"\[{ escapedName}]|'{escapedTable}'\[{escapedName}]|{escapedTable}\[{escapedName}]");
                    bool hasComments = modelMeasure.Expression.Contains(@"--");
                    string newExpression = daxMeasureRegex.Replace(resultExpression, $" CALCULATE ( { modelMeasure.Expression}{(hasComments ? "\r\n" : string.Empty)})");

                    if (newExpression != resultExpression)
                    {
                        resultExpression = newExpression;
                        foundDependentMeasures = true;
                        if (!ignoreNonUniqueMeasureNames)
                        {
                            if (distinctColumns.Contains(modelMeasure.Name))
                            {
                                // todo - prompt user to see whether to continue
                                var msg = "The measure name: '" + modelMeasure.Name + "' is also used as a column name in one or more of the tables in this model";
                                _eventAggregator.PublishAsync(new OutputMessage(MessageType.Warning, msg));
                                throw new InvalidOperationException(msg);
                            }
                        }
                    }

                }
            } while (foundDependentMeasures);

            return resultExpression;
        }

        public List<ADOTabularMeasure> FindDependentMeasures(string measureName)
        {
            if (!IsConnected)
            {
                // We do not support offline analysis of dependent measures
                // By using VPAX we could implement it by using the old algorithm with search/replace
                // but it would be better to wait for a tokenizer before implementing it
                throw new ApplicationException("Connection required to execute FindDependentMeasures");
            }

            // New algorithm using DEPENDENCY view
            // 
            // TODO we could pass a query or a string as a parameter,
            // so that if the entire query is used as a parameter we generate all the measures
            var modelMeasures = GetAllMeasures();
            
            var dependentMeasures = new List<ADOTabularMeasure>();

            Queue<ADOTabularMeasure> scanMeasures = new Queue<ADOTabularMeasure>();
            scanMeasures.Enqueue(modelMeasures.First(m => m.Name == measureName));

            // Track user-defined functions to process separately
            Queue<string> scanFunctions = new Queue<string>();
            HashSet<string> processedFunctions = new HashSet<string>();

            while (scanMeasures.Count > 0 || scanFunctions.Count > 0)
            {
                // Process measures
                if (scanMeasures.Count > 0)
                {
                    var measure = scanMeasures.Dequeue();
                    if (dependentMeasures.Where(item => item.Name == measure.Name).Any()) continue;
                    dependentMeasures.Add(measure);

                    // Query for both MEASURE and FUNCTION dependencies
                    var dmvDependency = $"SELECT REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT\r\nFROM $SYSTEM.DISCOVER_CALC_DEPENDENCY\r\nWHERE OBJECT='{measure.Name.Replace("'", "''")}' AND (REFERENCED_OBJECT_TYPE = 'MEASURE' OR REFERENCED_OBJECT_TYPE = 'FUNCTION')";

                    using (var dr = ExecuteReader(dmvDependency, null))
                    {
                        while (dr.Read())
                        {
                            var referencedObjectType = dr.GetString(0);
                            var referencedObjectName = dr.GetString(2);

                            if (referencedObjectType == "MEASURE")
                            {
                                if (!dependentMeasures.Where(item => item.Name == referencedObjectName).Any())
                                {
                                    var dependentMeasure = modelMeasures.First(m => m.Name == referencedObjectName);
                                    scanMeasures.Enqueue(dependentMeasure);
                                }
                            }
                            else if (referencedObjectType == "FUNCTION" && !processedFunctions.Contains(referencedObjectName))
                            {
                                // Add user-defined functions to be processed recursively
                                scanFunctions.Enqueue(referencedObjectName);
                                processedFunctions.Add(referencedObjectName);
                            }
                        }
                    }
                }

                // Process user-defined functions
                if (scanFunctions.Count > 0)
                {
                    var functionName = scanFunctions.Dequeue();

                    // Query dependencies of the user-defined function
                    var dmvFunctionDependency = $"SELECT REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT\r\nFROM $SYSTEM.DISCOVER_CALC_DEPENDENCY\r\nWHERE OBJECT='{functionName.Replace("'", "''")}' AND (REFERENCED_OBJECT_TYPE = 'MEASURE' OR REFERENCED_OBJECT_TYPE = 'FUNCTION')";

                    using (var dr = ExecuteReader(dmvFunctionDependency, null))
                    {
                        while (dr.Read())
                        {
                            var referencedObjectType = dr.GetString(0);
                            var referencedObjectName = dr.GetString(2);

                            if (referencedObjectType == "MEASURE")
                            {
                                if (!dependentMeasures.Where(item => item.Name == referencedObjectName).Any())
                                {
                                    var dependentMeasure = modelMeasures.First(m => m.Name == referencedObjectName);
                                    scanMeasures.Enqueue(dependentMeasure);
                                }
                            }
                            else if (referencedObjectType == "FUNCTION" && !processedFunctions.Contains(referencedObjectName))
                            {
                                // Recursively process nested user-defined functions
                                scanFunctions.Enqueue(referencedObjectName);
                                processedFunctions.Add(referencedObjectName);
                            }
                        }
                    }
                }
            }
            return dependentMeasures;
        }

        public List<ADOTabularMeasure> FindDependentMeasuresForQuery(string query, bool recursive)
        {
            if (!IsConnected)
            {
                // We do not support offline analysis of dependent measures
                // By using VPAX we could implement it by using the old algorithm with search/replace
                // but it would be better to wait for a tokenizer before implementing it
                throw new ApplicationException("Connection required to execute FindDependentMeasures");
            }

            var modelMeasures = GetAllMeasures();

            var dependentMeasures = new List<ADOTabularMeasure>();
            Queue<ADOTabularMeasure> scanMeasures = new Queue<ADOTabularMeasure>();

            // get all the measures referenced in the query
            var dmvQuery = $"SELECT REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT\r\nFROM $SYSTEM.DISCOVER_CALC_DEPENDENCY\r\nWHERE QUERY='{query.Replace("'", "''")}'";
            using (var dr = ExecuteReader(dmvQuery, null))
            {
                while (dr.Read())
                {
                    var referencedObjectType = dr.GetString(0);
                    if (referencedObjectType != "MEASURE") continue;
                    var referencedMeasureName = dr.GetString(2);
                    if (!dependentMeasures.Where(item => item.Name == referencedMeasureName).Any())
                    {
                        var dependentMeasure = modelMeasures.First(m => m.Name == referencedMeasureName);
                        scanMeasures.Enqueue(dependentMeasure);
                    }
                }
            }

            if (!recursive)
            {
                while (scanMeasures.Count > 0)
                {
                    var m = scanMeasures.Dequeue();
                    dependentMeasures.Add(m);
                }
                return dependentMeasures;
            }

            // recursively get all the measures that the measures referenced in the query depend on
            while (scanMeasures.Count > 0)
            {
                var measure = scanMeasures.Dequeue();
                if (dependentMeasures.Where(item => item.Name == measure.Name).Any()) continue;
                dependentMeasures.Add(measure);

                var dmvDependency = $"SELECT REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT\r\nFROM $SYSTEM.DISCOVER_CALC_DEPENDENCY\r\nWHERE OBJECT='{measure.Name.Replace("'", "''")}' AND REFERENCED_OBJECT_TYPE = 'MEASURE'";

                using (var dr = ExecuteReader(dmvDependency, null))
                {
                    while (dr.Read())
                    {
                        var referencedObjectType = dr.GetString(0);
                        if (referencedObjectType != "MEASURE") continue;
                        // var referencedTable = dr.GetString(1);
                        var referencedMeasureName = dr.GetString(2);
                        if (!dependentMeasures.Where(item => item.Name == referencedMeasureName).Any())
                        {
                            var dependentMeasure = modelMeasures.First(m => m.Name == referencedMeasureName);
                            scanMeasures.Enqueue(dependentMeasure);
                        }
                    }
                }
            }
            return dependentMeasures;
        }

        #region Comment Script "--> SHOW" tree builders

        /// <summary>
        /// Builds a hierarchical dependency tree for the objects referenced by the supplied query.
        /// The direct references of the query become the root nodes and each referenced object is
        /// recursively expanded (all object types) using the DISCOVER_CALC_DEPENDENCY DMV.
        /// </summary>
        public List<ShowTreeNode> BuildQueryDependencyTree(string query)
        {
            if (!IsConnected)
            {
                throw new ApplicationException("Connection required to show dependencies");
            }

            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildQueryDependencyTree), "start");

            var roots = new List<ShowTreeNode>();
            // dedupe roots by their stable identity to avoid repeating the same referenced object
            var rootKeys = new HashSet<string>();

            // lookups used to populate the Expression column and to resolve/expand references found by
            // parsing query-scoped function bodies (which the DMV cannot see into)
            var ctx = new DependencyContext
            {
                MeasureExpressions = BuildMeasureExpressionLookup(),
                FunctionExpressions = BuildFunctionExpressionLookup(),
                ColumnTables = BuildColumnTableLookup(),
                QueryFunctionExpressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                QueryFunctionReferences = new Dictionary<string, IReadOnlyList<DaxStudio.Parsers.Metadata.DaxObjectReference>>(StringComparer.OrdinalIgnoreCase)
            };

            var dmvQuery = $"SELECT OBJECT_TYPE, OBJECT, REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT\r\nFROM $SYSTEM.DISCOVER_CALC_DEPENDENCY\r\nWHERE QUERY='{query.Replace("'", "''")}'";
            using (var dr = ExecuteReader(dmvQuery, null))
            {
                int refTypeOrd = OrdinalOrMinusOne(dr, "REFERENCED_OBJECT_TYPE");
                int refTableOrd = OrdinalOrMinusOne(dr, "REFERENCED_TABLE");
                int refObjectOrd = OrdinalOrMinusOne(dr, "REFERENCED_OBJECT");
                while (dr.Read())
                {
                    var refObject = GetStringOrNull(dr, refObjectOrd);
                    if (string.IsNullOrEmpty(refObject)) continue;
                    var refType = GetStringOrNull(dr, refTypeOrd);
                    var refTable = GetStringOrNull(dr, refTableOrd);
                    if (rootKeys.Add(NodeIdentity(refType, refTable, refObject)))
                    {
                        var node = new ShowTreeNode(refObject, refType, refTable);
                        SetNodeExpression(node, ctx.MeasureExpressions, ctx.FunctionExpressions);
                        roots.Add(node);
                    }
                }
            }

            // Query-scoped user-defined functions (DEFINE FUNCTION ...) are not reported by the
            // DISCOVER_CALC_DEPENDENCY DMV, so parse the query itself to surface them (with their body
            // and the objects they reference).
            AddQueryScopedFunctions(query, roots, rootKeys, ctx);

            var expanded = new HashSet<string>();
            foreach (var root in roots)
            {
                ExpandDependencyNode(root, expanded, ctx);
            }

            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildQueryDependencyTree), $"end - {roots.Count} root(s)");
            return roots;
        }

        /// <summary>Holds the metadata lookups shared across a single dependency-tree build.</summary>
        private class DependencyContext
        {
            /// <summary>Model measure name -&gt; DAX expression.</summary>
            public IReadOnlyDictionary<string, string> MeasureExpressions { get; set; }
            /// <summary>Model user-defined function name -&gt; DAX expression (from TMSCHEMA_FUNCTIONS).</summary>
            public IReadOnlyDictionary<string, string> FunctionExpressions { get; set; }
            /// <summary>Column name -&gt; the table(s) that contain a column of that name (for resolving bare refs).</summary>
            public IReadOnlyDictionary<string, List<string>> ColumnTables { get; set; }
            /// <summary>Query-scoped function name -&gt; its <c>(params) =&gt; body</c> definition text.</summary>
            public Dictionary<string, string> QueryFunctionExpressions { get; set; }
            /// <summary>Query-scoped function name -&gt; the references parsed from its body.</summary>
            public Dictionary<string, IReadOnlyList<DaxStudio.Parsers.Metadata.DaxObjectReference>> QueryFunctionReferences { get; set; }
        }

        /// <summary>
        /// Builds a case-insensitive lookup of measure name to DAX expression from the connected model,
        /// used to populate the Expression column of the dependency tree. Returns an empty dictionary
        /// when the measures cannot be read.
        /// </summary>
        private Dictionary<string, string> BuildMeasureExpressionLookup()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var m in GetAllMeasures())
                {
                    if (!string.IsNullOrEmpty(m.Name) && !dict.ContainsKey(m.Name))
                        dict.Add(m.Name, m.Expression ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildMeasureExpressionLookup), "unable to read model measures - Expression column will be blank for measures");
            }
            return dict;
        }

        /// <summary>
        /// Builds a case-insensitive lookup of user-defined function name to DAX expression from the
        /// TMSCHEMA_FUNCTIONS DMV (present only on newer engines). Returns an empty dictionary when the
        /// DMV is not available or cannot be read.
        /// </summary>
        private Dictionary<string, string> BuildFunctionExpressionLookup()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var dmvs = DynamicManagementViews;
            if (dmvs == null || !dmvs.Contains("TMSCHEMA_FUNCTIONS")) return dict;
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_FUNCTIONS", null))
                {
                    int nameOrd = OrdinalOrMinusOne(dr, "Name");
                    int exprOrd = OrdinalOrMinusOne(dr, "Expression");
                    while (dr.Read())
                    {
                        var name = GetStringOrNull(dr, nameOrd);
                        if (string.IsNullOrEmpty(name) || dict.ContainsKey(name)) continue;
                        dict.Add(name, GetStringOrNull(dr, exprOrd) ?? string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildFunctionExpressionLookup), "unable to read TMSCHEMA_FUNCTIONS - Expression column will be blank for functions");
            }
            return dict;
        }

        /// <summary>
        /// Builds a case-insensitive lookup of column name to the list of table(s) that contain a column
        /// of that name, used to resolve a bare <c>[Column]</c> reference (which carries no table) found
        /// while parsing a query-scoped function body. Returns an empty dictionary when the model columns
        /// cannot be read.
        /// </summary>
        private Dictionary<string, List<string>> BuildColumnTableLookup()
        {
            var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var model = _dmvConnection.Database.Models.BaseModel;
                foreach (var t in model.Tables)
                {
                    foreach (var c in t.Columns)
                    {
                        if (c.ObjectType != ADOTabularObjectType.Column || string.IsNullOrEmpty(c.Name)) continue;
                        if (!dict.TryGetValue(c.Name, out var tables))
                        {
                            tables = new List<string>();
                            dict.Add(c.Name, tables);
                        }
                        if (!tables.Contains(t.Caption)) tables.Add(t.Caption);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildColumnTableLookup), "unable to read model columns - bare column references in query functions may be skipped");
            }
            return dict;
        }

        /// <summary>
        /// Sets <see cref="ShowTreeNode.Expression"/> for MEASURE and FUNCTION nodes from the supplied
        /// lookups. Nodes of other types (columns, tables, ...) are left unchanged.
        /// </summary>
        private static void SetNodeExpression(ShowTreeNode node, IReadOnlyDictionary<string, string> measureExpressions, IReadOnlyDictionary<string, string> functionExpressions)
        {
            if (node == null || string.IsNullOrEmpty(node.ObjectType) || string.IsNullOrEmpty(node.Name)) return;
            if (string.Equals(node.ObjectType, "MEASURE", StringComparison.OrdinalIgnoreCase))
            {
                if (measureExpressions != null && measureExpressions.TryGetValue(node.Name, out var me)) node.Expression = me;
            }
            else if (string.Equals(node.ObjectType, "FUNCTION", StringComparison.OrdinalIgnoreCase))
            {
                if (functionExpressions != null && functionExpressions.TryGetValue(node.Name, out var fe)) node.Expression = fe;
            }
        }

        /// <summary>
        /// Parses the supplied query for query-scoped user-defined functions (DEFINE FUNCTION ...) and adds
        /// each one as a root node with the object type <c>QUERY_FUNCTION</c> and its full definition
        /// (<c>(params) =&gt; body</c>) in the Expression column. When the DMV already surfaced a function of
        /// the same name it is re-labelled as a query function (rather than duplicated) and its expression
        /// is backfilled from the parsed definition. Each function's parsed body references are recorded on
        /// <paramref name="ctx"/> so the tree can be extended with the objects the function depends on.
        /// </summary>
        private void AddQueryScopedFunctions(string query, List<ShowTreeNode> roots, HashSet<string> rootKeys, DependencyContext ctx)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            try
            {
                var parser = new DaxStudio.Parsers.Dax.DaxParserService(null);
                var definedFunctions = parser.GetDefinedFunctions(query);
                // function names actually called in the query outside of any DEFINE FUNCTION body -
                // functions that are declared but never invoked must not appear in the tree.
                var calledAtTopLevel = parser.GetReferencedFunctionNames(query);
                // columns/measures/functions referenced in the arguments passed to each function at its
                // call site(s) - e.g. 'Product'[Color] in queryFunc(VALUES('Product'[Color])).
                var callSiteReferences = parser.GetFunctionCallArgumentReferences(query);
                foreach (var fn in definedFunctions)
                {
                    if (fn == null || string.IsNullOrEmpty(fn.Name)) continue;

                    // record the definition and the combined references (the function body plus whatever is
                    // passed to it at its call site) so QUERY_FUNCTION nodes can be expanded whether they are
                    // added here as a root or later as the child of another function.
                    ctx.QueryFunctionExpressions[fn.Name] = fn.Expression;
                    ctx.QueryFunctionReferences[fn.Name] = MergeReferences(fn.References,
                        callSiteReferences != null && callSiteReferences.TryGetValue(fn.Name, out var args) ? args : null);

                    // only functions actually called from the query itself become roots; ones referenced
                    // solely by another function are added as that function's children during expansion.
                    if (calledAtTopLevel == null || !calledAtTopLevel.Contains(fn.Name)) continue;

                    // Reuse an existing root for the same function name (the DMV may report it as a FUNCTION)
                    // so a query-scoped function is never shown twice.
                    var existing = roots.FirstOrDefault(r =>
                        string.Equals(r.Name, fn.Name, StringComparison.OrdinalIgnoreCase)
                        && (string.Equals(r.ObjectType, "FUNCTION", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(r.ObjectType, "QUERY_FUNCTION", StringComparison.OrdinalIgnoreCase)));
                    if (existing != null)
                    {
                        existing.ObjectType = "QUERY_FUNCTION";
                        existing.Expression = fn.Expression;
                        continue;
                    }

                    var key = NodeIdentity("QUERY_FUNCTION", null, fn.Name);
                    if (!rootKeys.Add(key)) continue;
                    roots.Add(new ShowTreeNode(fn.Name, "QUERY_FUNCTION", null) { Expression = fn.Expression });
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(AddQueryScopedFunctions), "unable to parse query-scoped functions");
            }
        }

        /// <summary>
        /// Combines a query-scoped function's body references with the references passed to it at its call
        /// site(s), de-duplicated by kind/table/name. Either input may be null.
        /// </summary>
        private static IReadOnlyList<DaxStudio.Parsers.Metadata.DaxObjectReference> MergeReferences(
            IReadOnlyList<DaxStudio.Parsers.Metadata.DaxObjectReference> bodyReferences,
            IReadOnlyList<DaxStudio.Parsers.Metadata.DaxObjectReference> callSiteReferences)
        {
            var merged = new List<DaxStudio.Parsers.Metadata.DaxObjectReference>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddRange(IReadOnlyList<DaxStudio.Parsers.Metadata.DaxObjectReference> references)
            {
                if (references == null) return;
                foreach (var r in references)
                {
                    if (r == null) continue;
                    var key = $"{r.Kind}|{r.Table}|{r.Name}";
                    if (seen.Add(key)) merged.Add(r);
                }
            }

            AddRange(bodyReferences);
            AddRange(callSiteReferences);
            return merged;
        }

        /// <summary>
        /// Returns the distinct set of tables referenced (directly or indirectly) by the supplied
        /// query. Reuses <see cref="BuildQueryDependencyTree"/> and flattens the resulting tree,
        /// collecting every non-blank <see cref="ShowTreeNode.TableName"/>. Used by the
        /// "--> SHOW DIAGRAM" comment-script command to filter the Model Diagram.
        /// </summary>
        public List<string> GetQueryDependencyTables(string query)
        {
            var tables = new List<string>();
            if (string.IsNullOrWhiteSpace(query)) return tables;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roots = BuildQueryDependencyTree(query);
            var stack = new Stack<ShowTreeNode>(roots);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node == null) continue;
                if (!string.IsNullOrWhiteSpace(node.TableName) && seen.Add(node.TableName))
                {
                    tables.Add(node.TableName);
                }
                foreach (var child in node.Children)
                {
                    stack.Push(child);
                }
            }

            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(GetQueryDependencyTables), $"end - {tables.Count} table(s)");
            return tables;
        }

        /// <summary>
        /// Recursively expands a dependency node by querying the objects it references. Uses the
        /// <paramref name="expanded"/> set (keyed by a stable identity) to guarantee a finite tree:
        /// an object that has already been expanded elsewhere is still added but not re-expanded.
        /// </summary>
        private void ExpandDependencyNode(ShowTreeNode node, HashSet<string> expanded, DependencyContext ctx)
        {
            var key = NodeIdentity(node.ObjectType, node.TableName, node.Name);
            // if this identity was already fully expanded, keep the node but do not recurse (avoids cycles)
            if (!expanded.Add(key)) return;

            // Query-scoped functions are not in the model, so the DMV knows nothing about them. Expand them
            // from the references parsed out of their body instead.
            if (string.Equals(node.ObjectType, "QUERY_FUNCTION", StringComparison.OrdinalIgnoreCase))
            {
                ExpandQueryFunctionNode(node, expanded, ctx);
                return;
            }

            var where = $"[OBJECT]='{node.Name.Replace("'", "''")}'";
            if (!string.IsNullOrEmpty(node.ObjectType)) where += $" AND [OBJECT_TYPE]='{node.ObjectType.Replace("'", "''")}'";
            if (!string.IsNullOrEmpty(node.TableName)) where += $" AND [TABLE]='{node.TableName.Replace("'", "''")}'";

            var dmvDependency = $"SELECT REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT\r\nFROM $SYSTEM.DISCOVER_CALC_DEPENDENCY\r\nWHERE {where}";

            // materialize the children before recursing because the reader holds a live connection
            var children = new List<ShowTreeNode>();
            using (var dr = ExecuteReader(dmvDependency, null))
            {
                int refTypeOrd = OrdinalOrMinusOne(dr, "REFERENCED_OBJECT_TYPE");
                int refTableOrd = OrdinalOrMinusOne(dr, "REFERENCED_TABLE");
                int refObjectOrd = OrdinalOrMinusOne(dr, "REFERENCED_OBJECT");
                while (dr.Read())
                {
                    var refObject = GetStringOrNull(dr, refObjectOrd);
                    if (string.IsNullOrEmpty(refObject)) continue;
                    var refType = GetStringOrNull(dr, refTypeOrd);
                    var refTable = GetStringOrNull(dr, refTableOrd);
                    // skip a direct self-reference
                    if (NodeIdentity(refType, refTable, refObject) == key) continue;
                    var child = new ShowTreeNode(refObject, refType, refTable);
                    SetNodeExpression(child, ctx.MeasureExpressions, ctx.FunctionExpressions);
                    children.Add(child);
                }
            }

            foreach (var child in children)
            {
                node.Children.Add(child);
                ExpandDependencyNode(child, expanded, ctx);
            }
        }

        /// <summary>
        /// Expands a <c>QUERY_FUNCTION</c> node using the column / measure / function references parsed from
        /// its body (the DMV cannot see into query-scoped functions). Each reference is resolved against the
        /// model - bare <c>[Name]</c> references become a MEASURE or COLUMN, function calls become a
        /// QUERY_FUNCTION (another query-scoped function), a model FUNCTION, or are ignored when they are not
        /// user-defined objects - and each resolved child is expanded recursively via the normal path.
        /// </summary>
        private void ExpandQueryFunctionNode(ShowTreeNode node, HashSet<string> expanded, DependencyContext ctx)
        {
            if (ctx.QueryFunctionReferences == null || !ctx.QueryFunctionReferences.TryGetValue(node.Name, out var references) || references == null)
                return;

            var children = new List<ShowTreeNode>();
            var childKeys = new HashSet<string>();
            foreach (var reference in references)
            {
                foreach (var child in ResolveReferenceNodes(reference, ctx))
                {
                    var childKey = NodeIdentity(child.ObjectType, child.TableName, child.Name);
                    // skip a direct self-reference and duplicates among this function's own children
                    if (childKey == NodeIdentity(node.ObjectType, node.TableName, node.Name)) continue;
                    if (childKeys.Add(childKey)) children.Add(child);
                }
            }

            foreach (var child in children)
            {
                node.Children.Add(child);
                ExpandDependencyNode(child, expanded, ctx);
            }
        }

        /// <summary>
        /// Resolves a single parsed reference from a query-function body into zero or more dependency nodes,
        /// classifying it against the model: a qualified column becomes a COLUMN; a bare reference becomes a
        /// MEASURE (when it matches a model measure) or a COLUMN for each table that has a column of that
        /// name; a function call becomes a QUERY_FUNCTION or a model FUNCTION. References that resolve to no
        /// known object (e.g. built-ins already filtered out, or a local variable) yield nothing.
        /// </summary>
        private IEnumerable<ShowTreeNode> ResolveReferenceNodes(DaxStudio.Parsers.Metadata.DaxObjectReference reference, DependencyContext ctx)
        {
            switch (reference.Kind)
            {
                case DaxStudio.Parsers.Metadata.DaxReferenceKind.Column:
                    yield return new ShowTreeNode(reference.Name, "COLUMN", reference.Table);
                    break;

                case DaxStudio.Parsers.Metadata.DaxReferenceKind.ColumnOrMeasure:
                    if (ctx.MeasureExpressions != null && ctx.MeasureExpressions.ContainsKey(reference.Name))
                    {
                        var measure = new ShowTreeNode(reference.Name, "MEASURE", null);
                        SetNodeExpression(measure, ctx.MeasureExpressions, ctx.FunctionExpressions);
                        yield return measure;
                    }
                    else if (ctx.ColumnTables != null && ctx.ColumnTables.TryGetValue(reference.Name, out var tables))
                    {
                        foreach (var table in tables)
                            yield return new ShowTreeNode(reference.Name, "COLUMN", table);
                    }
                    break;

                case DaxStudio.Parsers.Metadata.DaxReferenceKind.Function:
                    if (ctx.QueryFunctionExpressions != null && ctx.QueryFunctionExpressions.TryGetValue(reference.Name, out var qfExpr))
                    {
                        yield return new ShowTreeNode(reference.Name, "QUERY_FUNCTION", null) { Expression = qfExpr };
                    }
                    else if (ctx.FunctionExpressions != null && ctx.FunctionExpressions.ContainsKey(reference.Name))
                    {
                        var fn = new ShowTreeNode(reference.Name, "FUNCTION", null);
                        SetNodeExpression(fn, ctx.MeasureExpressions, ctx.FunctionExpressions);
                        yield return fn;
                    }
                    break;
            }
        }

        /// <summary>
        /// Builds a tree of the model metadata that mirrors the Power BI Desktop model view - a single
        /// "Semantic model" root with grouping folders (Calculation groups, Cultures, Expressions,
        /// Functions, Perspectives, Relationships, Roles, Tables) and, under each table, Calendars,
        /// Columns, Hierarchies, Measures and Partitions folders - annotated with each item's last-modified
        /// timestamp from the TMSCHEMA DMVs. Newer/optional DMVs (Functions, Calendars) are omitted when
        /// the model does not support them. Every node is rolled up with the most-recent modified time of
        /// its descendants (<see cref="ShowTreeNode.MaxUpdateUtc"/>) and the number of whole days since that
        /// effective change (<see cref="ShowTreeNode.DaysSinceChange"/>). When <paramref name="maxOnly"/> is
        /// true the tree is pruned to only the object(s) whose timestamp equals the single global maximum,
        /// keeping the enclosing folders/tables for context.
        /// </summary>
        public List<ShowTreeNode> BuildMetadataTimestampTree(bool maxOnly)
        {
            if (!IsConnected)
            {
                throw new ApplicationException("Connection required to show metadata timestamps");
            }

            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildMetadataTimestampTree), $"start (maxOnly={maxOnly})");

            var dmvs = DynamicManagementViews;
            bool HasDmv(string name) => dmvs != null && dmvs.Contains(name);

            // --- Model root (TMSCHEMA_MODEL) ---
            var modelRoot = new ShowTreeNode("Semantic model", "MODEL", null, null);
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_MODEL", null))
                {
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    int structOrd = OrdinalOrMinusOne(dr, "StructureModifiedTime");
                    if (dr.Read())
                    {
                        modelRoot.LastModifiedUtc = PreferStructure(GetDateTimeOrNull(dr, modOrd), GetDateTimeOrNull(dr, structOrd));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_MODEL", ex); }

            // --- Tables (own timestamps) ---
            var tableNodes = new Dictionary<string, ShowTreeNode>();
            var tableNames = new Dictionary<string, string>();
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_TABLES", null))
                {
                    int idOrd = OrdinalOrMinusOne(dr, "ID");
                    int nameOrd = OrdinalOrMinusOne(dr, "Name");
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    int structOrd = OrdinalOrMinusOne(dr, "StructureModifiedTime");
                    while (dr.Read())
                    {
                        var id = GetStringOrNull(dr, idOrd);
                        var name = GetStringOrNull(dr, nameOrd);
                        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name)) continue;
                        var ts = PreferStructure(GetDateTimeOrNull(dr, modOrd), GetDateTimeOrNull(dr, structOrd));
                        tableNodes[id] = new ShowTreeNode(name, "TABLE", null, ts);
                        tableNames[id] = name;
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_TABLES", ex); }

            // per-table leaf collections keyed by table id
            var tableColumns = new Dictionary<string, List<ShowTreeNode>>();
            var tableMeasures = new Dictionary<string, List<ShowTreeNode>>();
            var tablePartitions = new Dictionary<string, List<ShowTreeNode>>();
            var tableHierarchies = new Dictionary<string, List<ShowTreeNode>>();
            var tableCalendars = new Dictionary<string, List<ShowTreeNode>>();
            List<ShowTreeNode> TableList(Dictionary<string, List<ShowTreeNode>> map, string tableId)
            {
                if (!map.TryGetValue(tableId, out var list)) { list = new List<ShowTreeNode>(); map[tableId] = list; }
                return list;
            }

            var columnNames = new Dictionary<string, string>(); // column id -> name (used to label relationships)

            // --- Columns ---
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_COLUMNS", null))
                {
                    int idOrd = OrdinalOrMinusOne(dr, "ID");
                    int tableIdOrd = OrdinalOrMinusOne(dr, "TableID");
                    int explicitNameOrd = OrdinalOrMinusOne(dr, "ExplicitName");
                    int inferredNameOrd = OrdinalOrMinusOne(dr, "InferredName");
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    int structOrd = OrdinalOrMinusOne(dr, "StructureModifiedTime");
                    while (dr.Read())
                    {
                        var id = GetStringOrNull(dr, idOrd);
                        var tableId = GetStringOrNull(dr, tableIdOrd);
                        var name = GetStringOrNull(dr, explicitNameOrd) ?? GetStringOrNull(dr, inferredNameOrd);
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(name)) columnNames[id] = name;
                        if (string.IsNullOrEmpty(tableId) || !tableNodes.ContainsKey(tableId)) continue;
                        if (string.IsNullOrEmpty(name)) continue; // skip null-named (e.g. system RowNumber) columns
                        var ts = PreferStructure(GetDateTimeOrNull(dr, modOrd), GetDateTimeOrNull(dr, structOrd));
                        TableList(tableColumns, tableId).Add(new ShowTreeNode(name, "COLUMN", tableNames[tableId], ts));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_COLUMNS", ex); }

            // --- Measures ---
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_MEASURES", null))
                {
                    int tableIdOrd = OrdinalOrMinusOne(dr, "TableID");
                    int nameOrd = OrdinalOrMinusOne(dr, "Name");
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    while (dr.Read())
                    {
                        var tableId = GetStringOrNull(dr, tableIdOrd);
                        if (string.IsNullOrEmpty(tableId) || !tableNodes.ContainsKey(tableId)) continue;
                        var name = GetStringOrNull(dr, nameOrd);
                        if (string.IsNullOrEmpty(name)) continue;
                        TableList(tableMeasures, tableId).Add(new ShowTreeNode(name, "MEASURE", tableNames[tableId], GetDateTimeOrNull(dr, modOrd)));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_MEASURES", ex); }

            // --- Partitions ---
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_PARTITIONS", null))
                {
                    int tableIdOrd = OrdinalOrMinusOne(dr, "TableID");
                    int nameOrd = OrdinalOrMinusOne(dr, "Name");
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    int refreshedOrd = OrdinalOrMinusOne(dr, "RefreshedTime");
                    while (dr.Read())
                    {
                        var tableId = GetStringOrNull(dr, tableIdOrd);
                        if (string.IsNullOrEmpty(tableId) || !tableNodes.ContainsKey(tableId)) continue;
                        var name = GetStringOrNull(dr, nameOrd);
                        if (string.IsNullOrEmpty(name)) continue;
                        // Partitions have no StructureModifiedTime; RefreshedTime (last data refresh) is meaningful here.
                        var ts = MaxDate(GetDateTimeOrNull(dr, modOrd), GetDateTimeOrNull(dr, refreshedOrd));
                        TableList(tablePartitions, tableId).Add(new ShowTreeNode(name, "PARTITION", tableNames[tableId], ts));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_PARTITIONS", ex); }

            // --- Hierarchies ---
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_HIERARCHIES", null))
                {
                    int tableIdOrd = OrdinalOrMinusOne(dr, "TableID");
                    int nameOrd = OrdinalOrMinusOne(dr, "Name");
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    int structOrd = OrdinalOrMinusOne(dr, "StructureModifiedTime");
                    while (dr.Read())
                    {
                        var tableId = GetStringOrNull(dr, tableIdOrd);
                        if (string.IsNullOrEmpty(tableId) || !tableNodes.ContainsKey(tableId)) continue;
                        var name = GetStringOrNull(dr, nameOrd);
                        if (string.IsNullOrEmpty(name)) continue;
                        var ts = PreferStructure(GetDateTimeOrNull(dr, modOrd), GetDateTimeOrNull(dr, structOrd));
                        TableList(tableHierarchies, tableId).Add(new ShowTreeNode(name, "HIERARCHY", tableNames[tableId], ts));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_HIERARCHIES", ex); }

            // --- Calendars (newer models only) ---
            bool calendarsSupported = HasDmv("TMSCHEMA_CALENDARS");
            if (calendarsSupported)
            {
                try
                {
                    using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_CALENDARS", null))
                    {
                        int tableIdOrd = OrdinalOrMinusOne(dr, "TableID");
                        int nameOrd = OrdinalOrMinusOne(dr, "Name");
                        int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                        while (dr.Read())
                        {
                            var tableId = GetStringOrNull(dr, tableIdOrd);
                            if (string.IsNullOrEmpty(tableId) || !tableNodes.ContainsKey(tableId)) continue;
                            var name = GetStringOrNull(dr, nameOrd);
                            if (string.IsNullOrEmpty(name)) continue;
                            TableList(tableCalendars, tableId).Add(new ShowTreeNode(name, "CALENDAR", tableNames[tableId], GetDateTimeOrNull(dr, modOrd)));
                        }
                    }
                }
                catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_CALENDARS", ex); calendarsSupported = false; }
            }

            // Assemble each table node with its child folders (Desktop order)
            foreach (var kvp in tableNodes)
            {
                var tableId = kvp.Key;
                var tableNode = kvp.Value;
                if (calendarsSupported) tableNode.Children.Add(MakeFolder("Calendars", TableList(tableCalendars, tableId)));
                tableNode.Children.Add(MakeFolder("Columns", TableList(tableColumns, tableId)));
                tableNode.Children.Add(MakeFolder("Hierarchies", TableList(tableHierarchies, tableId)));
                tableNode.Children.Add(MakeFolder("Measures", TableList(tableMeasures, tableId)));
                tableNode.Children.Add(MakeFolder("Partitions", TableList(tablePartitions, tableId)));
            }

            // --- Model-level object collections ---
            var calcGroups = new List<ShowTreeNode>();
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_CALCULATION_GROUPS", null))
                {
                    int idOrd = OrdinalOrMinusOne(dr, "ID");
                    int tableIdOrd = OrdinalOrMinusOne(dr, "TableID");
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    while (dr.Read())
                    {
                        var tableId = GetStringOrNull(dr, tableIdOrd);
                        var name = (!string.IsNullOrEmpty(tableId) && tableNames.TryGetValue(tableId, out var tn))
                            ? tn : GetStringOrNull(dr, idOrd);
                        if (string.IsNullOrEmpty(name)) continue;
                        calcGroups.Add(new ShowTreeNode(name, "CALCULATION GROUP", null, GetDateTimeOrNull(dr, modOrd)));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_CALCULATION_GROUPS", ex); }

            var cultures = ReadModelLevelObjects("TMSCHEMA_CULTURES", "Name", "CULTURE");
            var expressions = ReadModelLevelObjects("TMSCHEMA_EXPRESSIONS", "Name", "EXPRESSION");
            var perspectives = ReadModelLevelObjects("TMSCHEMA_PERSPECTIVES", "Name", "PERSPECTIVE");
            var roles = ReadModelLevelObjects("TMSCHEMA_ROLES", "Name", "ROLE");

            // --- Relationships (friendly From/To label) ---
            var relationships = new List<ShowTreeNode>();
            try
            {
                using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.TMSCHEMA_RELATIONSHIPS", null))
                {
                    int fromTableOrd = OrdinalOrMinusOne(dr, "FromTableID");
                    int fromColOrd = OrdinalOrMinusOne(dr, "FromColumnID");
                    int toTableOrd = OrdinalOrMinusOne(dr, "ToTableID");
                    int toColOrd = OrdinalOrMinusOne(dr, "ToColumnID");
                    int nameOrd = OrdinalOrMinusOne(dr, "Name");
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    while (dr.Read())
                    {
                        var label = RelationshipLabel(dr, fromTableOrd, fromColOrd, toTableOrd, toColOrd, tableNames, columnNames)
                                    ?? GetStringOrNull(dr, nameOrd);
                        if (string.IsNullOrEmpty(label)) continue;
                        relationships.Add(new ShowTreeNode(label, "RELATIONSHIP", null, GetDateTimeOrNull(dr, modOrd)));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning("TMSCHEMA_RELATIONSHIPS", ex); }

            // --- Functions / user-defined functions (newer models only) ---
            bool functionsSupported = HasDmv("TMSCHEMA_FUNCTIONS");
            var functions = functionsSupported
                ? ReadModelLevelObjects("TMSCHEMA_FUNCTIONS", "Name", "FUNCTION")
                : new List<ShowTreeNode>();

            // Assemble model root groups (Desktop order)
            modelRoot.Children.Add(MakeFolder("Calculation groups", calcGroups));
            modelRoot.Children.Add(MakeFolder("Cultures", cultures));
            modelRoot.Children.Add(MakeFolder("Expressions", expressions));
            if (functionsSupported) modelRoot.Children.Add(MakeFolder("Functions", functions));
            modelRoot.Children.Add(MakeFolder("Perspectives", perspectives));
            modelRoot.Children.Add(MakeFolder("Relationships", relationships));
            modelRoot.Children.Add(MakeFolder("Roles", roles));

            var tablesFolder = new ShowTreeNode("Tables", string.Empty, null, null, isFolder: true);
            foreach (var t in tableNodes.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
            {
                tablesFolder.Children.Add(t);
            }
            modelRoot.Children.Add(tablesFolder);

            // Roll up MaxUpdate / DaysSinceChange across the whole tree
            ComputeRollups(modelRoot, DateTime.UtcNow);

            if (!maxOnly)
            {
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildMetadataTimestampTree), $"end - {tableNodes.Count} table(s)");
                return new List<ShowTreeNode> { modelRoot };
            }

            // MAX_UPDATED: prune to the object(s) carrying the single global maximum timestamp
            DateTime? globalMax = null;
            CollectMaxObjectTimestamp(modelRoot, ref globalMax);
            if (!globalMax.HasValue)
            {
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildMetadataTimestampTree), "end - no timestamps found");
                return new List<ShowTreeNode>();
            }
            PruneToMax(modelRoot, globalMax.Value);
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildMetadataTimestampTree), $"end - max={globalMax:o}");
            return new List<ShowTreeNode> { modelRoot };
        }

        /// <summary>Reads a flat list of model-level objects (name + ModifiedTime) from a TMSCHEMA DMV,
        /// swallowing any error (missing DMV / permissions) and returning what was read so far.</summary>
        private List<ShowTreeNode> ReadModelLevelObjects(string dmv, string nameColumn, string objectType)
        {
            var list = new List<ShowTreeNode>();
            try
            {
                using (var dr = ExecuteReader($"SELECT * FROM $SYSTEM.{dmv}", null))
                {
                    int nameOrd = OrdinalOrMinusOne(dr, nameColumn);
                    int modOrd = OrdinalOrMinusOne(dr, "ModifiedTime");
                    while (dr.Read())
                    {
                        var name = GetStringOrNull(dr, nameOrd);
                        if (string.IsNullOrEmpty(name)) continue;
                        list.Add(new ShowTreeNode(name, objectType, null, GetDateTimeOrNull(dr, modOrd)));
                    }
                }
            }
            catch (Exception ex) { LogTimestampDmvWarning(dmv, ex); }
            return list;
        }

        /// <summary>Builds a "From[Column] &lt;- To[Column]" relationship label, or null when it cannot be resolved.</summary>
        private static string RelationshipLabel(AdomdDataReader dr, int fromTableOrd, int fromColOrd, int toTableOrd, int toColOrd,
            Dictionary<string, string> tableNames, Dictionary<string, string> columnNames)
        {
            var fromTableId = GetStringOrNull(dr, fromTableOrd);
            var toTableId = GetStringOrNull(dr, toTableOrd);
            if (string.IsNullOrEmpty(fromTableId) || string.IsNullOrEmpty(toTableId)) return null;
            if (!tableNames.TryGetValue(fromTableId, out var fromTable) || string.IsNullOrEmpty(fromTable)) return null;
            if (!tableNames.TryGetValue(toTableId, out var toTable) || string.IsNullOrEmpty(toTable)) return null;
            columnNames.TryGetValue(GetStringOrNull(dr, fromColOrd) ?? string.Empty, out var fromColumn);
            columnNames.TryGetValue(GetStringOrNull(dr, toColOrd) ?? string.Empty, out var toColumn);
            return $"{fromTable}[{fromColumn}] <- {toTable}[{toColumn}]";
        }

        /// <summary>Creates a folder (grouping) node holding the given items, sorted by name.</summary>
        internal static ShowTreeNode MakeFolder(string label, List<ShowTreeNode> items)
        {
            var folder = new ShowTreeNode(label, string.Empty, null, null, isFolder: true);
            items.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));
            foreach (var item in items) folder.Children.Add(item);
            return folder;
        }

        /// <summary>Prefers the StructureModifiedTime over the ModifiedTime when both are present.</summary>
        internal static DateTime? PreferStructure(DateTime? modified, DateTime? structureModified)
            => structureModified.HasValue ? structureModified : modified;

        /// <summary>True for nodes that represent a real model object (not a folder or the model root).</summary>
        internal static bool IsRealObject(ShowTreeNode node) => !node.IsFolder && node.ObjectType != "MODEL";

        /// <summary>Recursively sets MaxUpdateUtc (most-recent change among descendants) and DaysSinceChange
        /// (whole days since the node's effective change - its own timestamp rolled up with its descendants').
        /// For individual leaf items, MaxUpdateUtc is instead set to the item's own timestamp when it carries
        /// the newest change within its container folder (so sorting by Max Update groups the most-recently
        /// changed item(s) of each folder together). Returns the subtree's effective most-recent change.</summary>
        internal static DateTime? ComputeRollups(ShowTreeNode node, DateTime nowUtc)
        {
            DateTime? childMax = null;
            DateTime? leafChildMax = null;
            foreach (var child in node.Children)
            {
                childMax = MaxDate(childMax, ComputeRollups(child, nowUtc));
                if (IsRealObject(child) && child.Children.Count == 0 && child.LastModifiedUtc.HasValue)
                    leafChildMax = MaxDate(leafChildMax, child.LastModifiedUtc);
            }
            node.MaxUpdateUtc = childMax;
            // Surface the container's newest change on the individual leaf item(s) carrying it, so the
            // Max Update column is populated for those rows (folders/tables keep their descendant rollup).
            if (leafChildMax.HasValue)
            {
                foreach (var child in node.Children)
                {
                    if (IsRealObject(child) && child.Children.Count == 0
                        && child.LastModifiedUtc.HasValue && child.LastModifiedUtc.Value == leafChildMax.Value)
                    {
                        child.MaxUpdateUtc = child.LastModifiedUtc;
                    }
                }
            }
            var effective = MaxDate(node.LastModifiedUtc, childMax);
            node.DaysSinceChange = effective.HasValue
                ? (int?)Math.Max(0, (int)Math.Floor((nowUtc - effective.Value).TotalDays))
                : null;
            return effective;
        }

        /// <summary>Finds the maximum timestamp across all real objects (tables + leaves) in the subtree.</summary>
        internal static void CollectMaxObjectTimestamp(ShowTreeNode node, ref DateTime? max)
        {
            if (IsRealObject(node) && node.LastModifiedUtc.HasValue) max = MaxDate(max, node.LastModifiedUtc);
            foreach (var child in node.Children) CollectMaxObjectTimestamp(child, ref max);
        }

        /// <summary>Prunes the subtree in place, keeping only real objects at <paramref name="max"/> and the
        /// folders/tables enclosing them. Returns true when the node should be kept.</summary>
        internal static bool PruneToMax(ShowTreeNode node, DateTime max)
        {
            for (int i = node.Children.Count - 1; i >= 0; i--)
            {
                if (!PruneToMax(node.Children[i], max)) node.Children.RemoveAt(i);
            }
            bool selfMatch = IsRealObject(node) && node.LastModifiedUtc.HasValue && node.LastModifiedUtc.Value == max;
            return selfMatch || node.Children.Count > 0;
        }

        /// <summary>Logs (but does not throw on) an error reading one of the timestamp DMVs.</summary>
        private static void LogTimestampDmvWarning(string dmv, Exception ex)
            => Log.Warning(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(BuildMetadataTimestampTree), $"error reading {dmv} - skipped");

        /// <summary>Builds a stable identity key for a tree node used to guard against cycles.</summary>
        private static string NodeIdentity(string objectType, string tableName, string name)
            => $"{objectType}|{tableName}|{name}";

        /// <summary>Resolves a column ordinal by name from the reader schema, returning -1 when absent.</summary>
        private static int OrdinalOrMinusOne(AdomdDataReader dr, string columnName)
        {
            for (int i = 0; i < dr.FieldCount; i++)
            {
                if (string.Equals(dr.GetName(i), columnName, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        /// <summary>Reads a string value defensively, returning null for missing columns or DBNull.</summary>
        private static string GetStringOrNull(AdomdDataReader dr, int ordinal)
        {
            if (ordinal < 0 || dr.IsDBNull(ordinal)) return null;
            return dr.GetValue(ordinal)?.ToString();
        }

        /// <summary>Reads a datetime value defensively, returning null for missing columns or DBNull.</summary>
        private static DateTime? GetDateTimeOrNull(AdomdDataReader dr, int ordinal)
        {
            if (ordinal < 0 || dr.IsDBNull(ordinal)) return null;
            var value = dr.GetValue(ordinal);
            if (value == null || value is DBNull) return null;
            if (value is DateTime dt) return dt;
            if (DateTime.TryParse(value.ToString(), out var parsed)) return parsed;
            return null;
        }

        /// <summary>Returns the greater of two nullable datetimes.</summary>
        private static DateTime? MaxDate(DateTime? a, DateTime? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return a.Value >= b.Value ? a : b;
        }

        #endregion

        public void SetSelectedDatabase(IDatabaseReference database)
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), $"{database.Name} - start (thread {System.Threading.Thread.CurrentThread.ManagedThreadId})");
            if (_connection == null) return;
            if (_connection.State == ConnectionState.Open || _connection.ServerType == ServerType.Offline)
            {
                if (Database != null && database.Name == Database.Name)
                {
                    Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), $"{database.Name} - skipped (already selected)");
                    return;
                }

                var context = new Polly.Context().WithDatabaseName(database?.Name??string.Empty);
                _retry.Execute(ctx =>
                {
                    if (database != null) {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        _dmvConnection?.ChangeDatabase(database.Name);
                        var dmvMs = sw.ElapsedMilliseconds;
                        _connection?.ChangeDatabase(database.Name);
                        sw.Stop();
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), $"ChangeDatabase '{database.Name}' complete (dmv conn: {dmvMs}ms, query conn: {sw.ElapsedMilliseconds - dmvMs}ms)");
                    }
                    //Database = _dmvConnection.Database;
                    ModelList = _dmvConnection.Database?.Models;
                    PublishDatabaseChangedWhenStable();
                }, context);
            }
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), database.Name + " - end" );
        }



        public void Connect(IConnectEvent message)
        {
            var id = new Guid();
            var msg = new ConnectEvent(message.ConnectionString, message.PowerPivotModeSelected, message.ApplicationName, message.PowerPivotModeSelected?message.WorkbookName:message.PowerBIFileName, message.ServerType, message.RefreshDatabases, message.DatabaseName, message.AccessToken);
            ConnectAsync(msg, id).GetAwaiter().GetResult();
        }

        internal async Task ConnectAsync(ConnectEvent message, Guid uniqueId)
        {
            IsConnecting = true;
            // the supported trace events/columns, the DMV list and the function list are all engine
            // version specific so any cached copies must be discarded before we connect to a
            // (potentially different) server
            ClearConnectionCaches();
            Log.Verbose(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(ConnectAsync), $"ConnectionString: {message.ConnectionString}/n  ServerType: {message.ServerType}");
            await _eventAggregator.PublishAsync(new ConnectionOpenedEvent(this));

            if (message.ServerType == ServerType.Offline)
            {
                await OpenOfflineConnectionAsync(message);
                // Don't publish ConnectionOpenedEvent again for offline connections
                // as it would clear the metadata that was just populated by ConnectionChangedEvent
            }
            else
            {
                await OpenOnlineConnectionAsync(message, uniqueId);
                await _eventAggregator.PublishAsync(new ConnectionOpenedEvent(this));
            }

            await _eventAggregator.PublishAsync(new DmvsLoadedEvent(DynamicManagementViews));
            await _eventAggregator.PublishAsync(new FunctionsLoadedEvent(FunctionGroups));

        }

        private async Task OpenOfflineConnectionAsync(ConnectEvent message)
        {

            var vpaContent = message.VpaxContent; //Dax.Vpax.Tools.VpaxTools.ImportVpax(message.FileName);
            _connection = new ADOTabular.ADOTabularConnection(string.Empty, ADOTabular.Enums.AdomdType.AnalysisServices);
            _connection.ServerType = ServerType.Offline;
            _connection.Visitor = new MetadataVisitorVpax(_connection, vpaContent.DaxModel, vpaContent.TomDatabase);

            _dmvConnection = new ADOTabular.ADOTabularConnection(string.Empty, ADOTabular.Enums.AdomdType.AnalysisServices);
            _dmvConnection.ServerType = ServerType.Offline;
            _dmvConnection.Visitor = new MetadataVisitorVpax(_connection, vpaContent.DaxModel, vpaContent.TomDatabase);
            // clear the caches again in case anything re-populated them from the previous connection
            // while we were publishing the ConnectionOpenedEvent
            ClearConnectionCaches();

            ServerType = message.ServerType;
            FileName = message.FileName??String.Empty;
            IsPowerPivot = message.PowerPivotModeSelected;
            //Databases.Add(_connection.Database);
            //Database = _connection.Database;
            await _eventAggregator.PublishAsync(new ConnectionChangedEvent(null, false));
        }

        public Dictionary<string, ADOTabularColumn> Columns => _dmvConnection?.Columns;

        private async Task OpenOnlineConnectionAsync(ConnectEvent message, Guid uniqueId)
        {
            var connectionString = UpdateApplicationName(message.ConnectionString, uniqueId);
            _connection = new ADOTabularConnection(connectionString, AdomdType.AnalysisServices);
            _dmvConnection = new ADOTabularConnection(connectionString, AdomdType.AnalysisServices);
            // clear the caches again in case anything re-populated them from the previous connection
            // while we were publishing the ConnectionOpenedEvent
            ClearConnectionCaches();

            
            if (message.AccessToken.IsNotNull())
            {
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OpenOnlineConnectionAsync), $"Setting Connection AccessToken (ExpirationTime: {message.AccessToken.ExpirationTime})");
                _connection.AccessToken = message.AccessToken;
                _connection.OnAccessTokenExpired = OnAccessTokenExpired;
                _dmvConnection.AccessToken = message.AccessToken;
                _dmvConnection.OnAccessTokenExpired = OnAccessTokenExpired;
            }

            ServerType = message.ServerType;
            FileName = message.FileName;
            IsPowerPivot = message.PowerPivotModeSelected;

            // open the DMV connection          
            var openDmvConnTask = _dmvConnection.OpenAsync();

            // Open the main query connection
            var openConnTask = _connection.OpenAsync();

            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OpenOnlineConnectionAsync), "Start open connections");
            var swOpen = System.Diagnostics.Stopwatch.StartNew();
            await Task.WhenAll(openConnTask, openDmvConnTask);
            swOpen.Stop();
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OpenOnlineConnectionAsync), $"End open connections (duration: {swOpen.ElapsedMilliseconds}ms)");

            // Change to the requested database after both connections are fully open
            if (!string.IsNullOrEmpty(message.DatabaseName))
            {
                _connection.ChangeDatabase(message.DatabaseName);
                _dmvConnection.ChangeDatabase(message.DatabaseName);
            }

            SetSelectedDatabase(_dmvConnection.Database);

        }

#if NET8_0_OR_GREATER
        private Microsoft.AnalysisServices.AccessToken OnAccessTokenExpired(Microsoft.AnalysisServices.AccessToken token)
#else
        private Microsoft.AnalysisServices.AdomdClient.AccessToken OnAccessTokenExpired(Microsoft.AnalysisServices.AdomdClient.AccessToken token)
#endif
        {
            if (_isDisposing)
            {
                // The connection is being disposed - skip the (blocking) token refresh. The refreshed
                // token would only be used to keep the connection alive, but it is going away.
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OnAccessTokenExpired), "Skipping AccessToken refresh - connection is being disposed");
                return token;
            }

            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OnAccessTokenExpired), "AccessToken Expired - refreshing token");
            var newToken = EntraIdHelper.RefreshToken(token).GetAwaiter().GetResult();
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OnAccessTokenExpired), $"AccessToken Refreshed - ExpirationTime: {newToken.ExpirationTime}");
            return newToken;

        }

        // Defer publishing DatabaseChangedEvent until both the query and dmv
        // connections are fully Open. The Polly retry policies in this class can
        // tear down and rebuild a connection on a background thread, leaving a
        // brief window where _connection or _dmvConnection is non-null but in a
        // Closed/Connecting state. Handlers of DatabaseChangedEvent (e.g. the
        // QueryHistory pane filter) read Connection.Database during that window
        // and previously threw NullReferenceException. Waiting briefly for both
        // connections to stabilise lets handlers see a consistent state.
        private const int DatabaseChangedStableTimeoutMs = 2000;
        private const int DatabaseChangedPollIntervalMs = 50;

        private void PublishDatabaseChangedWhenStable()
        {
            // Fire-and-forget: don't block the caller (which may itself be
            // running inside a Polly retry action on the UI thread).
            _ = Task.Run(async () =>
            {
                try
                {
                    var deadline = DateTime.UtcNow.AddMilliseconds(DatabaseChangedStableTimeoutMs);
                    while (!IsConnected && DateTime.UtcNow < deadline)
                    {
                        await Task.Delay(DatabaseChangedPollIntervalMs).ConfigureAwait(false);
                    }

                    if (!IsConnected)
                    {
                        Log.Warning(Common.Constants.LogMessageTemplate, nameof(ConnectionManager),
                            nameof(PublishDatabaseChangedWhenStable),
                            $"Connections did not stabilise within {DatabaseChangedStableTimeoutMs}ms; publishing DatabaseChangedEvent anyway");
                    }

                    await _eventAggregator.PublishAsync(new DatabaseChangedEvent()).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager),
                        nameof(PublishDatabaseChangedWhenStable), "Error publishing DatabaseChangedEvent");
                }
            });
        }

        private static string UpdateApplicationName(string connectionString, Guid uniqueId)
        {
            var builder = connectionString.ToConnectionStringBuilder();
            builder.TryGetValue("Application Name", out var appName);
            if (appName == null) return connectionString;
            appName = guidRegex.Replace((appName ?? string.Empty).ToString(), uniqueId.ToString());
            builder["Application Name"] = appName;
            return builder.ToString();
        }

        public async Task RefreshTablesAsync()
        {
            try
            {
                Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(RefreshTablesAsync), "Start");
                await Task.Factory.StartNew(() =>
                {
                    _retry.Execute(() =>
                    {
                        GetTables();
                        IsConnecting = false;
                        _eventAggregator.PublishAsync(new TablesRefreshedEvent(this));
                    });
                });
            }
            catch (Exception ex)
            {
                var errMsg = $"Error refreshing table list: {ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(RefreshTablesAsync), errMsg);
                await _eventAggregator.PublishAsync(new OutputMessage(MessageType.Error, errMsg));                
            }
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(RefreshTablesAsync), "End");
        }


        public bool IsTestingRls => _connection?.IsTestingRls??false;

        public static bool IsPbiXmlaEndpoint(string connectionString)
        {
            var builder = connectionString.ToConnectionStringBuilder();
            var server = builder.GetDataSource();
            return server.StartsWith("powerbi://", StringComparison.InvariantCultureIgnoreCase)
                || server.StartsWith("pbiazure://", StringComparison.InvariantCultureIgnoreCase)
                || server.StartsWith("pbidedicated://", StringComparison.InvariantCultureIgnoreCase);
        }
        private object _supportedTraceEventClassesLock = new object();
        private Dictionary<DaxStudioTraceEventClass,HashSet<TOM.TraceColumn>> _supportedTraceEventClasses;
        private bool disposedValue;

        // Set at the start of the dispose path (before the underlying connections are torn down)
        // so the OnAccessTokenExpired callback can short-circuit. Disposing an ADOMD/AMO connection
        // can re-enter OnAccessTokenExpired, which otherwise blocks on an async token refresh (and
        // can escalate to an interactive sign-in prompt) while the connection is going away.
        // Marked volatile because the callback may be invoked on a different thread than Dispose().
        private volatile bool _isDisposing;

        public Dictionary<DaxStudioTraceEventClass, HashSet<TOM.TraceColumn>> SupportedTraceEventClasses
        {
            get
            {
                lock (_supportedTraceEventClassesLock) {
                    _supportedTraceEventClasses ??= PopulateSupportedTraceEventClasses();
                }
                return _supportedTraceEventClasses;

            }
        }

        // The set of trace events and the columns that each event supports varies between engine
        // versions (eg. SSAS 2025 removed the ApplicationName column from the VertiPaqSEQuery* events)
        // so the cached values must be discarded whenever we connect to a different server, otherwise
        // we can request columns which are not valid for the current engine.
        public void ClearSupportedTraceEventClasses()
        {
            lock (_supportedTraceEventClassesLock)
            {
                _supportedTraceEventClasses = null;
            }
        }

        // Discards all the metadata that is cached for the lifetime of this ConnectionManager, but which
        // is specific to the server that we are connected to. A ConnectionManager is created once per
        // document and is re-used every time that document connects to a different server.
        private void ClearConnectionCaches()
        {
            ClearSupportedTraceEventClasses();
            // these are built from _dmvConnection which gets replaced when we connect
            _dynamicManagementViews = null;
            _functionGroups = null;
        }

        private Dictionary<DaxStudioTraceEventClass,HashSet<TOM.TraceColumn>> PopulateSupportedTraceEventClasses()
        {
            var result = new Dictionary<DaxStudioTraceEventClass, HashSet<TOM.TraceColumn>>();
            using (var dr = ExecuteReader("SELECT * FROM $SYSTEM.DISCOVER_TRACE_EVENT_CATEGORIES", null))
            {
                while (dr.Read())
                {
                    var xml = dr.GetString(0);
                    using (var sr = new StringReader(xml))
                    using (var xr = new XmlTextReader(new StringReader(xml)))
                    {
                        XPathDocument xPath = new XPathDocument(xr);
                        var nav = xPath.CreateNavigator();
                        var iter = nav.Select("/EVENTCATEGORY/EVENTLIST/EVENT/ID");
                        while (iter.MoveNext())
                        {
                            var columns = new HashSet<TOM.TraceColumn>();
                            var iter2 = iter.Current.Select("../EVENTCOLUMNLIST/EVENTCOLUMN/ID");
                            while (iter2.MoveNext())
                            {
                                columns.Add((TOM.TraceColumn)iter2.Current.ValueAsInt);
                            }
                            
                            result.Add((DaxStudioTraceEventClass)iter.Current.ValueAsInt, columns);
                        }
                    }
                }
            }
            return result;
        }

        public bool TryGetColumn(string tablename, string columnname, out ADOTabularColumn column)
        {
            if (tablename != null 
                && columnname != null 
                && _dmvConnection.Database.Models.BaseModel.Tables.TryGetValue(tablename, out var table))
            {
                return table.Columns.TryGetValue(columnname, out column);
            }
            column = null;
            return false;
        }

        public async Task ProcessDatabaseAsync(string refreshType)
        {
//            var refreshCommand = $@"
//{{  
//    ""refresh"": {{
//        ""type"": ""{refreshType}"",  
//        ""objects"": [
//            {{
//                ""database"": ""{_connection.Database.Name}""
//            }}  
//        ]  
//    }}
//}}";
            var refreshCommand = $@"
<Process xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">  
  <Object>  
    <DatabaseID>{_dmvConnection.Database.Id}</DatabaseID>  
  </Object>  
  <Type>{refreshType}</Type>  
</Process>
";

            refreshCommand = $@"<Batch Transaction=""false"" xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">
  <Refresh xmlns=""http://schemas.microsoft.com/analysisservices/2014/engine"">
    <DatabaseID>{_dmvConnection.Database.Id}</DatabaseID>
    <Model>
      <xs:schema xmlns:xs=""http://www.w3.org/2001/XMLSchema"" xmlns:sql=""urn:schemas-microsoft-com:xml-sql"">
        <xs:element>
          <xs:complexType>
            <xs:sequence>
              <xs:element type=""row""/>
            </xs:sequence>
          </xs:complexType>
        </xs:element>
        <xs:complexType name=""row"">
          <xs:sequence>
            <xs:element name=""RefreshType"" type=""xs:long"" sql:field=""RefreshType"" minOccurs=""0""/>
          </xs:sequence>
        </xs:complexType>
      </xs:schema>
      <row xmlns=""urn:schemas-microsoft-com:xml-analysis:rowset"">
        <RefreshType>3</RefreshType>
      </row>
    </Model>
  </Refresh>
  <SequencePoint xmlns=""http://schemas.microsoft.com/analysisservices/2014/engine"">
    <DatabaseID>aafa360c-734a-471d-b2b3-ba56dfe88121</DatabaseID>
  </SequencePoint>
</Batch>";

            await Task.Run(() =>
            {
                var server = new TOM.Server();
                var db = server.Databases[_dmvConnection.Database.Id];
                db.Model.RequestRefresh(Microsoft.AnalysisServices.Tabular.RefreshType.Full);
                db.Model.SaveChanges();
                server.Disconnect();
            });
//            await Task.Run(() => _dmvConnection.ExecuteNonQuery(refreshCommand));
        }

        public async Task ProcessTableAsync(string tableName)
        {
            var refreshType = "defragment";
//            var refreshCommand = $@"
//{{  
//    ""refresh"": {{
//        ""type"": ""{refreshType}"",  
//        ""objects"": [
//            {{
//                ""database"": ""{_connection.Database.Name}"",
//                ""table"": ""{tableName}""
//            }}  
//        ]  
//    }}
//}}";
            var refreshCommand = $@"
<Process xmlns=""http://schemas.microsoft.com/analysisservices/2003/engine"">  
  <Object>  
    <DatabaseID>{_dmvConnection.Database.Id}</DatabaseID>  
    <Table></Table>
  </Object>  
  <Type>{refreshType}</Type>  
</Process>
";
            await Task.Run(() => _dmvConnection.ExecuteNonQuery(refreshCommand));
            return;

        }

        public DataSet DiscoverQueryDependencies(string queryText)
        {
            var restriction = new AdomdRestriction("QUERY", queryText);
            var restrictions = new AdomdRestrictionCollection() { restriction};
            DataSet ds =  _connection.GetSchemaDataSet("DISCOVER_CALC_DEPENDENCY", restrictions);
            DataTable t = ds.Tables[0];
            List<ADOTabularMeasure> measures = new List<ADOTabularMeasure>();
            foreach (DataRow row in t.Rows)
            {
                var refObjType = row["REFERENCED_OBJECT_TYPE"].ToString();
                if (refObjType == "MEASURE" || refObjType == "FUNCTION")
                {
                    measures.AddRange(FindDependentMeasures(row["REFERENCED_OBJECT"].ToString()));
                    
                }
            }
            foreach (var m in measures)
            {
                t.Rows.Add(new[] { "QUERY", "MEASURE", m.Table.Name, m.Name, m.Expression });
            }

            return ds;
        }

#if NET8_0_OR_GREATER
        public Microsoft.AnalysisServices.AccessToken AccessToken { get => _connection.AccessToken; }
#else
        public Microsoft.AnalysisServices.AdomdClient.AccessToken AccessToken { get => _connection.AccessToken; }
#endif

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Signal the token-refresh callback to short-circuit, then detach the callbacks
                    // so disposing the connections cannot re-enter OnAccessTokenExpired.
                    _isDisposing = true;
                    if (_connection != null) _connection.OnAccessTokenExpired = null;
                    if (_dmvConnection != null) _dmvConnection.OnAccessTokenExpired = null;

                    _connection?.Dispose();
                    _dmvConnection?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ConnectionManager()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
