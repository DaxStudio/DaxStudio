using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using Microsoft.Win32;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(RibbonViewModel))]
    public class RibbonViewModel : PropertyChangedBase
        , IHandle<ConnectionChangedEvent>
        , IHandle<ActivateDocumentEvent>
    {
        private readonly IDaxStudioHost _host;
        private readonly IEventAggregator _eventAggregator;
        private ADOTabularConnection _connection;
        private bool databaseComboChanging = false;


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
            _eventAggregator.Publish(new RunQueryEvent(SelectedTarget) );
        }

        public bool CanRunQuery()
        {
            return ActiveDocument.CanRunQuery;
        }

        public void Save()
        {
            ActiveDocument.Save();
        }
        public void SaveAs()
        {
            ActiveDocument.SaveAs();
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

        public void Handle(ConnectionChangedEvent message)
        {
            _connection = message.Connection;
            if (_connection == null)
            {
                Databases = null;
                SelectedDatabase = null;
                return;
            }
            Databases = _connection.Databases;
            NotifyOfPropertyChange(()=> Databases);
            databaseComboChanging = true;
            SelectedDatabase = _connection.Database.Name;
            databaseComboChanging = false;

        }

        private string _selectedDatabase;
        public string SelectedDatabase {
            get { return _selectedDatabase; }
            set
            {
                if (value == _selectedDatabase)
                    return;
                if (!databaseComboChanging)
                {
                    databaseComboChanging = true;

                    _selectedDatabase = value;
                    _connection.ChangeDatabase(_selectedDatabase);
                    //NotifyOfPropertyChange(()=> SelectedDatabase);
                    _eventAggregator.Publish(new UpdateConnectionEvent(_connection));

                    databaseComboChanging = false;
                }
            }
        }

        [ImportMany]
        public List<IResultsTarget> ResultsTargets {get; set; }

        private IResultsTarget _selectedTarget;
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
            NotifyOfPropertyChange(()=> TraceWatchers);
        }

        protected DocumentViewModel ActiveDocument { get; set; }
    }
}
