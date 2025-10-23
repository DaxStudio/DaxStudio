using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;

namespace DaxStudio.UI.ViewModels
{
    [PartCreationPolicy(CreationPolicy.NonShared)]
    [Export]
    public class OutputPaneViewModel:ToolWindowBase
    {
        private readonly BindableCollection<OutputMessage> _messages;
        private readonly IEventAggregator _eventAggregator;
        [ImportingConstructor]
        public OutputPaneViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _messages = new BindableCollection<OutputMessage>();
        }

        public IObservableCollection<OutputMessage> Messages { get { return _messages; }
            
        }

        public void AddInformation(string message)
        {
            _messages.Add(new OutputMessage(MessageType.Information, message));
        }

        public void AddSuccess(string message, double durationMs)
        {
            _messages.Add(new OutputMessage(MessageType.Success, message, durationMs));
        }

        public void AddWarning(string message)
        {
            _messages.Add(new OutputMessage(MessageType.Warning, message));
        }

        public void AddError(string message, double durationMs)
        {
            var msg = new OutputMessage(MessageType.Error, message,durationMs);
            
            _messages.Add(msg);
        }

        public void AddError(string message,int row, int column)
        {
            _messages.Add(new LocationOutputMessage(MessageType.Error, message, row, column) { Parent = this });
        }

        public override string Title => "Log";

        public override string DefaultDockingPane => "DockBottom";
        public override string ContentId => "output";

        public void Clear()
        {
            Messages.Clear();
            AddInformation("Log Cleared");
        }

        internal void AddMessage(OutputMessage message)
        {
            message.Parent = this;
            Messages.Add(message);
        }

        private ICommand _gotoLocation;
        public ICommand GotoLocation
        {
            get
            {
                if (_gotoLocation == null)
                {
                    _gotoLocation = new Utils.RelayCommand<LocationOutputMessage>(msg =>
                    {
                        Debug.WriteLine($"Goto location ({msg.Row},{msg.Column})");
                        _eventAggregator.PublishOnUIThreadAsync(new NavigateToLocationEvent(msg.Row, msg.Column));

                    });
                }
                return _gotoLocation;
            }
        }

        private ICommand _openFolder;
        public ICommand OpenFolder
        {
            get
            {
                if (_openFolder == null)
                    _openFolder = new Utils.RelayCommand<FolderOutputMessage>(msg =>
                    {
                        Debug.WriteLine($"Open ({msg.FolderPath})");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = msg.FolderPath,
                            UseShellExecute = true,
                            Verb = "open"
                        });
                    });
                
                return _openFolder;
            }
        }
    }


}
