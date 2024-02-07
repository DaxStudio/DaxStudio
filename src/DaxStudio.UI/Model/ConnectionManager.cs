using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Enums;
using ADOTabular.MetadataInfo;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using Polly;
using Polly.Retry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ADOTabular.Utils;
using DaxStudio.UI.Interfaces;
using System.Threading;
using DaxStudio.Common.Enums;
using System.Xml.XPath;
using System.IO;
using static Dax.Vpax.Tools.VpaxTools;
using DaxStudio.Controls.DataGridFilter.Querying;
using DaxStudio.UI.ViewModels;
using System.Data.Common;
using System.Data.OleDb;
using System.Windows.Forms.VisualStyles;
using Microsoft.AnalysisServices;
using System.Xml;
using ADOTabular.Interfaces;

namespace DaxStudio.UI.Model
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
        , IMetadataProvider
        , IConnection
        , IModelIntellisenseProvider
    {
        public bool IsConnecting { get; private set; }

        private ADOTabularConnection _connection;
        private ADOTabularConnection _dmvConnection;
        private readonly IEventAggregator _eventAggregator;
        private RetryPolicy _retry;
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
        public string ApplicationName => _connection?.ApplicationName??"DAX Studio";

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
                        _connection.Close(true); // force the connection closed and close the session

                        _connection = new ADOTabularConnection(_connection.ConnectionString, _connection.Type);
                        _connection.ChangeDatabase(currentDb);

                        _eventAggregator.PublishOnUIThreadAsync(new ReconnectEvent(_connection.SessionId));
                        var msg =
                            $"A connection error occurred: {exception.Message}\nAttempting to reconnect (retry: {retryCount})";
                        _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, msg));
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
                        _dmvConnection.Close(true); // force the connection closed and close the session

                        _dmvConnection = new ADOTabularConnection(_dmvConnection.ConnectionString, _dmvConnection.Type);
                        _dmvConnection.ChangeDatabase(currentDb);

                        _eventAggregator.PublishOnUIThreadAsync(new ReconnectEvent(_dmvConnection.SessionId));
                        var msg =
                            $"A connection error occurred: {exception.Message}\nAttempting to reconnect (retry: {retryCount})";
                        _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, msg));
                        Log.Warning(exception, Common.Constants.LogMessageTemplate, nameof(ConnectionManager),
                            "RetryPolicy", msg);
                    });
        }

        public string ConnectionString => _connection?.ConnectionString ?? string.Empty;

        public string ConnectionStringWithInitialCatalog =>
            _connection?.ConnectionStringWithInitialCatalog ?? string.Empty;

        public ADOTabularDatabase Database => _dmvRetry.Execute(() => {
            return _dmvConnection?.Database;
        });
        public string DatabaseName => _dmvRetry.Execute(() => _dmvConnection?.Database?.Name ?? string.Empty);
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

                    if (_dmvConnection.HasRlsParameters())
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
                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning,
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
                    if (_connection.HasRlsParameters())
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
                    _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning,
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
                        conn.ChangeDatabase(this.DatabaseName);
                        if (conn.State != ConnectionState.Open) conn.Open();
                        var dbChanges = conn.Database?.LastUpdate > _lastSchemaUpdate;
                        _lastSchemaUpdate = conn.Database?.LastUpdate ?? DateTime.MinValue;
                        conn.Close(true); // close and end the session
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
                return _connection.State == ConnectionState.Open;
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
        public ServerType ServerType { get; private set; }

        public int SPID { get { return _connection?.SPID??0; } }
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
            return _dmvRetry.Execute(() => { return _dmvConnection.Database.Models; });
    }

        public ADOTabularTableCollection GetTables()
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(GetTables), "Start");
            return _dmvRetry.Execute(() =>
            {
                try
                {
                    return _dmvConnection.Database.Models[SelectedModelName].Tables;
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


        public async Task UpdateColumnSampleData(ITreeviewColumn column, int sampleSize) 
        {

            column.UpdatingSampleData = true;
            try
            {
                await Task.Run(() => {
                    using (var newConn = _dmvConnection.Clone())
                    {
                        column.SampleData?.Clear();
                        try
                        {
                            column.SampleData?.AddRange(column.InternalColumn.GetSampleData(newConn, sampleSize));
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(UpdateColumnSampleData), "Error getting sample data");
                            _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Error getting sample data for tooltip\n{ex.Message}"));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error populating tooltip sample data: {ex.Message}";
                Log.Warning(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(UpdateColumnSampleData), errorMsg);
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, errorMsg));
            }
            finally
            {
                column.UpdatingSampleData = false;
            }

        }
        public async Task UpdateColumnBasicStats(ITreeviewColumn column)
        {

      
            column.UpdatingBasicStats = true;
            try
            {
                await Task.Run(() => {
                    using (var newConn = _dmvConnection.Clone())
                    {
                        column.InternalColumn.UpdateBasicStats(newConn);
                        column.MinValue = column.InternalColumn.MinValue;
                        column.MaxValue = column.InternalColumn.MaxValue;
                        column.DistinctValues = column.InternalColumn.DistinctValues;
                    }
                });
            }
            catch (Exception ex)
            {
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Error populating tooltip basic statistics data: {ex.Message}"));
            }
            finally
            {
                column.UpdatingBasicStats = false;
            }
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
                        await _eventAggregator.PublishOnUIThreadAsync( 
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
                if (_connection.State == ConnectionState.Open)
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

                _eventAggregator.PublishOnUIThreadAsync(new DatabaseChangedEvent());
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
                        Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), databaseName);
                        _connection.ChangeDatabase(databaseName);
                        _dmvConnection.ChangeDatabase(databaseName);
                    }
                    if (_dmvConnection.Database != null)
                    {
                        ModelList = _dmvConnection.Database.Models;
                    }
                    _eventAggregator.PublishOnUIThreadAsync(new DatabaseChangedEvent());
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
            var modelMeasures = (from t in model.Tables
                                 from m in t.Measures
                                 select m).ToList();
            var distinctColumns = (from t in model.Tables
                                   from c in t.Columns
                                   where c.ObjectType == ADOTabularObjectType.Column
                                   select c.Name).Distinct().ToList();

            var finalMeasure = modelMeasures.First(m => m.Name == measureName);

            var resultExpression = finalMeasure.Expression;

            bool foundDependentMeasures;

            do
            {
                foundDependentMeasures = false;
                foreach (var modelMeasure in modelMeasures)
                {
                    Regex daxMeasureRegex = new Regex($@"\[{ modelMeasure.Name.Replace("]","]]")}]|'{modelMeasure.Table.DaxName}'\[{modelMeasure.Name.Replace("]", "]]")}]|{modelMeasure.Table.DaxName}\[{modelMeasure.Name.Replace("]", "]]")}]");
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
                                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, msg));
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

            while (scanMeasures.Count > 0)
            {
                var measure = scanMeasures.Dequeue();
                if (dependentMeasures.Where(item => item.Name == measure.Name).Any()) continue;
                dependentMeasures.Add(measure);

                var dmvDependency = $"SELECT REFERENCED_OBJECT_TYPE, REFERENCED_TABLE, REFERENCED_OBJECT\r\nFROM $SYSTEM.DISCOVER_CALC_DEPENDENCY\r\nWHERE QUERY='EVALUATE {{ {measure.Expression.Replace("'", "''" )} }}'";

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

        public void SetSelectedDatabase(IDatabaseReference database)
        {
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), database.Name + " - start");
            if (Database != null && database.Name == Database.Name) return;

            var context = new Polly.Context().WithDatabaseName(database?.Name??string.Empty);
            _retry.Execute(ctx =>
            {
                if (database != null) { 
                    _dmvConnection?.ChangeDatabase(database.Name);
                    _connection?.ChangeDatabase(database.Name);
                }
                //Database = _dmvConnection.Database;
                ModelList = _dmvConnection.Database?.Models;
                _eventAggregator.PublishOnBackgroundThreadAsync(new DatabaseChangedEvent());
            }, context);
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetSelectedDatabase), database.Name + " - end" );
        }



        public void Connect(IConnectEvent message)
        {
            var id = new Guid();
            var msg = new ConnectEvent(message.ConnectionString, message.PowerPivotModeSelected, message.ApplicationName, message.PowerPivotModeSelected?message.WorkbookName:message.PowerBIFileName, message.ServerType, message.RefreshDatabases, message.DatabaseName);
            ConnectAsync(msg, id).Wait();
        }

        internal async Task ConnectAsync(ConnectEvent message, Guid uniqueId)
        {
            IsConnecting = true;
            Log.Verbose(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(ConnectAsync), $"ConnectionString: {message.ConnectionString}/n  ServerType: {message.ServerType}");

            if (message.ServerType == ServerType.Offline)
                OpenOfflineConnection(message);
            else
                OpenOnlineConnection(message, uniqueId);


            await _eventAggregator.PublishOnUIThreadAsync(new ConnectionOpenedEvent());
            //await _eventAggregator.PublishOnUIThreadAsync(new SelectedDatabaseChangedEvent(_dmvConnection.Database?.Name));
            await _eventAggregator.PublishOnBackgroundThreadAsync(new DmvsLoadedEvent(DynamicManagementViews));
            await _eventAggregator.PublishOnBackgroundThreadAsync(new FunctionsLoadedEvent(FunctionGroups));
            //await Task.Delay(300);


        }

        private void OpenOfflineConnection(ConnectEvent message)
        {

            var vpaContent = message.VpaxContent; //Dax.Vpax.Tools.VpaxTools.ImportVpax(message.FileName);
            _connection = new ADOTabular.ADOTabularConnection(string.Empty, ADOTabular.Enums.AdomdType.AnalysisServices);
            _connection.ServerType = ServerType.Offline;
            _connection.Visitor = new MetadataVisitorVpax(_connection, vpaContent.DaxModel, vpaContent.TomDatabase);

            _dmvConnection = new ADOTabular.ADOTabularConnection(string.Empty, ADOTabular.Enums.AdomdType.AnalysisServices);
            _dmvConnection.ServerType = ServerType.Offline;
            _dmvConnection.Visitor = new MetadataVisitorVpax(_connection, vpaContent.DaxModel, vpaContent.TomDatabase);

            ServerType = message.ServerType;
            FileName = message.FileName??String.Empty;
            IsPowerPivot = message.PowerPivotModeSelected;
            Databases.Add(_connection.Database);
            //Database = _connection.Database;
            _eventAggregator.PublishOnUIThreadAsync(new ConnectionChangedEvent(null, false));
        }

        public Dictionary<string, ADOTabularColumn> Columns => _dmvConnection?.Columns;

        private void OpenOnlineConnection(ConnectEvent message, Guid uniqueId)
        {
            var connectionString = UpdateApplicationName(message.ConnectionString, uniqueId);
            _connection = new ADOTabularConnection(connectionString, AdomdType.AnalysisServices);
            _dmvConnection = new ADOTabularConnection(connectionString, AdomdType.AnalysisServices);

            ServerType = message.ServerType;
            FileName = message.FileName;
            IsPowerPivot = message.PowerPivotModeSelected;
            
            // open the DMV connection
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OpenOnlineConnection), "Start open DMV connection");
            if (_dmvConnection.State != ConnectionState.Open) _dmvConnection.Open();
            //_connection = _dmvConnection.Clone();
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OpenOnlineConnection), "End open DMV connection");
            
            // Open the main query connection
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OpenOnlineConnection), "Start open query connection");
            if (_connection.State != ConnectionState.Open)  _connection.Open(); 
 
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(OpenOnlineConnection), "End open query connection");
            
            SetSelectedDatabase(_dmvConnection.Database);

        }

        private string UpdateApplicationName(string connectionString, Guid uniqueId)
        {
            var builder = new OleDbConnectionStringBuilder(connectionString);
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
                        _eventAggregator.PublishOnUIThreadAsync(new TablesRefreshedEvent());
                    });
                });
            }
            catch (Exception ex)
            {
                var errMsg = $"Error refreshing table list: {ex.Message}";
                Log.Error(ex, Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(RefreshTablesAsync), errMsg);
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, errMsg));                
            }
            Log.Debug(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(RefreshTablesAsync), "End");
        }

        public IEnumerable<IFilterableTreeViewItem> GetTreeViewTables(IMetadataPane metadataPane, IGlobalOptions options)
        {
            return _retry.Execute(() => {

                ADOTabularModel tmpModel; 
                if (_dmvConnection.ServerMode == "Offline")
                { 
                    // if we are in offline mode there is no need to clone the connection
                    tmpModel = _connection.Database.Models[SelectedModel.Name];
                }
                else
                {
                    // in online mode we clone the connection to try and avoid
                    // XmlReader in use errors
                    
                    tmpModel = _dmvConnection.Database.Models[SelectedModel.Name];
                    
                }

                var tvt = tmpModel.TreeViewTables(options, _eventAggregator, metadataPane);
                return tvt;
            });
        }

        public async void UpdateTableBasicStats(TreeViewTable table)
        {
            table.UpdatingBasicStats = true;
            try
            {
                await Task.Run(() => {
                    using (var newConn = _dmvConnection.Clone())
                    {
                        table.UpdateBasicStats(newConn);
;
                    }
                });
            }
            catch (Exception ex)
            {
                await _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Warning, $"Error populating tooltip basic statistics data: {ex.Message}"));
            }
            finally
            {
                table.UpdatingBasicStats = false;
            }
        }


        /// <summary>
        /// Attempts to set the ViewAs user.
        /// Warning: this uses settings that are not documented by Microsoft and so could be subject to changes at any time
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="roles"></param>
        internal async Task SetViewAsAsync(string userName, string roles, List<ITraceWatcher> activeTraces)
        {
            Log.Information(Common.Constants.LogMessageTemplate, nameof(ConnectionManager), nameof(SetViewAsAsync), $"Setting ViewAs User: '{userName}' Roles: '{roles}'");
            /*
             * ;Authentication Scheme=ActAs;
             * Ext Auth Info="<Properties><UserName>test</UserName><BypassAuthorization>true</BypassAuthorization><RestrictCatalog>29530e54-5667-46ab-9c6a-d5b494347966</RestrictCatalog></Properties>";
             */
            if (string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(roles)) throw new ArgumentException("You must specify either a Username or Roles to activate the ViewAs functionality");

            var builder = new OleDbConnectionStringBuilder(this.ConnectionString);

            // set catalog
            builder["Initial Catalog"] = this.DatabaseName;
            var catalogElement = $"<RestrictCatalog>{this.DatabaseName}</RestrictCatalog>";

            string userElement = string.Empty;
            string rolesElement = string.Empty;

            if (!string.IsNullOrEmpty(userName))
            {
                

                userElement = $"<UserName>{userName}</UserName><BypassAuthorization>true</BypassAuthorization>";

                if (!string.IsNullOrEmpty(roles))
                {
                    // set Roles= on connstr
                    // add roles restriction to ExtAuth
                    builder.Add("Roles", roles);
                    rolesElement = $"<RestrictRoles>{roles}</RestrictRoles>";
                }

                var extAuthInfo = $"<Properties>{userElement}{catalogElement}{rolesElement}</Properties>";

                // if data source does not support ActAs we should try Effective Username
                if ( SupportsActAs() )
                {
                    // ExtAuth works on PBI or ASAzure
                    builder.Add("Authentication Scheme", "ActAs");
                    builder.Add("Ext Auth Info", extAuthInfo);
                } else
                {
                    builder.Add("EffectiveUsername", userName);
                }

            }

            if (!string.IsNullOrEmpty(roles))
            {
                // set Roles= on connstr
                // add roles restriction to ExtAuth
                builder["Roles"] = roles;
            }

            var connEvent = new ConnectEvent(builder.ConnectionString, IsPowerPivot, this.ApplicationName, FileName, ServerType, true, this.DatabaseName);
            connEvent.ActiveTraces = activeTraces;
            await _eventAggregator.PublishOnUIThreadAsync(connEvent);
            

        }

        private bool SupportsActAs()
        {
            return (this.ServerName.StartsWith("asazure://", StringComparison.InvariantCultureIgnoreCase)
                || this.ServerName.StartsWith("powerbi://", StringComparison.InvariantCultureIgnoreCase)
                || this.ServerName.StartsWith("pbiazure://", StringComparison.InvariantCultureIgnoreCase)
                || this.ServerName.StartsWith("pbidedicated://", StringComparison.InvariantCultureIgnoreCase)
                || this.ServerName.StartsWith("localhost:", StringComparison.InvariantCultureIgnoreCase)
                );

        }

        public void StopViewAs(List<ITraceWatcher> activeTraces)
        {
            var builder = new OleDbConnectionStringBuilder(this.ConnectionString);
            builder.Remove("Authentication Scheme");
            builder.Remove("Ext Auth Info");
            builder.Remove("Roles");
            builder.Remove("EffectiveUsername");

            var connEvent = new ConnectEvent(builder.ConnectionString, IsPowerPivot, this.ApplicationName, FileName, ServerType, true, DatabaseName);
            connEvent.ActiveTraces = activeTraces;
            _eventAggregator.PublishOnUIThreadAsync(connEvent);

        }

        public bool IsTestingRls => _connection?.IsTestingRls??false;

        public static bool IsPbiXmlaEndpoint(string connectionString)
        {
            var builder = new System.Data.OleDb.OleDbConnectionStringBuilder(connectionString);
            var server = builder["Data Source"].ToString();
            return server.StartsWith("powerbi://", StringComparison.InvariantCultureIgnoreCase)
                || server.StartsWith("pbiazure://", StringComparison.InvariantCultureIgnoreCase)
                || server.StartsWith("pbidedicated://", StringComparison.InvariantCultureIgnoreCase);
        }
        private object _supportedTraceEventClassesLock = new object();
        private HashSet<DaxStudioTraceEventClass> _supportedTraceEventClasses;
        public HashSet<DaxStudioTraceEventClass> SupportedTraceEventClasses
        {
            get
            {
                lock (_supportedTraceEventClassesLock) {
                    _supportedTraceEventClasses ??= PopulateSupportedTraceEventClasses();
                }
                return _supportedTraceEventClasses;

            }
        }

        private HashSet<DaxStudioTraceEventClass> PopulateSupportedTraceEventClasses()
        {
            var result = new HashSet<DaxStudioTraceEventClass>();
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
                            result.Add((DaxStudioTraceEventClass)iter.Current.ValueAsInt);
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
            var server = new Server();
            var db = server.Databases[_dmvConnection.Database.Id];
            db.Model.RequestRefresh(Microsoft.AnalysisServices.Tabular.RefreshType.Full);
            db.Model.SaveChanges();
            server.Disconnect();

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

    }
}
