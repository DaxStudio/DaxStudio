using System;
using System.ComponentModel.Composition;
using System.Globalization;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(StatusBarViewModel))]
    public class StatusBarViewModel:PropertyChangedBase
        , IHandle<EditorPositionChangedMessage>
        , IHandle<DocumentConnectionUpdateEvent>
        , IHandle<ConnectionClosedEvent>
        , IHandle<ActivateDocumentEvent>
    {
        
        [ImportingConstructor]
        public StatusBarViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);
        }

        public bool Working { get; set; }

        private string _message = "Ready";
        public string Message { 
            get { return _message; }
            set { 
                _message = value;
                NotifyOfPropertyChange(() => Message);
                Working = (Message != "Ready");
                NotifyOfPropertyChange(() => Working) ;
            }
        }
        private string _serverName = "";
        public string ServerName { get { return string.IsNullOrWhiteSpace(_serverName)?"<Not Connected>":_serverName; }
            set
            {
                _serverName = value;
                NotifyOfPropertyChange(()=>ServerName); 
            } }

        private string _serverVersion = "";
        public string ServerVersion
        {
            get { return string.IsNullOrWhiteSpace(_serverVersion) ? "" : _serverVersion; }
            set
            {
                _serverVersion = value;
                NotifyOfPropertyChange(() => ServerVersion);
            }
        }


        private string _spid = "";
        
        public string Spid 
        { 
            get { return _spid == ""?"-":_spid; } 
            set { _spid = value;
                  NotifyOfPropertyChange(()=>Spid);
                }
        }

        private string _timerText = "";
        public string TimerText { get { return _timerText; }
            set
            {
                _timerText = value;
                NotifyOfPropertyChange(() => TimerText);
            }
        }
        
        public string PositionText { get; set; }

        public void Handle(EditorPositionChangedMessage message)
        {
            PositionText = string.Format("Ln {0}, Col {1} ", message.Line, message.Column);
            NotifyOfPropertyChange(()=>PositionText);
        }

        public void Handle(DocumentConnectionUpdateEvent message)
        {
            
            if (message != null)
            {
                if (message.Connection != null)
                {
                    ServerName = message.Connection.IsPowerPivot?"<Power Pivot>": message.Connection.ServerName;
                    Spid = message.Connection.Spid.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    
                    ServerName = "";
                    Spid = "";
                }
            }
        }

        private int _rowCount = -1;
        public int RowCount
        {
            get { return _rowCount; }
            set
            {
                _rowCount = value;
                NotifyOfPropertyChange(() => Rows);
            }
        }
        public string Rows
        {
            get { 
                if (RowCount >= 0) { 
                    return string.Format("{0} row{1}", RowCount, RowCount!=1?"s":""); } 
                else { 
                    return ""; 
                } 
            }
        }
        public void Handle(ActivateDocumentEvent message)
        {
            if (message.Document == null ) return;
            // remove handler for previous active document
            if (ActiveDocument != null)
            {
                ActiveDocument.PropertyChanged -= ActiveDocument_PropertyChanged;
            }
            // set new active document
            ActiveDocument = message.Document;
            // add property changed handler for new document
            ActiveDocument.PropertyChanged += ActiveDocument_PropertyChanged;
            TimerText = message.Document.ElapsedQueryTime;
            NotifyOfPropertyChange(() => TimerText);
            NotifyOfPropertyChange(() => ActiveDocument);
            Spid = ActiveDocument.Spid.ToString() ;
            ServerName = ActiveDocument.ServerName;
            ServerVersion = ActiveDocument.ServerVersion;
            TimerText = ActiveDocument.ElapsedQueryTime;
            Message = ActiveDocument.StatusBarMessage;
            RowCount = ActiveDocument.RowCount;
        }

        void ActiveDocument_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "StatusBarMessage":
                    Message = ActiveDocument.StatusBarMessage;
                    break;
                case "Spid":
                    NotifyOfPropertyChange(() => Spid);
                    break;
                case "ServerName":
                    NotifyOfPropertyChange(() => ServerName);
                    break;
                case "ServerVersion":
                    NotifyOfPropertyChange(() => ServerVersion);
                    break;
                case "ElapsedQueryTime":
                    TimerText = ActiveDocument.ElapsedQueryTime;
                    break;
                case "RowCount":
                    RowCount = ActiveDocument.RowCount;
                    //NotifyOfPropertyChange(()=>Rows);
                    break;
            }
        }

        public DocumentViewModel ActiveDocument { get; set; }

        public void Handle(ConnectionClosedEvent message)
        {
            ServerName = string.Empty;
        }
        
    }
}
