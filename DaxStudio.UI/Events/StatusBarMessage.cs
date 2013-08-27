using System;
using Caliburn.Micro;

namespace DaxStudio.UI.Events
{
    class StatusBarMessage : IDisposable
    {
        private IEventAggregator _eventAggregator;

        public StatusBarMessage(string message)
        {
            _eventAggregator = IoC.Get<IEventAggregator>();
            _eventAggregator.Publish(new StatusBarMessageEvent(message));
        }

        public void Dispose()
        {
            // reset the message to the default text on dispose.
            _eventAggregator.Publish(new StatusBarMessageEvent("Ready"));
            _eventAggregator = null;
        }
    }

    public class StatusBarMessageEvent
    {
        public StatusBarMessageEvent(string message)
        {
            Text = message;
        }

        public string Text { get; set; }
    }
}
