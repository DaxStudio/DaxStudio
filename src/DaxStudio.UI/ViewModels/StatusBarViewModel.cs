using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils;
using Serilog;

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
        public StatusBarViewModel(IEventAggregator eventAggregator, IGlobalOptions options)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.SubscribeOnPublishedThread(this);
            Options = options;
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
                NotifyOfPropertyChange(() => CanCopyServerNameToClipboard);
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
            get { return (_spid == "" || _spid== "0" )?"-":_spid; } 
            set { _spid = value;
                  NotifyOfPropertyChange(()=>Spid);
                }
        }

        private string _timerText = "00:00.0";
        public string TimerText { get { return _timerText; }
            set
            {
                _timerText = value;
                NotifyOfPropertyChange(() => TimerText);
            }
        }
        
        public string PositionText { get; set; }

        public Task HandleAsync(EditorPositionChangedMessage message, CancellationToken cancellationToken)
        {
            PositionText = string.Format("Ln {0}, Col {1} ", message.Line, message.Column);
            NotifyOfPropertyChange(()=>PositionText);
            return Task.CompletedTask;
        }

        public Task HandleAsync(DocumentConnectionUpdateEvent message, CancellationToken cancellationToken)
        {
            
            if (message != null)
            {
                if (message.Connection != null)
                {
                    ServerName = message.Connection.IsPowerPivot?"<Power Pivot>": message.Connection.ServerName;
                    Spid = message.Connection.SPID.ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    
                    ServerName = "";
                    Spid = "";
                }
            }
            return Task.CompletedTask;
        }

        private int _rowCount = 0;
        private IEventAggregator _eventAggregator;

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
                    return string.Format("{0:n0} row{1}", RowCount, RowCount!=1?"s":""); } 
                else { 
                    return ""; 
                } 
            }
        }
        public Task HandleAsync(ActivateDocumentEvent message,CancellationToken cancellationToken)
        {
            Log.Verbose(Common.Constants.LogMessageTemplate, nameof(StatusBarViewModel), "HandleAsync<ActivateDocumentMessage>", "Starting");
            if (message.Document == null ) return Task.CompletedTask;
            // remove handler for previous active document
            if (ActiveDocument != null)
            {
                ActiveDocument.PropertyChanged -= ActiveDocument_PropertyChanged;
            }
            // set new active document
            ActiveDocument = message.Document;
            // add property changed handler for new document
            ActiveDocument.PropertyChanged += ActiveDocument_PropertyChanged;
            //TimerText = message.Document.ElapsedQueryTime;
            //NotifyOfPropertyChange(() => TimerText);
            NotifyOfPropertyChange(() => ActiveDocument);
            Spid = ActiveDocument.Spid.ToString() ;
            ServerName = ActiveDocument.ServerName;
            ServerVersion = ActiveDocument.ServerVersion;
            TimerText = ActiveDocument.ElapsedQueryTime;
            Message = ActiveDocument.StatusBarMessage;
            RowCount = ActiveDocument.RowCount;
            Log.Verbose(Common.Constants.LogMessageTemplate, nameof(StatusBarViewModel), "HandleAsync<ActivateDocumentMessage>", "Finished");
            return Task.CompletedTask;
        }

        void ActiveDocument_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "StatusBarMessage":
                    Message = ActiveDocument.StatusBarMessage;
                    break;
                case "Spid":
                    Spid = ActiveDocument.Spid.ToString();
                    break;
                case "ServerName":
                    ServerName = ActiveDocument.ServerName;
                    break;
                case "ServerVersion":
                    ServerVersion = ActiveDocument.ServerVersion;
                    break;
                case "ElapsedQueryTime":
                    TimerText = ActiveDocument.ElapsedQueryTime;
                    break;
                case "RowCount":
                    RowCount = ActiveDocument.RowCount;
                    break;
            }
        }

        public bool CanCopyServerNameToClipboard { get => !string.IsNullOrWhiteSpace(_serverName);}
        public void CopyServerNameToClipboard()
        {
            try
            {
                System.Windows.Clipboard.SetText(ServerName);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Copied Server Name: \"{ServerName}\" to clipboard"));
            }
            catch(Exception ex)
            {
                Log.Error(ex, "{class} {method} {message}", "StatusBarViewModel", "CopyServerNameToClipboard", "Error copying server name to clipboard:\n" + ex.Message);
                _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, $"Error copying server name to clipboard:\n{ex.Message}"));
            }
        }


        //public bool ShowDatabaseID { get => Options.ShowDatabaseIdStatus; }

        //public bool CanCopyDatabaseIDToClipboard { get => !string.IsNullOrWhiteSpace(DatabaseID); }
        //public void CopyDatabaseIdToClipboard()
        //{
        //    try
        //    {
        //        System.Windows.Clipboard.SetText(DatabaseID);
        //        _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Information, $"Copied Database ID: \"{DatabaseID}\" to clipboard"));
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(ex, "{class} {method} {message}", nameof(StatusBarViewModel), nameof(CopyDatabaseIdToClipboard), "Error copying DatabaseID to clipboard: " + ex.Message);
        //        _eventAggregator.PublishOnUIThreadAsync(new OutputMessage(MessageType.Error, "Error copying DatabaseID to clipboard, please try again"));
        //    }
        //}

        public DocumentViewModel ActiveDocument { get; set; }
        public IGlobalOptions Options { get; }

        public Task HandleAsync(ConnectionClosedEvent message, CancellationToken cancellationToken)
        {
            ServerName = string.Empty;
            return Task.CompletedTask;
        }


    }
}
