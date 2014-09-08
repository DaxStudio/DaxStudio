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

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<ConnectionPendingEvent>
        , IHandle<UpdateConnectionEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<QueryFinishedEvent>
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

        public bool CanRunQuery
        {
            get
            {
                return !_queryRunning;
            }
        }

        public void CancelQuery()
        {
            _eventAggregator.Publish(new CancelQueryEvent());
        }

        public bool CanCancelQuery
        {
            get { return !CanRunQuery; }
        }

        public bool CanClearCache
        {
            get { return CanRunQuery; }
        }

        public void ClearCache()
        {
            var sw = Stopwatch.StartNew();
            _connection.Database.ClearCache();
            _eventAggregator.Publish(new OutputInformationMessageEvent(string.Format("Evalating Calculation Script for Database: {0}", _connection.Database.Name)));
            ActiveDocument.ExecuteQueryAsync("EVALUATE ROW(\"BLANK\",0)").ContinueWith((ascendant) => {
                sw.Stop();
                var duration = sw.ElapsedMilliseconds;
                _eventAggregator.Publish(new OutputInformationMessageEvent(string.Format("Cache Cleared for Database: {0}",_connection.Database.Name),duration));
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
            //if (connection != null)
            //{
                _connection = connection;
            //}
            if (_connection == null)
            {
                Databases = null;
                SelectedDatabase = null;
                Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "RibbonViewModel", "Handle:UpdateConnectionEvent", "<null>", "<null>");
                CanConnect = true;
                return;
            }
            try
            {
                Log.Debug("{Class} {Event} {Connection} {selectedDatabase}", "RibbonViewModel", "Handle:UpdateConnectionEvent", _connection.ConnectionString, databaseName);
                Databases = _connection.Databases;

                _databaseComboChanging = true;
                NotifyOfPropertyChange(() => Databases);
                _databaseComboChanging = false;

                SelectedDatabase = _connection.Database.Name;
            }
            catch (Exception ex)
            {
                _eventAggregator.Publish(new OutputMessage(MessageType.Error, ex.Message));
            }
            finally
            {
                CanConnect = true;
            }
        }

        private string _selectedDatabase; 
        public string SelectedDatabase {
            get { return _selectedDatabase; }
            set
            {
                //Log.Debug("{Class} {Event} {selectedDatabase}", "RibbonViewModel", "SelectedDatabase:Set", value);
                if (value == _selectedDatabase) return;
                if (_databaseComboChanging) return;

                _databaseComboChanging = true;
                
                _selectedDatabase = value;
                if (_connection != null && _selectedDatabase != null)
                {
                    if (!_connection.Database.Name.Equals(_selectedDatabase))
                    {
                        Log.Debug("{Class} {Event} {selectedDatabase}", "RibbonViewModel", "SelectedDatabase:Set (changing)", value);
                        _connection.ChangeDatabase(_selectedDatabase);
                        _eventAggregator.Publish(new UpdateConnectionEvent(_connection,_selectedDatabase));//, ActiveDocument.IsPowerPivotConnection));
                    }
                }
                    
                NotifyOfPropertyChange(()=> SelectedDatabase);
                _databaseComboChanging = false;
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
            //ActiveDocument.PropertyChanged += ActiveDocumentOnPropertyChanged;   

            if (ActiveDocument.IsQueryRunning != _queryRunning)
            {
                _queryRunning = ActiveDocument.IsQueryRunning;
                NotifyOfPropertyChange(() => CanRunQuery);
                NotifyOfPropertyChange(() => CanCancelQuery);
                NotifyOfPropertyChange(() => CanClearCache);
            }

            
            if (ActiveDocument.Connection != null)
            {
                RefreshConnectionDetails(ActiveDocument.Connection, ActiveDocument.Connection.Database.Name);
                foreach (var tw in TraceWatchers)
                {
                    // TODO - can we enable traces for PowerPivot?
                    //    tw.IsEnabled = (ActiveDocument.Connection.Type == AdomdType.AnalysisServices);
                    tw.CheckEnabled(ActiveDocument.Connection);
                }
            }
            NotifyOfPropertyChange(() => TraceWatchers);
        }
        private void ActiveDocumentOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        { }
        

        protected DocumentViewModel ActiveDocument { get; set; }

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
    }
}
