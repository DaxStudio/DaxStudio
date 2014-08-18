using System;
using System.ComponentModel.Composition;
using System.Globalization;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.ViewModels
{
    [Export(typeof(StatusBarViewModel))]
    public class StatusBarViewModel:PropertyChangedBase
        , IHandle<StatusBarMessageEvent>
        , IHandle<EditorPositionChangedMessage>
        , IHandle<UpdateConnectionEvent>
    {
        //private ADOTabularConnection _connection;
        [ImportingConstructor]
        public StatusBarViewModel(IEventAggregator eventAggregator)
        {
            eventAggregator.Subscribe(this);
        }
        /*
        public ADOTabularConnection Connection
        {
            get { return _connection; }
            set
            {
                _connection = value;
                if (_connection != null)
                    _connection.ConnectionChanged += ConnectionOnConnectionChanged;
            }
        }
        */
        public bool Working { get; set; }

        /*private void ConnectionOnConnectionChanged(object sender, EventArgs eventArgs)
        {
            NotifyOfPropertyChange(()=> ServerName);
            NotifyOfPropertyChange(()=> Spid);
        }
        */
        public void Handle(StatusBarMessageEvent message)
        {
            Message = message.Text;
            Working = (Message != "Ready");
            NotifyOfPropertyChange(()=>Message);
            NotifyOfPropertyChange(() => Working);
        }

        public string Message { get; set; }
        private string _serverName = "";
        public string ServerName { get { return _serverName==""?"<Not Connected>":_serverName; }
            set
            {
                _serverName = value;
                NotifyOfPropertyChange(()=>ServerName); 
            } }

        private string _spid = "";
        public string Spid 
        { 
            get { return _spid == ""?"-":_spid; } 
            set { _spid = value;
                  NotifyOfPropertyChange(()=>Spid);
                }
        }

        public string TimerText { get; set; }
        
        public string PositionText { get; set; }

        public void Handle(EditorPositionChangedMessage message)
        {
            PositionText = string.Format("Ln {0}, Col {1} ", message.Line, message.Column);
            NotifyOfPropertyChange(()=>PositionText);
        }

        public void Handle(UpdateConnectionEvent message)
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
        }
    }
}
