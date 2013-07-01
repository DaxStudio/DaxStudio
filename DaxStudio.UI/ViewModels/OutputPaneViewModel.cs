using System.ComponentModel.Composition;
using Caliburn.Micro;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.ViewModels
{
    public class OutputPaneViewModel:ToolWindowBase
    {
        private readonly BindableCollection<OutputMessage> _messages;
        [ImportingConstructor]
        public OutputPaneViewModel ()
        {
            _messages = new BindableCollection<OutputMessage>();
        }

        public IObservableCollection<OutputMessage> Messages { get { return _messages; }
            
        }

        public void AddInformation(string message)
        {
            _messages.Add(new OutputMessage(MessageType.Information, message));
        }

        public void AddInformation(string message, double durationMs)
        {
            _messages.Add(new OutputMessage(MessageType.Information, message,durationMs));
        }

        public void AddWarning(string message)
        {
            _messages.Add(new OutputMessage(MessageType.Warning, message));
        }

        public void AddError(string message)
        {
            _messages.Add(new OutputMessage( MessageType.Error,message));
        }

        public override string Title
        {
            get { return "Output"; }
        }

        
    }


}
