using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using ADOTabular.Enums;
using ADOTabular.Interfaces;
using ADOTabular.MetadataInfo;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.ViewModels;
using Polly.Retry;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/// <summary>
/// The purpose of the ConnectionManager is to centralize all the connection handling into one place
/// This allows for consistent retry policies and allows us to use a secondary connection for things 
/// like metadata refreshes.
/// </summary>
namespace DaxStudio.UI.Model
{

    // TODO - load metadata/tables from a different connection so that someone can type in the main window
    // TODO - add retry logic around queries and metadata refresh
    // TODO - flush metadata on connection failure
    // TODO - cache functions and dmvs unless we change the connection

    public class ConnectionManager : IConnectionManager
        , IDmvProvider
        , IFunctionProvider
        , IMetadataProvider
        , IModelIntellisenseProvider
        , IHandleWithTask<RefreshTablesEvent>
        , IHandle<SelectedModelChangedEvent>
    {
        private ADOTabularConnection _connection;
        private IEventAggregator _eventAggregator;
        private RetryPolicy retry;
        public ConnectionManager(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            
        }

        public IEnumerable<string> AllFunctions => _connection.AllFunctions;
        public string ApplicationName => _connection.ApplicationName;
        public void Cancel()
        {
            _connection.Cancel();
        }

        public void Close()
        {
            if (_connection != null)
            {
                if (_connection.State != ConnectionState.Closed && _connection.State != ConnectionState.Broken)
                {
                    _connection.Close();
                }
            }
        }
        public string ConnectionString => _connection?.ConnectionString??string.Empty;
        public string ConnectionStringWithInitialCatalog => _connection?.ConnectionStringWithInitialCatalog??string.Empty;

        public ADOTabularDatabase Database => _connection.Database;
        public string DatabaseName => _connection?.Database?.Name ?? string.Empty;
        public DaxMetadata DaxMetadataInfo => _connection.DaxMetadataInfo;

        #region Query Exection
        public DataTable ExecuteDaxQueryDataTable(string query)
        {
            return _connection.ExecuteDaxQueryDataTable(query);
        }
        public AdomdDataReader ExecuteReader(string query)
        {
            return _connection.ExecuteReader(query);
        }
        public string FileName => _connection.FileName;

        private ADOTabularDynamicManagementViewCollection _dynamicManagementViews;
        public ADOTabularDynamicManagementViewCollection DynamicManagementViews
        {
            get
            {
                if (_dynamicManagementViews == null) _dynamicManagementViews = new ADOTabularDynamicManagementViewCollection(_connection);
                return _dynamicManagementViews;
            }
        }

        #endregion


        public async Task<bool> HasSchemaChangedAsync()
        {
            var conn = _connection.Clone();
            bool hasChanged = await Task<bool>.Run(() => {
                conn.Open();
                return conn?.Database?.HasSchemaChanged() ?? false;
            });
            conn.Close();
            return false;
        }

        public ADOTabularDatabaseCollection Databases => _connection.Databases;
        public bool IsAdminConnection => _connection.IsAdminConnection;

        public bool IsConnected { get
            {
                if (_connection == null) return false;
                return _connection.State == ConnectionState.Open;
            } 
        }
        public bool IsPowerBIorSSDT => _connection?.IsPowerBIorSSDT??false;
        public bool IsPowerPivot => _connection?.IsPowerPivot ?? false;

        public void Open()
        {
            _connection.Open();
        }
        public void Refresh()
        {
            _connection.Refresh();
        }
        public string ServerEdition => _connection.ServerEdition;
        public string ServerLocation => _connection.ServerLocation;
        public string ServerMode => _connection.ServerMode;
        public string ServerName => _connection.ServerName;
        public string ServerVersion => _connection.ServerVersion;
        public string SessionId => _connection.SessionId;
        public ADOTabular.Enums.ServerType ServerType => _connection.ServerType;
        public int SPID => _connection.SPID;
        public string ShortFileName => _connection.ShortFileName;

        public  bool ShouldAutoRefreshMetadata( IGlobalOptions options)
        {
            switch (_connection.ConnectionType)
            {
                case ADOTabular.ADOTabularConnectionType.Cloud:
                    return options.AutoRefreshMetadataCloud;
                case ADOTabular.ADOTabularConnectionType.LocalNetwork:
                    return options.AutoRefreshMetadataLocalNetwork;
                case ADOTabular.ADOTabularConnectionType.LocalMachine:
                    return options.AutoRefreshMetadataLocalMachine;
                default:
                    return true;
            }
        }

        private ADOTabularFunctionGroupCollection _functionGroups;
        public ADOTabularFunctionGroupCollection FunctionGroups
        {
            get
            {
                if (_functionGroups == null) _functionGroups = new ADOTabularFunctionGroupCollection(_connection);
                return _functionGroups;
            }
        }

        public ADOTabularDatabaseCollection GetDatabases()
        {
            return _connection.Databases;
        }

        public ADOTabularModelCollection GetModels()
        {
            return _connection.Database.Models;
        }

        public ADOTabularTableCollection GetTables()
        {
            return _connection.Database.Models[SelectedModelName].Tables;
        }

        public AdomdType Type => _connection.Type;

        public string SelectedDatabaseName { get; set; }

        public string SelectedModelName { get; set; }

        public ADOTabularDatabase SelectedDatabase { get; set; }

        public async Task UpdateColumnSampleData(ITreeviewColumn column, int sampleSize) 
        {

            column.UpdatingSampleData = true;
            try
            {
                await Task.Run(() => {
                    using (var newConn = _connection.Clone())
                    {
                        column.SampleData.Clear();
                        column.SampleData.AddRange(column.InternalColumn.GetSampleData(newConn, sampleSize));
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
                    using (var newConn = _connection.Clone())
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
        public ADOTabularModel SelectedModel { get; set; }

        public void SetSelectedModel(ADOTabular.ADOTabularModel model)
        {


            SelectedModel = model;
            SelectedModelName = model.Name;

            if (SelectedModel != null)
            {
                if (_connection.IsMultiDimensional)
                {
                    if (_connection.Is2012SP1OrLater)
                    {
                        _connection.SetCube(SelectedModel.Name);
                    }
                    else
                    {
                        _eventAggregator.PublishOnUIThread( 
                            new OutputMessage(MessageType.Error, 
                                $"DAX Studio can only connect to Multi-Dimensional servers running 2012 SP1 CU4 (11.0.3368.0) or later, this server reports a version number of {_connection.ServerVersion}")
                            );
                    }
                }
            }
            //_eventAggregator.PublishOnBackgroundThread(new SelectedModelChangedEvent(SelectedModelName));
            _eventAggregator.PublishOnBackgroundThread(new RefreshTablesEvent());
        }

        public void SetSelectedDatabase(ADOTabularDatabase database)
        {
            if (_connection != null)
            {
                if (_connection.State == ConnectionState.Open)
                {
                    if (SelectedDatabase != null && database != null && _connection.Database.Name != database.Name) //!Connection.Database.Equals(_selectedDatabase))
                    {
                        Log.Debug("{Class} {Event} {selectedDatabase}", "MetadataPaneViewModel", "SelectedDatabase:Set (changing)", database.Name);
                        _connection.ChangeDatabase(database.Name);

                    }
                    if (_connection.Database != null)
                    {
                        ModelList = _connection.Database.Models;
                    }
                }
            }

            if (SelectedDatabase != database)
            {
                SelectedDatabase = database;
                if (SelectedDatabase != null) _connection.ChangeDatabase(SelectedDatabase.Name);
                ModelList = _connection.Database.Models;

                _eventAggregator.PublishOnUIThread(new DatabaseChangedEvent());
            }

        }

        public List<ADOTabularMeasure> GetAllMeasures(string filterTable = null)
        {
            bool allTables = (string.IsNullOrEmpty(filterTable));
            var model = _connection.Database.Models.BaseModel;
            var modelMeasures = (from t in model.Tables
                                 from m in t.Measures
                                 where (allTables || t.Caption == filterTable)
                                 select m).ToList();
            return modelMeasures;
        }

        public string DefineFilterDumpMeasureExpression(string tableCaption, bool allTables)
        {

            var model = _connection.Database.Models.BaseModel;
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

            var model = _connection.Database.Models.BaseModel;
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
                    //string daxMeasureName = "[" + modelMeasure.Name + "]";
                    //string newExpression = resultExpression.Replace(daxMeasureName, " CALCULATE ( " + modelMeasure.Expression + " )");
                    Regex daxMeasureRegex = new Regex(@"[^\w']?\[" + modelMeasure.Name + "]");

                    string newExpression = daxMeasureRegex.Replace(resultExpression, " CALCULATE ( " + modelMeasure.Expression + " )");

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
                                _eventAggregator.PublishOnUIThread(new OutputMessage(MessageType.Warning, msg));
                                throw new InvalidOperationException(msg);
                            }
                        }
                    }

                }
            } while (foundDependentMeasures);

            return resultExpression;
        }

        public void SetSelectedDatabase(IDatabaseReference database)
        {
            //if (database.Name == _connection.Database?.Name) return;

            _connection.ChangeDatabase(database.Name);
            SelectedDatabase = _connection.Database;
            ModelList = _connection.Database?.Models;
            _eventAggregator.PublishOnBackgroundThread(new DatabaseChangedEvent());
        }

        internal void Connect(ConnectEvent message)
        {
            _connection = new ADOTabularConnection(message.ConnectionString, AdomdType.AnalysisServices);

            _connection.Open();
            _eventAggregator.PublishOnUIThread(new ConnectionOpenedEvent());
            _eventAggregator.PublishOnUIThread(new SelectedDatabaseChangedEvent(_connection.Database?.Name));
            _eventAggregator.PublishOnBackgroundThread(new DmvsLoadedEvent(DynamicManagementViews));
            _eventAggregator.PublishOnBackgroundThread(new FunctionsLoadedEvent(FunctionGroups));
        }

        public Task Handle(RefreshTablesEvent message)
        {
            return Task.Factory.StartNew(() => {
                GetTables();
                _eventAggregator.PublishOnUIThread(new TablesRefreshedEvent());
            });
        }

        public void Handle(SelectedModelChangedEvent message)
        {
            var model = _connection?.Database?.Models[message.SelectedModel];
            SelectedModelName = message.SelectedModel;
            if (model == null) return;
            SetSelectedModel(model);
        }

    }
}
