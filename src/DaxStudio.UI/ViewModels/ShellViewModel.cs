using System;
using System.Reflection;
using Caliburn.Micro;
using System.ComponentModel.Composition;
using DaxStudio.Interfaces;
using DaxStudio.UI.Events;
using DaxStudio.UI.Utils;
using DaxStudio.UI.Model;
using Serilog;
using System.Windows;

namespace DaxStudio.UI.ViewModels {
    [Export(typeof (IShell))]
    public class ShellViewModel : 
        Screen, 
        IShell,
        IHandle<NewVersionEvent>
    {
        private readonly IWindowManager _windowManager;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDaxStudioHost _host;
        private readonly NotifyIcon notifyIcon;
        private Window _window;

        //private ILogger log;
        [ImportingConstructor]
        public ShellViewModel(IWindowManager windowManager, IEventAggregator eventAggregator ,RibbonViewModel ribbonViewModel, StatusBarViewModel statusBar, IConductor conductor, IDaxStudioHost host, IVersionCheck versionCheck)
        {

            Ribbon = ribbonViewModel;
            Ribbon.Shell = this;
            StatusBar = statusBar;
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
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
            DisplayName = string.Format("DaxStudio - {0}", Version.ToString(3));
            notifyIcon = new NotifyIcon();
            VersionChecker = versionCheck;
            Application.Current.Activated += OnApplicationActivated; 
            Log.Verbose("============ Shell Started - v{version} =============",Version.ToString());
        }

        private void OnApplicationActivated(object sender, EventArgs e)
        {
            Log.Debug("{class} {method}", "ShellViewModel", "OnApplicationActivated");
            //_eventAggregator.PublishOnUIThread(new ApplicationActivatedEvent());
            _eventAggregator.PublishOnUIThreadAsync(new ApplicationActivatedEvent());
            System.Diagnostics.Debug.WriteLine("OnApplicationActivated");
        }

        

        public Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public DocumentTabViewModel Tabs { get; set; }
        public RibbonViewModel Ribbon { get; set; }
        public StatusBarViewModel StatusBar { get; set; }
        public void ContentRendered()
        { }

        public IVersionCheck VersionChecker { get; set; }
        public override void TryClose(bool? dialogResult = null)
        {
            //Properties.Settings.Default.Save();
            base.TryClose(dialogResult);
            if (dialogResult == true )
            {
                notifyIcon.Dispose();
            }
        }
        //public override void TryClose()
        //{
        //    base.TryClose();
        //}

        protected override void OnDeactivate(bool close)
        {
            base.OnDeactivate(close);
            TryClose();
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            _eventAggregator.PublishOnUIThread(new ApplicationActivatedEvent());
        }

        
        protected override void OnViewLoaded(object view)
        {
            base.OnViewReady(view);
            // load the saved window positions
            _window = view as Window;
            _window.Closing += windowClosing;
            // SetPlacement will adjust the position if it's outside of the visible boundaries
            //_window.SetPlacement(Properties.Settings.Default.MainWindowPlacement);
            _window.SetPlacement(RegistryHelper.GetWindowPosition());
        }

        void windowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Store the current window position
            var w = sender as Window;
            //Properties.Settings.Default.MainWindowPlacement = w.GetPlacement();
            //Properties.Settings.Default.Save();
            RegistryHelper.SetWindowPosition(w.GetPlacement());
            _window.Closing -= windowClosing;
        }

        public override void CanClose(Action<bool> callback)
        {
            Tabs.CanClose(callback);
        }

        public void Handle(NewVersionEvent message)
        {           
            
            var newVersionText = string.Format("Version {0} is available for download.\nClick here to go to the download page",message.NewVersion.ToString(3));
            Log.Debug("{class} {method} {message}", "ShellViewModel", "Handle<NewVersionEvent>", newVersionText);
            notifyIcon.Notify(newVersionText, message.DownloadUrl);
        }

        #region Global Keyboard Hooks
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
            _eventAggregator.PublishOnUIThread(new NewDocumentEvent(Ribbon.SelectedTarget));
        }

        public void OpenDocument()
        {
            _eventAggregator.PublishOnUIThread(new OpenFileEvent());
        }

        public void SelectionToUpper()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToUpper));
        }

        public void SelectionToLower()
        {
            _eventAggregator.PublishOnUIThread(new SelectionChangeCaseEvent(ChangeCase.ToLower));
        }

        public void UncommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(false));
        }

        public void CommentSelection()
        {
            _eventAggregator.PublishOnUIThread(new CommentEvent(true));
        }

        public void Undo()
        {
            Ribbon.Undo();
        }

        public void Redo()
        {
            Ribbon.Redo();
        }

        public void Find()
        {
            Ribbon.FindNow();
        }

        public void FindPrev()
        {
            Ribbon.FindPrevNow();
        }

        public void FormatQuery()
        {
            Ribbon.FormatQuery();
        }
        #endregion
    }


}