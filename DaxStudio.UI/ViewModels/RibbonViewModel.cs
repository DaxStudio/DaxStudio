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

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<UpdateConnectionEvent>
        , IHandle<ActivateDocumentEvent>
        , IHandle<QueryFinishedEvent>
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private ADOTabularConnection _connection;
        private bool _databaseComboChanging = false;


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

        public void RunQuery()
        {
            _queryRunning = true;
            NotifyOfPropertyChange(()=>CanRunQuery);
            NotifyOfPropertyChange(()=>CanCancelQuery);
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
            _connection.Database.ClearCache();
            _eventAggregator.Publish(new OutputInformationMessageEvent(string.Format("Cache Cleared for Database: {0}",_connection.Database.Name)));
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

        public bool CanConnect()
        {
            return true;
        }

        public ShellViewModel Shell { get; set; }

        public void Exit()
        {
            Shell.TryClose();
        }

        public void Open()
        {
            /*
            // Configure open file dialog box
            var dlg = new OpenFileDialog
                {
                    FileName = "Document",
                    DefaultExt = ".dax",
                    Filter = "DAX documents (.dax)|*.dax"
                };

            // Show open file dialog box
            var result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                // Open document 
                var fileName = dlg.FileName;
                _eventAggregator.Publish(new OpenFileEvent(fileName));
            }
            */
            _eventAggregator.Publish(new OpenFileEvent());
        }

        public void Handle(UpdateConnectionEvent message)
        {
            if (message != null)
            {
                _connection = message.Connection;
            }
            if (_connection == null)
            {
                Databases = null;
                SelectedDatabase = null;
                return;
            }
            Databases = _connection.Databases;
            
            //databaseComboChanging = true;
            SelectedDatabase = _connection.Database.Name;
            NotifyOfPropertyChange(() => Databases);
            //databaseComboChanging = false;

        }

        private string _selectedDatabase; 
        public string SelectedDatabase {
            get { return _selectedDatabase; }
            set
            {
                if (value == _selectedDatabase) return;
                if (_databaseComboChanging) return;

                _databaseComboChanging = true;

                _selectedDatabase = value;
                if (_connection != null && _selectedDatabase != null)
                {
                    if (!_connection.Database.Name.Equals(_selectedDatabase))
                    {
                        _connection.ChangeDatabase(_selectedDatabase);
                        _eventAggregator.Publish(new UpdateConnectionEvent(_connection,_selectedDatabase));
                    }
                }
                    
                NotifyOfPropertyChange(()=> SelectedDatabase);
                _databaseComboChanging = false;
            }
        }

        [ImportMany]
        public List<IResultsTarget> ResultsTargets {get; set; }

        private IResultsTarget _selectedTarget;
        private bool _queryRunning;
        // default to first target if none currently selected
        public IResultsTarget SelectedTarget {
            get { return _selectedTarget ?? ResultsTargets[0]; }
            set { _selectedTarget = value;
            NotifyOfPropertyChange(()=>SelectedTarget);}
        }

        public IObservableCollection<ITraceWatcher> TraceWatchers { get { return ActiveDocument == null ? null : ActiveDocument.TraceWatchers; } } 

        public ADOTabularDatabaseCollection Databases { get; set; }
        public void Handle(ActivateDocumentEvent message)
        {
            ActiveDocument = message.Document;
            //ActiveDocument.PropertyChanged += ActiveDocumentOnPropertyChanged;   
            NotifyOfPropertyChange(()=> TraceWatchers);
            if (ActiveDocument.Connection != null)
            {
                foreach (var tw in TraceWatchers)
                {
                    tw.IsEnabled = (ActiveDocument.Connection.Type == AdomdType.AnalysisServices);
                }
            }
            
        }

        private void ActiveDocumentOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
        /*    if (propertyChangedEventArgs.PropertyName != "CanRunQuery") return;
            NotifyOfPropertyChange(() => CanRunQuery());
            NotifyOfPropertyChange(()=> CanCancelQuery());
         */
        }

        protected DocumentViewModel ActiveDocument { get; set; }

        public IEnumerable<string> Worksheets
        {
            get { return _host.Worksheets; }
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
            NotifyOfPropertyChange(()=>CanRunQuery);
            NotifyOfPropertyChange(()=>CanCancelQuery);
            NotifyOfPropertyChange(()=>CanClearCache);
        }
    }
}
