using System.Collections.Generic;
using System.ComponentModel.Composition;
using ADOTabular.AdomdClientWrappers;
using Caliburn.Micro;
using DaxStudio.UI.Events;
using DaxStudio.UI.Model;
using Microsoft.AnalysisServices;

namespace DaxStudio.UI.ViewModels
{
    [InheritedExport(typeof(ITraceWatcher)), PartCreationPolicy(CreationPolicy.NonShared)]
    public abstract class TraceWatcherBaseViewModel : PropertyChangedBase, IToolWindow, ITraceWatcher, IHandle<ConnectionChangedEvent>
    {
        private List<TraceEventArgs> _events;
        private readonly IEventAggregator _eventAggregator;

        [ImportingConstructor]
        protected TraceWatcherBaseViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            WaitForEvent = TraceEventClass.QueryEnd;
            //todo - add abstract method
// ReSharper disable DoNotCallOverridableMethodsInConstructor
            MonitoredEvents = GetMonitoredEvents();
// ReSharper restore DoNotCallOverridableMethodsInConstructor

        }

        public List<TraceEventClass> MonitoredEvents { get; private set; }
        public TraceEventClass WaitForEvent { get; set; }

        // this is a list of the events captured by this trace watcher
        public List<TraceEventArgs> Events
        {
            get { return _events ?? (_events = new List<TraceEventArgs>()); }
        }

        protected abstract List<TraceEventClass> GetMonitoredEvents();

        // This method is called after the WaitForEvent is seen (usually the QueryEnd event)
        // This is where you can do any processing of the events before displaying them to the UI
        protected abstract void ProcessResults();


        // This method is called as events are raised
        public void ProcessEvent(TraceEventArgs eventArgs)
        {
            if (MonitoredEvents.Contains(eventArgs.EventClass))
            {
                Events.Add(eventArgs);
            }
            if (eventArgs.EventClass == WaitForEvent)
            {
                ProcessResults();
            }
        }

        // This method is called before a trace starts which gives you a chance to 
        // reset any stored state
        public void Reset()
        {
            Events.Clear();
        }

        
        // IToolWindow interface
        public abstract string Title { get; set; }

        public virtual string DefaultDockingPane
        {
            get { return "DockBottom"; }
            set { }
        }

        public bool CanClose
        {
            get { return false; }
            set { }
        }
        public bool CanHide
        {
            get { return false; }
            set { }
        }
        public int AutoHideMinHeight { get; set; }
        public bool IsSelected { get; set; }

        private bool _isEnabled ;
        public bool IsEnabled { get { return _isEnabled; }
            set { _isEnabled = value;
            NotifyOfPropertyChange("IsEnabled");} 
        }

        public bool IsActive { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                _isChecked = value;
                _eventAggregator.Publish(new TraceWatcherToggleEvent(this, value));
            }
        }

        public void Handle(ConnectionChangedEvent message)
        {
            IsEnabled = (message.Connection.Type == AdomdType.AnalysisServices);
        }


        public void CheckEnabled(ADOTabular.ADOTabularConnection _connection)
        {
            IsEnabled = (!_connection.IsPowerPivot);
        }
    }
}
