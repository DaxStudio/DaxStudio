using System;
using System.Globalization;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.UI.Events;

namespace DaxStudio.UI.ViewModels
{
    public class StatusBarViewModel:PropertyChangedBase
        , IHandle<StatusBarMessageEvent>
        , IHandle<EditorPositionChangedMessage>
    {
        private ADOTabularConnection _connection;

        public StatusBarViewModel(IEventAggregator eventAggregator, ADOTabularConnection connection)
        {
            eventAggregator.Subscribe(this);
            Connection = connection;
        }

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

        private void ConnectionOnConnectionChanged(object sender, EventArgs eventArgs)
        {
            NotifyOfPropertyChange(()=> ServerName);
            NotifyOfPropertyChange(()=> Spid);
        }

        public void Handle(StatusBarMessageEvent message)
        {
            Message = message.Text;
            NotifyOfPropertyChange(()=>Message);
        }

        public string Message { get; set; }
        public string ServerName { get { return _connection==null?"<Not Connected>": _connection.ServerName; } }
        public string Spid { get { return _connection==null?" - ": _connection.SPID.ToString(CultureInfo.InvariantCulture); } }
        public string TimerText { get; set; }
        public string PositionText { get; set; }

        public void Handle(EditorPositionChangedMessage message)
        {
            PositionText = string.Format("Ln {0}, Col {1} ", message.Line, message.Column);
            NotifyOfPropertyChange(()=>PositionText);
        }
    }
}
