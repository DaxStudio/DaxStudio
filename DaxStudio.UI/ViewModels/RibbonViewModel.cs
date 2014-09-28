using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Windows;
using ADOTabular;
using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using DaxStudio.UI.Properties;
using System.Linq;
using Microsoft.Win32;
using Serilog;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<ConnectionPendingEvent>
        , IHandle<UpdateConnectionEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<QueryFinishedEvent>
        , IHandle<NewDocumentEvent>
        , IHandle<ApplicationActivatedEvent>
        , IHandle<TraceChangingEvent>
        , IHandle<TraceChangedEvent>
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private ADOTabularConnection _connection;
        private bool _databaseComboChanging = false;

        private const string urlDaxStudioWiki = "http://daxstudio.codeplex.com/documentation";
        private const string urlPowerPivotForum = "http://social.msdn.microsoft.com/Forums/sqlserver/en-US/home?forum=sqlkjpowerpivotforexcel";
        private const string urlSsasForum = "http://social.msdn.microsoft.com/Forums/sqlserver/en-US/home?forum=sqlanalysisservices";

        [ImportingConstructor]
        public RibbonViewModel(IDaxStudioHost host, IEventAggregator eventAggregator )
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            _host = host;
            
        }

        public Visibility OutputGroupIsVisible
        {
            get { return _host.IsExcel?Visibility.Visible : Visibility.Collapsed; }
        }

        public void NewQuery()
        {
            _eventAggregator.Publish(new NewDocumentEvent());
        }

        public void CommentSelection()
        {
            _eventAggregator.Publish(new CommentEvent(true));
        }

        public void MergeParameters()
        {
            ActiveDocument.MergeParameters();
        }

        public void Undo()
        {
            ActiveDocument.Undo();
        }

        public void Redo()
        {
            ActiveDocument.Redo();
        }

        public void UncommentSelection()
        {
            _eventAggregator.Publish(new CommentEvent(false));
        }

        public void ToUpper()
        {
            _eventAggregator.Publish(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }

        public void ToLower()
        {
            _eventAggregator.Publish(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }

        public void RunQuery()
        {
            _queryRunning = true;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            _eventAggregator.Publish(new RunQueryEvent(SelectedTarget,SelectedWorksheet) );

        }

        public string RunQueryDisableReason
        {
            get
            {
                if ( _queryRunning) return  "A query is currently executing";
                if (!ActiveDocument.IsConnected) return "Query window not connected to a model";
                if (_traceStatus == QueryTraceStatus.Starting) return "Waiting for Trace to start";
                if (_traceStatus == QueryTraceStatus.Stopping) return "Waiting for Trace to stop";
                return "not disabled";
            }
        }

        public bool CanSelectDatabase
        {
            get
            {
                return ActiveDocument.IsConnected;
            }
        }

        public bool CanRunQuery
        {
            get
            {
                return !_queryRunning && ActiveDocument.IsConnected && (_traceStatus == QueryTraceStatus.Started || _traceStatus == QueryTraceStatus.Stopped);
            }
        }

        public void CancelQuery()
        {
            _eventAggregator.Publish(new CancelQueryEvent());
        }

        public bool CanCancelQuery
        {
            get { return !CanRunQuery && ActiveDocument.IsConnected; }
        }

        public bool CanClearCache
        {
            get { return CanRunQuery; }
        }

        public void ClearCache()
        {
            var doc = ActiveDocument;
            var sw = Stopwatch.StartNew();
            _connection.Database.ClearCache();
            //_eventAggregator.Publish(new OutputInformationMessageEvent(string.Format("Evalating Calculation Script for Database: {0}", _connection.Database.Name)));
            doc.OutputMessage(string.Format("Evalating Calculation Script for Database: {0}", _connection.Database.Name));
            ActiveDocument.ExecuteQueryAsync("EVALUATE ROW(\"BLANK\",0)").ContinueWith((ascendant) => {
                sw.Stop();
                var duration = sw.ElapsedMilliseconds;
                doc.OutputMessage(string.Format("Cache Cleared for Database: {0}",_connection.Database.Name),duration);
            });
        }

        public void Save()
        {
            ActiveDocument.Save();
        }
        public void SaveAs()
        {
            ActiveDocument.SaveAs();
        }

        public void Connect()
        {
            ActiveDocument.ChangeConnection();
        }

        private bool _canConnect;
        public bool CanConnect
        {
            get { return _canConnect; }
            set { 
                _canConnect = value;
                NotifyOfPropertyChange(()=> CanConnect);
            }
        }

        public ShellViewModel Shell { get; set; }

        public void Exit()
        {
            Shell.TryClose();
        }

        public void Open()
        {
            _eventAggregator.Publish(new OpenFileEvent());
        }

        public void Handle(UpdateConnectionEvent message)
        {
            RefreshConnectionDetails(message.Connection, message.DatabaseName);
        }

        private void RefreshConnectionDetails(ADOTabularConnection connection, string databaseName)
        {
            var doc = ActiveDocument;
            //if (connection != null)
            //{
                _connection = connection;
            //}
            if (_connection == null)
            {
                Databases = null;
                SelectedDatabase = null;
                Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "RibbonViewModel", "RefreshConnectionDetails", "<null>", "<null>");
                CanConnect = true;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanSelectDatabase);
                return;
            }
            try
            {
                Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "RibbonViewModel", "RefreshConnectionDetails", _connection.ConnectionString, databaseName);
                Databases = _connection.Databases;

                _databaseComboChanging = true;
                NotifyOfPropertyChange(() => Databases);
                //NotifyOfPropertyChange(() => SelectedDatabase);
                _databaseComboChanging = false;

                SelectedDatabase = _connection.Database.Name;
                NotifyOfPropertyChange(()=>SelectedDatabase);
            }
            catch (Exception ex)
            {
                //_eventAggregator.Publish(new OutputMessage(MessageType.Error, ex.Message));
                doc.OutputError(ex.Message);
            }
            finally
            {
                CanConnect = true;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanClearCache);
                NotifyOfPropertyChange(() => CanSelectDatabase);
            }
        }

        private string _selectedDatabase; 
        public string SelectedDatabase {
            get { return _selectedDatabase; }
            set
            {
                var doc = ActiveDocument;
                //Log.Debug("{Class} {Event} {selectedDatabase}", "RibbonViewModel", "SelectedDatabase:Set", value);
                if (_databaseComboChanging) return;
                if (value == _selectedDatabase )
                { 
                    NotifyOfPropertyChange(()=> SelectedDatabase);
                    return; 
                }

                _databaseComboChanging = true;
                
                _selectedDatabase = value;

                var msg = doc.NewStatusBarMessage("Loading Metadata...");
                //_eventAggregator.Publish(new StatusBarMessage("Loading Metadata..."));
                

                //Task.Factory.StartNew(() =>  {
                //Execute.OnUIThreadAsync(() => {
                    if (_connection != null && _selectedDatabase != null)
                    {
                        if (!_connection.Database.Name.Equals(_selectedDatabase))
                        {
                            Log.Debug("{Class} {Event} {selectedDatabase}", "RibbonViewModel", "SelectedDatabase:Set (changing)", value);
                            _connection.ChangeDatabase(_selectedDatabase);
                            _eventAggregator.Publish(new UpdateConnectionEvent(_connection, _selectedDatabase));//, ActiveDocument.IsPowerPivotConnection));
                        }
                    }

                    NotifyOfPropertyChange(() => SelectedDatabase);

                //}).ContinueWith((x) =>
                //{
                    msg.Dispose();
                    //msg.Dispose();
                    _databaseComboChanging = false;
                //}); 
            }
        }

        [ImportMany]
        public IEnumerable<IResultsTarget> AvailableResultsTargets {get; set; }

        public IEnumerable<IResultsTarget> ResultsTargets { get {
            //return  AvailableResultsTargets.OrderBy<IEnumerable<IResultsTarget>,int>(AvailableResultsTargets, x => x.DisplayOrder).Where(x=> x.IsEnabled.ToList<IResultsTarget>();
            return (from t in AvailableResultsTargets
                    where t.IsEnabled
                    select t).OrderBy(x => x.DisplayOrder).AsEnumerable<IResultsTarget>();
        } }

        private IResultsTarget _selectedTarget;
        private bool _queryRunning;
        private QueryTraceStatus _traceStatus;
        // default to first target if none currently selected
        public IResultsTarget SelectedTarget {
            get { return _selectedTarget ?? AvailableResultsTargets.Where(x => x.IsDefault).First<IResultsTarget>(); }
            set { _selectedTarget = value;
            NotifyOfPropertyChange(()=>SelectedTarget);}
        }

        public IObservableCollection<ITraceWatcher> TraceWatchers { get { return ActiveDocument == null ? null : ActiveDocument.TraceWatchers; } } 

        public ADOTabularDatabaseCollection Databases { get; set; }
        public void Handle(ActivateDocumentEvent message)
        {
            Log.Debug("{Class} {Event} {Document}", "RibbonViewModel", "Handle:ActivateDocumentEvent", message.Document.DisplayName);
            ActiveDocument = message.Document;
            var doc = ActiveDocument;
            //ActiveDocument.PropertyChanged += ActiveDocumentOnPropertyChanged;   
            
        //if (ActiveDocument.IsQueryRunning != _queryRunning)
        //{
            _queryRunning = ActiveDocument.IsQueryRunning;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
            NotifyOfPropertyChange(() => CanSelectDatabase);
        //}


            if (ActiveDocument.Connection == null)
            {
                _selectedDatabase = null;
                NotifyOfPropertyChange(() => SelectedDatabase);
                UpdateTraceWatchers();
                NotifyOfPropertyChange(() => TraceWatchers);
                return;
            }
            try
            {
                if (ActiveDocument.Connection.State == System.Data.ConnectionState.Open)
                {
                    Databases = null;
                    _selectedDatabase = null;
                    RefreshConnectionDetails(ActiveDocument.Connection, ActiveDocument.Connection.Database.Name);
                    // TODO - do we still need to check trace watchers if we are not connected??
                    UpdateTraceWatchers();
                }
            }
            catch (AdomdConnectionException ex)
            {
                //_eventAggregator.Publish(new OutputMessage(MessageType.Error, ex.Message));
                Log.Error("{Exception}", ex);
                doc.OutputError(ex.Message);
            }
            NotifyOfPropertyChange(() => TraceWatchers);
        }

        private void UpdateTraceWatchers()
        {
            foreach (var tw in TraceWatchers)
            {
                // TODO - can we enable traces for PowerPivot?
                //    tw.IsEnabled = (ActiveDocument.Connection.Type == AdomdType.AnalysisServices);
                tw.CheckEnabled(ActiveDocument.Connection);
            }
        }

        protected DocumentViewModel ActiveDocument { get; set; }
        // TODO - should this be an observable collection??
        public IEnumerable<string> Worksheets
        {
            get { return _host.Proxy.Worksheets; }
        }

        public string SelectedWorksheet {
            get { return ActiveDocument.SelectedWorksheet; } 
            set { ActiveDocument.SelectedWorksheet = value; } }

        // TODO - configure MRU list
        public System.Collections.Specialized.StringCollection RecentDocuments
        {
            get { return Settings.Default.RecentDocuments; }
            
        }

        public void Handle(QueryFinishedEvent message)
        {
            _queryRunning = false;
            NotifyOfPropertyChange(() => CanRunQuery);
            NotifyOfPropertyChange(() => CanCancelQuery);
            NotifyOfPropertyChange(() => CanClearCache);
        }

        public void LinkToDaxStudioWiki()
        {
            System.Diagnostics.Process.Start(urlDaxStudioWiki);
        }

        public void LinkToPowerPivotForum()
        {
            System.Diagnostics.Process.Start(urlPowerPivotForum);
        }

        public void LinkToSsasForum()
        {
            System.Diagnostics.Process.Start(urlSsasForum);
        }

        public void Handle(ConnectionPendingEvent message)
        {
            CanConnect = false;
        }
        public void Handle(ApplicationActivatedEvent message)
        {
            Log.Debug("{Class} {Event} {@ApplicationActivatedEvent}", "DocumentViewModel", "ApplicationActivatedEvent", message);
            if (_host.IsExcel)
            {
                //TODO - refresh workbooks and powerpivot conn if the host is excel
                NotifyOfPropertyChange(() => Worksheets);
            }
        }

        public void Handle(NewDocumentEvent message)
        {
            Databases = null;
            SelectedDatabase = null;
        }

        public void Handle(TraceChangingEvent message)
        {
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
        }

        public void Handle(TraceChangedEvent message)
        {
            _traceStatus = message.TraceStatus;
            NotifyOfPropertyChange(() => CanRunQuery);
        }
    }
}
