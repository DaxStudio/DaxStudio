using System;
using System.Reflection;
using Caliburn.Micro;
using System.ComponentModel.Composition;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils;
using Serilog;

namespace DaxStudio.UI.ViewModels {
    [Export(typeof (IShell))]
    public class ShellViewModel : Screen, IShell
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDaxStudioHost _host;
        private ILogger log;
        [ImportingConstructor]
        public ShellViewModel(IWindowManager windowManager, IEventAggregator eventAggregator ,RibbonViewModel ribbonViewModel, StatusBarViewModel statusBar, IConductor conductor, IDaxStudioHost host)
        {
            Ribbon = ribbonViewModel;
            Ribbon.Shell = this;
            StatusBar = statusBar;
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            Tabs = (DocumentTabViewModel) conductor;
            Tabs.ConductWith(this);
            Tabs.CloseStrategy = new ApplicationCloseStrategy();
            _host = host;
            if (_host.CommandLineFileName != string.Empty)
            {
                Tabs.NewQueryDocument(_host.CommandLineFileName);
            }
            else
            {
                Tabs.NewQueryDocument();
            }
            DisplayName = string.Format("DaxStudio - v{0}.{1}", Version.Major, Version.Minor);
            log = new LoggerConfiguration().ReadAppSettings().CreateLogger();
            Log.Logger = log;
            Log.Verbose("============ Application Launch =============");
        }

        public Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public DocumentTabViewModel Tabs { get; set; }
        public RibbonViewModel Ribbon { get; set; }
        public StatusBarViewModel StatusBar { get; set; }
        public void ContentRendered()
        { }

        public override void TryClose()
        {
            base.TryClose();

        }

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            TryClose();
        }

        // Used for Global Keyboard Hooks
        public void RunQuery()
        {
            Ribbon.RunQuery();
        }

        public void SaveCurrentDocument()
        {
            Ribbon.Save();
        }

        public void NewDocument()
        {
            _eventAggregator.Publish(new NewDocumentEvent());
        }

        public void SelectionToUpper()
        {
            _eventAggregator.Publish(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }

        public void SelectionToLower()
        {
            _eventAggregator.Publish(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }
        
        public void UncommentSelection()
        {
            _eventAggregator.Publish(new CommentEvent(false));
        }

        public void CommentSelection()
        {
            _eventAggregator.Publish(new CommentEvent(true));
        }
        
        protected override void OnActivate()
        {
            base.OnActivate();
            _eventAggregator.Publish(new ApplicationActivatedEvent());
        }

        public override void CanClose(Action<bool> callback)
        {
            //base.CanClose(callback);
            Tabs.CanClose(callback);
        }
    }


}